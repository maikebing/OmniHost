using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using OmniHost.Gtk.Gtk;

namespace OmniHost.Gtk;

internal sealed class GtkHostWindow : IHostWindow
{
    private readonly OmniWindowContext _windowContext;
    private readonly OmniHostOptions _options;
    private readonly IWebViewAdapter _adapter;
    private readonly IDesktopApp? _desktopApp;
    private readonly CancellationTokenSource _windowLifetime = new();

    private IntPtr _windowHandle;
    private IntPtr _hostContainerHandle;
    private IntPtr _runLoop;
    private Exception? _deferredError;
    private int _surfaceWidth;
    private int _surfaceHeight;
    private string _lastWindowState = "unknown";
    private int _closeRequested;
    private GtkSynchronizationContext? _syncContext;

    private GtkNative.DeleteEventCallback? _deleteEventCallback;
    private GtkNative.DestroyCallback? _destroyCallback;
    private GtkNative.SizeAllocateCallback? _sizeAllocateCallback;
    private GtkNative.IdleCallback? _initializeIdleCallback;

    internal GtkHostWindow(OmniWindowContext windowContext, IDesktopApp? desktopApp)
    {
        _windowContext = windowContext ?? throw new ArgumentNullException(nameof(windowContext));
        _options = windowContext.Options;
        _adapter = windowContext.Adapter;
        _desktopApp = desktopApp;
        _surfaceWidth = _options.Width;
        _surfaceHeight = _options.Height;
    }

    public HostSurfaceDescriptor Surface
        => new(HostSurfaceKind.GtkWidget, _hostContainerHandle, _surfaceWidth, _surfaceHeight);

    public void Run()
    {
        EnsureLinux();
        EnsureGtkInitialized();

        _syncContext = new GtkSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(_syncContext);

        _windowHandle = GtkNative.GtkWindowNew(GtkNative.GtkWindowToplevel);
        if (_windowHandle == IntPtr.Zero)
            throw new InvalidOperationException("gtk_window_new returned a null window handle.");

        _hostContainerHandle = GtkNative.GtkFixedNew();
        if (_hostContainerHandle == IntPtr.Zero)
            throw new InvalidOperationException("gtk_fixed_new returned a null widget handle.");

        GtkNative.GtkContainerAdd(_windowHandle, _hostContainerHandle);
        GtkNative.GtkWindowSetTitle(_windowHandle, _options.Title);
        GtkNative.GtkWindowSetDefaultSize(_windowHandle, _options.Width, _options.Height);

        if (_options.StartMaximized)
            GtkNative.GtkWindowMaximize(_windowHandle);

        HookSignals();

        _runLoop = GtkNative.GMainLoopNew(IntPtr.Zero, isRunning: false);
        if (_runLoop == IntPtr.Zero)
            throw new InvalidOperationException("g_main_loop_new returned a null loop handle.");

        ScheduleInitialization();
        GtkNative.GtkWidgetShowAll(_windowHandle);
        GtkNative.GMainLoopRun(_runLoop);
        GtkNative.GMainLoopUnref(_runLoop);
        _runLoop = IntPtr.Zero;

        if (_deferredError is not null)
            ExceptionDispatchInfo.Capture(_deferredError).Throw();
    }

    public void RequestClose()
    {
        if (_windowHandle == IntPtr.Zero)
            return;

        PostToUiThread(static state =>
        {
            var window = (GtkHostWindow)state!;
            _ = window.HandleCloseRequestedAsync("request_close");
        }, this);
    }

    public void RequestActivate()
    {
        if (_windowHandle == IntPtr.Zero)
            return;

        PostToUiThread(static state =>
        {
            var window = (GtkHostWindow)state!;
            GtkNative.GtkWindowPresent(window._windowHandle);
        }, this);
    }

    private void HookSignals()
    {
        _deleteEventCallback = OnDeleteEvent;
        _destroyCallback = OnDestroy;
        _sizeAllocateCallback = OnSizeAllocate;

        GtkNative.GSignalConnectData(
            _windowHandle,
            "delete-event",
            Marshal.GetFunctionPointerForDelegate(_deleteEventCallback),
            IntPtr.Zero,
            IntPtr.Zero,
            0);

        GtkNative.GSignalConnectData(
            _windowHandle,
            "destroy",
            Marshal.GetFunctionPointerForDelegate(_destroyCallback),
            IntPtr.Zero,
            IntPtr.Zero,
            0);

        GtkNative.GSignalConnectData(
            _hostContainerHandle,
            "size-allocate",
            Marshal.GetFunctionPointerForDelegate(_sizeAllocateCallback),
            IntPtr.Zero,
            IntPtr.Zero,
            0);
    }

    private void ScheduleInitialization()
    {
        _initializeIdleCallback = _unused =>
        {
            _ = InitializeAsync();
            return 0;
        };

        GtkNative.GIdleAdd(
            Marshal.GetFunctionPointerForDelegate(_initializeIdleCallback),
            IntPtr.Zero);
    }

