using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using AppKit;
using Foundation;

namespace NativeWebHost.Mac;

/// <summary>
/// macOS runtime backed by AppKit windows and the process NSApplication loop.
/// </summary>
public sealed class MacRuntime : IMultiWindowDesktopRuntime
{
    private readonly IHostWindowFactory _windowFactory;
    private readonly HostWindowCoordinator _coordinator;
    private readonly object _executionGate = new();
    private RuntimeExecution? _currentExecution;

    public MacRuntime()
        : this(new MacHostWindowFactory())
    {
    }

    public MacRuntime(IHostWindowFactory windowFactory)
        : this(windowFactory, new HostWindowCoordinator())
    {
    }

    internal MacRuntime(IHostWindowFactory windowFactory, HostWindowCoordinator coordinator)
    {
        _windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public void Run(
        NativeWebHostOptions options,
        IWebViewAdapterFactory adapterFactory,
        IDesktopApp? desktopApp)
        => Run(options, Array.Empty<NativeWebWindowDefinition>(), adapterFactory, desktopApp);

    public void Run(
        NativeWebHostOptions options,
        IReadOnlyList<NativeWebWindowDefinition> additionalWindows,
        IWebViewAdapterFactory adapterFactory,
        IDesktopApp? desktopApp)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(additionalWindows);
        ArgumentNullException.ThrowIfNull(adapterFactory);

        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("MacRuntime is only supported on macOS.");

        if (!NSThread.IsMain)
            throw new InvalidOperationException("MacRuntime must be started on the AppKit main thread.");

        RuntimeExecution execution;
        lock (_executionGate)
        {
            if (_currentExecution is not null)
                throw new InvalidOperationException("This MacRuntime instance is already running.");

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
        private readonly MacRuntime _runtime;
        private readonly ConcurrentQueue<Exception> _failures = new();
        private readonly object _gate = new();
        private readonly HashSet<string> _scheduledWindowIds = new(StringComparer.Ordinal);
        private bool _completed;

        public RuntimeExecution(
            MacRuntime runtime,
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

        public INativeWebWindowManager WindowManager { get; }

        public void Run(
            NativeWebHostOptions options,
            IReadOnlyList<NativeWebWindowDefinition> additionalWindows)
        {
            Exception? capturedException = null;

            void RunOnMainThread()
            {
                try
                {
                    NSApplication.Init();
                    var app = NSApplication.SharedApplication;
                    app.ActivationPolicy = NSApplicationActivationPolicy.Regular;

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
            }

            if (NSThread.IsMain)
            {
                RunOnMainThread();
            }
            else
            {
                using var completed = new ManualResetEventSlim();
                NSApplication.SharedApplication.BeginInvokeOnMainThread(() =>
                {
                    RunOnMainThread();
                    completed.Set();
                });
                completed.Wait();
            }

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

            throw new AggregateException("One or more NativeWebHost macOS windows failed.", failures);
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

        public NativeWebWindowContext? GetWindowContext(string windowId)
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

        public void OpenWindow(NativeWebWindowDefinition window)
        {
            ArgumentNullException.ThrowIfNull(window);
            ScheduleWindowOpen(new HostWindowDefinition(window.WindowId, window.Options, IsMainWindow: false));
        }

        private void ScheduleWindowOpen(HostWindowDefinition definition)
        {
            lock (_gate)
            {
                if (_completed)
                    throw new InvalidOperationException("The macOS runtime is no longer accepting new windows.");

                if (!_scheduledWindowIds.Add(definition.WindowId))
                    throw new InvalidOperationException(
                        $"A host window with id '{definition.WindowId}' is already scheduled or running.");
            }

            NSApplication.SharedApplication.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    OpenWindowCore(definition);
                }
                catch (Exception ex)
                {
                    _failures.Enqueue(ex);

                    lock (_gate)
                    {
                        _scheduledWindowIds.Remove(definition.WindowId);
                    }
                }
            });
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

    private sealed class RuntimeWindowManager : INativeWebWindowManager
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

        public NativeWebWindowContext? GetWindowContext(string windowId)
            => _execution.GetWindowContext(windowId);

        public void OpenWindow(NativeWebWindowDefinition window)
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
