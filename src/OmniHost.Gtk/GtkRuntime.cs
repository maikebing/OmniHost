using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using OmniHost.Core;
using OmniHost.Gtk.Gtk;

namespace OmniHost.Gtk;

/// <summary>
/// First-pass Linux runtime backed by GTK host windows.
/// </summary>
/// <remarks>
/// This package currently focuses on native window hosting and runtime/window-manager
/// flow. A Linux browser adapter still needs to be paired with it for an end-to-end path.
/// </remarks>
public sealed class GtkRuntime : IMultiWindowDesktopRuntime
{
    private readonly IHostWindowFactory _windowFactory;
    private readonly HostWindowCoordinator _coordinator;
    private readonly object _executionGate = new();
    private RuntimeExecution? _currentExecution;

    public GtkRuntime()
        : this(new GtkHostWindowFactory())
    {
    }

    public GtkRuntime(IHostWindowFactory windowFactory)
        : this(windowFactory, new HostWindowCoordinator())
    {
    }

    internal GtkRuntime(IHostWindowFactory windowFactory, HostWindowCoordinator coordinator)
    {
        _windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public void Run(
        OmniHostOptions options,
        IWebViewAdapterFactory adapterFactory,
        IDesktopApp? desktopApp)
        => Run(options, Array.Empty<OmniWindowDefinition>(), adapterFactory, desktopApp);

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
                throw new InvalidOperationException("This GtkRuntime instance is already running.");

            execution = new RuntimeExecution(this, adapterFactory, desktopApp);
            _currentExecution = execution;
        }

        try
        {
            execution.Run(options, additionalWindows);
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

    private void RunWindow(HostWindowDefinition definition, RuntimeExecution execution)
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

    private sealed class RuntimeExecution
    {
        private readonly GtkRuntime _runtime;
        private readonly ConcurrentQueue<Exception> _failures = new();
        private readonly object _gate = new();
        private readonly HashSet<string> _scheduledWindowIds = new(StringComparer.Ordinal);
        private SynchronizationContext? _uiContext;
        private bool _completed;

        public RuntimeExecution(
            GtkRuntime runtime,
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

        public void Run(OmniHostOptions options, IReadOnlyList<OmniWindowDefinition> additionalWindows)
        {
            Exception? capturedException = null;

            var uiThread = new Thread(() =>
            {
                try
                {
                    _uiContext = new GtkSynchronizationContext();
                    SynchronizationContext.SetSynchronizationContext(_uiContext);

                    foreach (var window in additionalWindows)
                    {
                        ArgumentNullException.ThrowIfNull(window);
                        ScheduleWindowOpen(new HostWindowDefinition(window.WindowId, window.Options, IsMainWindow: false));
                    }

                    OpenWindowCore(new HostWindowDefinition("main", options, IsMainWindow: true));
                }
                catch (Exception ex)
                {
                    capturedException = ex;
                }
            });

            uiThread.Name = "OmniHost-Gtk-UI";
            uiThread.Start();
            uiThread.Join();

            if (capturedException is not null)
                ExceptionDispatchInfo.Capture(capturedException).Throw();
        }

        public void ThrowIfFailed()
        {
            if (_failures.IsEmpty)
                return;

            var failures = _failures.ToArray();
            if (failures.Length == 1)
                ExceptionDispatchInfo.Capture(failures[0]).Throw();

            throw new AggregateException("One or more OmniHost GTK windows failed.", failures);
        }

        public void Complete()
        {
            lock (_gate)
            {
                _completed = true;
            }
        }

        public int OpenWindowCount => _runtime._coordinator.OpenWindowCount;

        public string? MainWindowId => _runtime._coordinator.MainWindowId;

        public IReadOnlyCollection<string> GetOpenWindowIds()
            => _runtime._coordinator.GetOpenWindowIds();

        public IReadOnlyCollection<HostWindowSnapshot> GetOpenWindows()
            => _runtime._coordinator.GetOpenWindows();

        public OmniWindowContext? GetWindowContext(string windowId)
            => _runtime._coordinator.GetWindowContext(windowId);

        public bool TryCloseWindow(string windowId)
            => _runtime._coordinator.TryRequestClose(windowId);

        public bool TryActivateWindow(string windowId)
            => _runtime._coordinator.TryRequestActivate(windowId);

        public Task<bool> PostEventAsync(
            string windowId,
            string eventName,
            string jsonPayload,
            CancellationToken cancellationToken = default)
            => _runtime._coordinator.PostEventAsync(windowId, eventName, jsonPayload, cancellationToken);

        public Task<int> BroadcastEventAsync(
            string eventName,
            string jsonPayload,
            CancellationToken cancellationToken = default)
            => _runtime._coordinator.BroadcastEventAsync(eventName, jsonPayload, cancellationToken);

        public void OpenWindow(OmniWindowDefinition window)
        {
            ArgumentNullException.ThrowIfNull(window);
            ScheduleWindowOpen(new HostWindowDefinition(window.WindowId, window.Options, IsMainWindow: false));
        }

        private void ScheduleWindowOpen(HostWindowDefinition definition)
        {
            lock (_gate)
            {
                if (_completed)
                    throw new InvalidOperationException("The GTK runtime is no longer accepting new windows.");

                if (!_scheduledWindowIds.Add(definition.WindowId))
                    throw new InvalidOperationException(
                        $"A host window with id '{definition.WindowId}' is already scheduled or running.");
            }

            if (_uiContext is null)
                throw new InvalidOperationException("The GTK UI thread is not ready yet.");

            _uiContext.Post(static state =>
            {
                var (execution, scheduledDefinition) = ((RuntimeExecution, HostWindowDefinition))state!;

                try
                {
                    execution.OpenWindowCore(scheduledDefinition);
                }
                catch (Exception ex)
                {
                    execution._failures.Enqueue(ex);

                    lock (execution._gate)
                    {
                        execution._scheduledWindowIds.Remove(scheduledDefinition.WindowId);
                    }
                }
            }, (this, definition));
        }

        private void OpenWindowCore(HostWindowDefinition definition)
        {
            try
            {
                _runtime.RunWindow(definition, this);
            }
            finally
            {
                lock (_gate)
                {
                    _scheduledWindowIds.Remove(definition.WindowId);
                }
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

        public OmniWindowContext? GetWindowContext(string windowId)
            => _execution.GetWindowContext(windowId);

        public void OpenWindow(OmniWindowDefinition window)
            => _execution.OpenWindow(window);

        public bool TryCloseWindow(string windowId)
            => _execution.TryCloseWindow(windowId);

        public bool TryActivateWindow(string windowId)
            => _execution.TryActivateWindow(windowId);

        public Task<bool> PostEventAsync(
            string windowId,
            string eventName,
            string jsonPayload,
            CancellationToken cancellationToken = default)
            => _execution.PostEventAsync(windowId, eventName, jsonPayload, cancellationToken);

        public Task<int> BroadcastEventAsync(
            string eventName,
            string jsonPayload,
            CancellationToken cancellationToken = default)
            => _execution.BroadcastEventAsync(eventName, jsonPayload, cancellationToken);
    }
}
