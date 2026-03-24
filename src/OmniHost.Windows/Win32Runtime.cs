using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using OmniHost.Core;

namespace OmniHost.Windows;

/// <summary>
/// AOT-compatible <see cref="IDesktopRuntime"/> that creates raw Win32 windows
/// and runs native message loops without any WinForms or WPF dependency.
/// </summary>
/// <remarks>
/// Each host window runs on its own dedicated STA thread so that COM-backed
/// browser adapters such as WebView2 always execute in a compatible apartment.
/// </remarks>
public sealed class Win32Runtime : IMultiWindowDesktopRuntime
{
    private readonly IHostWindowFactory _windowFactory;
    private readonly HostWindowCoordinator _coordinator;
    private readonly object _executionGate = new();
    private RuntimeExecution? _currentExecution;

    /// <summary>
    /// Creates a Win32 runtime using the default raw Win32 host window implementation.
    /// </summary>
    public Win32Runtime()
        : this(new Win32HostWindowFactory())
    {
    }

    /// <summary>
    /// Creates a Win32 runtime with a custom host-window factory.
    /// </summary>
    public Win32Runtime(IHostWindowFactory windowFactory)
        : this(windowFactory, new HostWindowCoordinator())
    {
    }

    internal Win32Runtime(IHostWindowFactory windowFactory, HostWindowCoordinator coordinator)
    {
        _windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    /// <inheritdoc/>
    public void Run(
        OmniHostOptions options,
        IWebViewAdapterFactory adapterFactory,
        IDesktopApp? desktopApp)
        => Run(options, Array.Empty<OmniWindowDefinition>(), adapterFactory, desktopApp);

    /// <inheritdoc/>
    public void Run(
        OmniHostOptions options,
        IReadOnlyList<OmniWindowDefinition> additionalWindows,
        IWebViewAdapterFactory adapterFactory,
        IDesktopApp? desktopApp)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(additionalWindows);
        ArgumentNullException.ThrowIfNull(adapterFactory);

        RuntimeExecution execution;
        lock (_executionGate)
        {
            if (_currentExecution is not null)
                throw new InvalidOperationException("This Win32Runtime instance is already running.");

            execution = new RuntimeExecution(this, adapterFactory, desktopApp);
            _currentExecution = execution;
        }

        try
        {
            execution.OpenMainWindow(options);

            foreach (var window in additionalWindows)
            {
                ArgumentNullException.ThrowIfNull(window);
                execution.OpenAdditionalWindow(window);
            }

            execution.WaitForCompletion();
            execution.ThrowIfFailed();
        }
        finally
        {
            execution.Complete();

            lock (_executionGate)
            {
                if (ReferenceEquals(_currentExecution, execution))
                    _currentExecution = null;
            }
        }
    }

    private void RunWindow(
        HostWindowDefinition definition,
        RuntimeExecution execution)
    {
        if (definition.IsMainWindow)
        {
            _coordinator.RunMainWindow(
                definition.Options,
                execution.WindowManager,
                execution.AdapterFactory,
                _windowFactory,
                execution.DesktopApp);
            return;
        }

        _coordinator.RunAdditionalWindow(
            definition.WindowId,
            definition.Options,
            execution.WindowManager,
            execution.AdapterFactory,
            _windowFactory,
            execution.DesktopApp);
    }