    private async Task InitializeAsync()
    {
        try
        {
            await _adapter.InitializeAsync(Surface, _options);
            _adapter.Resize(_surfaceWidth, _surfaceHeight);
            RegisterWindowBridgeHandlers();

            await PublishWindowStateChangedIfNeededAsync(
                "initialized",
                _options.StartMaximized ? "maximized" : "normal");

            if (_desktopApp is IWindowAwareDesktopApp windowAwareDesktopApp)
            {
                await windowAwareDesktopApp.OnWindowStartAsync(
                    _windowContext,
                    _windowLifetime.Token);
            }
            else if (_desktopApp is not null)
            {
                await _desktopApp.OnStartAsync(_adapter, _windowLifetime.Token);
            }

            await _adapter.NavigateAsync(_options.StartUrl);
        }
        catch (Exception ex)
        {
            _deferredError = ex;
            RequestClose();
        }
    }

    private void RegisterWindowBridgeHandlers()
    {
        var bridge = _adapter.JsBridge;

        bridge.RegisterHandler("window.minimize", _payload =>
        {
            if (_windowHandle != IntPtr.Zero)
            {
                GtkNative.GtkWindowIconify(_windowHandle);
                _ = PublishWindowStateChangedIfNeededAsync("bridge_minimize", "minimized");
            }

            return Task.FromResult("null");
        });

        bridge.RegisterHandler("window.maximize", _payload =>
        {
            if (_windowHandle != IntPtr.Zero)
            {
                var shouldRestore = string.Equals(_lastWindowState, "maximized", StringComparison.Ordinal);
                if (shouldRestore)
                {
                    GtkNative.GtkWindowUnmaximize(_windowHandle);
                    _ = PublishWindowStateChangedIfNeededAsync("bridge_restore", "normal");
                }
                else
                {
                    GtkNative.GtkWindowMaximize(_windowHandle);
                    _ = PublishWindowStateChangedIfNeededAsync("bridge_maximize", "maximized");
                }
            }

            return Task.FromResult("null");
        });

        bridge.RegisterHandler("window.close", _payload =>
        {
            RequestClose();
            return Task.FromResult("null");
        });

        bridge.RegisterHandler("window.startDrag", _payload => Task.FromResult("null"));
        bridge.RegisterHandler("window.showSystemMenu", _payload => Task.FromResult("null"));
    }

    private int OnDeleteEvent(IntPtr widget, IntPtr eventData, IntPtr userData)
    {
        _ = HandleCloseRequestedAsync("delete_event");
        return 1;
    }

    private async Task HandleCloseRequestedAsync(string reason)
    {
        if (Interlocked.Exchange(ref _closeRequested, 1) != 0)
            return;

        try
        {
            _windowLifetime.Cancel();

            await PublishWindowLifecycleEventAsync("window.closing", new
            {
                windowId = _windowContext.WindowId,
                state = "normal",
                reason,
            });

            if (_desktopApp is IWindowAwareDesktopApp windowAwareDesktopApp)
            {
                await windowAwareDesktopApp.OnWindowClosingAsync(
                    _windowContext,
                    _windowLifetime.Token);
            }
            else if (_desktopApp is not null)
            {
                await _desktopApp.OnClosingAsync();
            }
        }
        catch
        {
        }

        if (_windowHandle != IntPtr.Zero)
            GtkNative.GtkWidgetDestroy(_windowHandle);
    }

    private void OnDestroy(IntPtr widget, IntPtr userData)
    {
        PublishWindowLifecycleEventAsync("window.closed", new
        {
            windowId = _windowContext.WindowId,
            state = "closed",
            reason = "destroy",
        }).GetAwaiter().GetResult();

        _windowLifetime.Dispose();
        _adapter.DisposeAsync().AsTask().GetAwaiter().GetResult();

        if (_runLoop != IntPtr.Zero)
            GtkNative.GMainLoopQuit(_runLoop);

        _windowHandle = IntPtr.Zero;
        _hostContainerHandle = IntPtr.Zero;
    }

    private void OnSizeAllocate(IntPtr widget, IntPtr allocation, IntPtr userData)
    {
        var size = Marshal.PtrToStructure<GtkNative.GtkAllocation>(allocation);
        _surfaceWidth = size.width;
        _surfaceHeight = size.height;

        _adapter.Resize(_surfaceWidth, _surfaceHeight);
        _ = PublishWindowStateChangedIfNeededAsync("size_allocate", "normal");
    }

    private async Task PublishWindowStateChangedIfNeededAsync(string reason, string windowState)
    {
        if (string.Equals(_lastWindowState, windowState, StringComparison.Ordinal))
            return;

        _lastWindowState = windowState;

        await PublishWindowLifecycleEventAsync("window.stateChanged", new
        {
            windowId = _windowContext.WindowId,
            state = windowState,
            isMinimized = false,
            isMaximized = false,
            width = _surfaceWidth,
            height = _surfaceHeight,
            reason,
        });
    }

    private async Task PublishWindowLifecycleEventAsync(string eventName, object payload)
    {
        try
        {
            await _adapter.JsBridge.PostMessageAsync(eventName, JsonSerializer.Serialize(payload));
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void PostToUiThread(SendOrPostCallback callback, object state)
    {
        if (_syncContext is null || SynchronizationContext.Current == _syncContext)
        {
            callback(state);
            return;
        }

        _syncContext.Post(callback, state);
    }

    private static void EnsureLinux()
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("GtkRuntime is only supported on Linux.");
    }

    private static int _initialized;

    private static void EnsureGtkInitialized()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
            return;

        int argc = 0;
        IntPtr argv = IntPtr.Zero;

        if (!GtkNative.GtkInitCheck(ref argc, ref argv))
            throw new InvalidOperationException(
                "gtk_init_check failed. Ensure GTK 3 is installed and a display is available.");
    }
}