    private Thread CreateWindowThread(
        HostWindowDefinition definition,
        RuntimeExecution execution)
    {
        var thread = new Thread(() =>
        {
            try
            {
                RunWindow(definition, execution);
            }
            catch (Exception ex)
            {
                execution.RecordFailure(ex);
            }
            finally
            {
                execution.OnWindowThreadCompleted(definition.WindowId);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Name = definition.IsMainWindow
            ? "OmniHost-UI"
            : $"OmniHost-UI-{definition.WindowId}";

        return thread;
    }

    private sealed class RuntimeExecution
    {
        private readonly Win32Runtime _runtime;
        private readonly ManualResetEventSlim _allWindowsClosed = new(initialState: true);
        private readonly ConcurrentQueue<Exception> _failures = new();
        private readonly object _gate = new();
        private readonly HashSet<string> _scheduledWindowIds = new(StringComparer.Ordinal);
        private bool _isCompleted;
        private int _activeWindowThreads;

        public RuntimeExecution(
            Win32Runtime runtime,
            IWebViewAdapterFactory adapterFactory,
            IDesktopApp? desktopApp)
        {
            _runtime = runtime;
            AdapterFactory = adapterFactory;
            DesktopApp = desktopApp;
            WindowManager = new RuntimeWindowManager(this);
        }

        public IWebViewAdapterFactory AdapterFactory { get; }

        public IDesktopApp? DesktopApp { get; }

        public IOmniWindowManager WindowManager { get; }

        public void OpenMainWindow(OmniHostOptions options)
            => OpenWindow(new HostWindowDefinition("main", options, IsMainWindow: true));

        public void OpenAdditionalWindow(OmniWindowDefinition window)
        {
            ArgumentNullException.ThrowIfNull(window);
            OpenWindow(new HostWindowDefinition(window.WindowId, window.Options, IsMainWindow: false));
        }

        public void WaitForCompletion() => _allWindowsClosed.Wait();

        public void ThrowIfFailed()
        {
            if (_failures.IsEmpty)
                return;

            var failures = _failures.ToArray();
            if (failures.Length == 1)
                ExceptionDispatchInfo.Capture(failures[0]).Throw();

            throw new AggregateException("One or more OmniHost windows failed.", failures);
        }

        public void Complete()
        {
            lock (_gate)
            {
                _isCompleted = true;
            }
        }

        public void RecordFailure(Exception exception)
            => _failures.Enqueue(exception);

        public void OnWindowThreadCompleted(string windowId)
        {
            lock (_gate)
            {
                _scheduledWindowIds.Remove(windowId);
            }

            if (Interlocked.Decrement(ref _activeWindowThreads) == 0)
                _allWindowsClosed.Set();
        }

        public int OpenWindowCount => _runtime._coordinator.OpenWindowCount;

        public string? MainWindowId => _runtime._coordinator.MainWindowId;

        public IReadOnlyCollection<string> GetOpenWindowIds()
            => _runtime._coordinator.GetOpenWindowIds();

        public IReadOnlyCollection<HostWindowSnapshot> GetOpenWindows()
            => _runtime._coordinator.GetOpenWindows();

        public bool TryCloseWindow(string windowId)
            => _runtime._coordinator.TryRequestClose(windowId);

        private void OpenWindow(HostWindowDefinition definition)
        {
            lock (_gate)
            {
                if (_isCompleted)
                    throw new InvalidOperationException("The runtime is no longer accepting new windows.");

                if (!_scheduledWindowIds.Add(definition.WindowId))
                    throw new InvalidOperationException(
                        $"A host window with id '{definition.WindowId}' is already scheduled or running.");

                if (Interlocked.Increment(ref _activeWindowThreads) == 1)
                    _allWindowsClosed.Reset();
            }

            try
            {
                var thread = _runtime.CreateWindowThread(definition, this);
                thread.Start();
            }
            catch
            {
                OnWindowThreadCompleted(definition.WindowId);
                throw;
            }
        }
    }

    private sealed class RuntimeWindowManager : IOmniWindowManager
    {
        private readonly RuntimeExecution _execution;

        public RuntimeWindowManager(RuntimeExecution execution)
        {
            _execution = execution;
        }

        public int OpenWindowCount => _execution.OpenWindowCount;

        public string? MainWindowId => _execution.MainWindowId;

        public IReadOnlyCollection<string> GetOpenWindowIds()
            => _execution.GetOpenWindowIds();

        public IReadOnlyCollection<HostWindowSnapshot> GetOpenWindows()
            => _execution.GetOpenWindows();

        public void OpenWindow(OmniWindowDefinition window)
            => _execution.OpenAdditionalWindow(window);

        public bool TryCloseWindow(string windowId)
            => _execution.TryCloseWindow(windowId);
    }
}
