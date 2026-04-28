using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using OmniHost.Windows.Frames;
using OmniHost.Windows.Win32;

namespace OmniHost.Windows;

/// <summary>
/// AOT-compatible raw Win32 window that hosts an <see cref="IWebViewAdapter"/>.
/// No WinForms or WPF dependency — window creation and the message loop are
/// implemented entirely via P/Invoke.
/// </summary>
internal sealed partial class Win32HostWindow : IHostWindow
{
    // ── Class registration ────────────────────────────────────────────────────

    private const string WindowClassName = "OmniHostWindow";
    private const uint TrayIconId = 1;
    private const uint TrayCommandOpen = 1001;
    private const uint TrayCommandExit = 1002;

    /// <summary>
    /// Static WndProc delegate stored in a field to prevent GC collection.
    /// Used for all <see cref="Win32HostWindow"/> instances.
    /// </summary>
    private static readonly NativeMethods.WndProcDelegate _sharedWndProc = StaticWndProc;

    /// <summary>Guard ensuring RegisterClassEx is called at most once per process.</summary>
    private static int _classRegistered;

    // ── Instance state ────────────────────────────────────────────────────────

    private readonly OmniWindowContext _windowContext;
    private readonly OmniHostOptions _options;
    private readonly IWebViewAdapter _adapter;
    private readonly IDesktopApp? _desktopApp;
    private readonly IWin32WindowFrameStrategy _frameStrategy;

    private IntPtr                      _hwnd;
    private Win32SynchronizationContext? _syncContext;
    private Exception?                  _deferredError;
    private int                         _surfaceWidth;
    private int                         _surfaceHeight;
    private string                      _lastWindowState = "unknown";
    private int                         _closeRequested;
    private IntPtr                      _largeIcon;
    private IntPtr                      _smallIcon;
    private bool                        _trayIconCreated;
    private readonly CancellationTokenSource _windowLifetime = new();

    // ── Construction ──────────────────────────────────────────────────────────

    internal Win32HostWindow(
        OmniWindowContext windowContext,
        IDesktopApp? desktopApp,
        IWin32WindowFrameStrategy frameStrategy)
    {
        _windowContext = windowContext ?? throw new ArgumentNullException(nameof(windowContext));
        _options = windowContext.Options;
        _adapter = windowContext.Adapter;
        _desktopApp = desktopApp;
        _frameStrategy = frameStrategy ?? throw new ArgumentNullException(nameof(frameStrategy));
        _surfaceWidth = _options.Width;
        _surfaceHeight = _options.Height;
    }

    /// <inheritdoc/>
    public HostSurfaceDescriptor Surface
        => new(HostSurfaceKind.Hwnd, _hwnd, _surfaceWidth, _surfaceHeight);

    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Creates the native window, runs the Win32 message loop, and returns only
    /// after the window is closed.  Throws if async WebView2 initialization failed.
    /// </summary>
    public void Run()
    {
        // Best-effort per-monitor DPI awareness (requires shcore.dll on Win 8.1+).
        TrySetDpiAwareness();

        RegisterWindowClass();
        _hwnd = CreateNativeWindow();
        _frameStrategy.OnWindowCreated(_hwnd);
        InitializeWindowIcon();
        InitializeTrayIcon();

        // Install the custom sync context BEFORE posting WM_APP_INIT so that
        // 'await' continuations inside InitializeAsync are dispatched here.
        _syncContext = new Win32SynchronizationContext(_hwnd);
        SynchronizationContext.SetSynchronizationContext(_syncContext);

        NativeMethods.ShowWindow(_hwnd,
            _options.StartMaximized ? NativeMethods.SW_SHOWMAXIMIZED : NativeMethods.SW_SHOW);
        NativeMethods.UpdateWindow(_hwnd);

        // Kick off async init from inside the message loop.
        NativeMethods.PostMessageW(_hwnd, NativeMethods.WM_APP_INIT, IntPtr.Zero, IntPtr.Zero);

        // ── Win32 message loop ────────────────────────────────────────────────
        while (NativeMethods.GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessageW(ref msg);
        }

        // Re-throw any exception that was captured during async initialisation.
        if (_deferredError is not null)
            ExceptionDispatchInfo.Capture(_deferredError).Throw();
    }

    /// <inheritdoc/>
    public void RequestClose()
    {
        if (_hwnd == IntPtr.Zero)
            return;

        NativeMethods.PostMessageW(_hwnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    /// <inheritdoc/>
    public void RequestActivate()
    {
        if (_hwnd == IntPtr.Zero)
            return;

        if (NativeMethods.IsIconic(_hwnd))
            NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_RESTORE);
        else
            NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOW);

        NativeMethods.SetForegroundWindow(_hwnd);
    }

    // ── Window class registration ─────────────────────────────────────────────

    private static void RegisterWindowClass()
    {
        if (Interlocked.Exchange(ref _classRegistered, 1) != 0)
            return;

        var hInstance = NativeMethods.GetModuleHandleW(IntPtr.Zero);
        var cursor    = NativeMethods.LoadCursorW(IntPtr.Zero, NativeMethods.IDC_ARROW);
        var wndProc   = Marshal.GetFunctionPointerForDelegate(_sharedWndProc);

        var wc = new NativeMethods.WNDCLASSEXW
        {
            cbSize        = Marshal.SizeOf<NativeMethods.WNDCLASSEXW>(),
            style         = NativeMethods.CS_HREDRAW | NativeMethods.CS_VREDRAW,
            lpfnWndProc   = wndProc,
            hInstance     = hInstance,
            hCursor       = cursor,
            // Use stock brush (1 = COLOR_SCROLLBAR) to avoid a visible white flash
            // before WebView2 paints for the first time.
            hbrBackground = (IntPtr)1,
            lpszClassName = WindowClassName,
        };

        ushort atom = NativeMethods.RegisterClassExW(ref wc);
        if (atom == 0)
        {
            int err = Marshal.GetLastWin32Error();
            // ERROR_CLASS_ALREADY_EXISTS (1410) is harmless in multi-window scenarios.
            if (err != 1410)
                throw new InvalidOperationException($"RegisterClassExW failed with error {err}.");
        }
    }

    // ── Window creation ───────────────────────────────────────────────────────

    private IntPtr CreateNativeWindow()
    {
        var hInstance = NativeMethods.GetModuleHandleW(IntPtr.Zero);

        var (style, exStyle) = _frameStrategy.GetWindowStyles();

        // We pass a GCHandle to 'this' as lpParam so the static WndProc can
        // retrieve the instance during WM_NCCREATE (before CreateWindowExW returns).
        var gcHandle = GCHandle.Alloc(this);

        var hwnd = NativeMethods.CreateWindowExW(
            exStyle,
            WindowClassName, _options.Title,
            style,
            NativeMethods.CW_USEDEFAULT, NativeMethods.CW_USEDEFAULT,
            _options.Width, _options.Height,
            IntPtr.Zero, IntPtr.Zero, hInstance,
            GCHandle.ToIntPtr(gcHandle));     // ← stored in GWLP_USERDATA via WM_NCCREATE

        if (hwnd == IntPtr.Zero)
        {
            gcHandle.Free();
            throw new InvalidOperationException(
                $"CreateWindowExW failed with error {Marshal.GetLastWin32Error()}.");
        }

        // GCHandle is now owned by GWLP_USERDATA; freed in OnDestroy.
        return hwnd;
    }

    // ── Static WndProc ────────────────────────────────────────────────────────

    private static IntPtr StaticWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        // WM_NCCREATE fires inside CreateWindowExW before it returns.
        // lParam is a pointer to CREATESTRUCTW. Its first field (offset 0) is
        // lpCreateParams — the value we passed as the last argument to CreateWindowExW.
        // We store that GCHandle pointer in GWLP_USERDATA so every subsequent message
        // can reach the Win32HostWindow instance.
        if (msg == NativeMethods.WM_NCCREATE)
        {
            // Marshal.ReadIntPtr(lParam, 0) reads CREATESTRUCTW.lpCreateParams,
            // which is the first field at offset 0 of the structure.
            var gcPtr = Marshal.ReadIntPtr(lParam, 0);
            if (gcPtr != IntPtr.Zero)
                NativeMethods.SetWindowLongPtrW(hwnd, NativeMethods.GWLP_USERDATA, gcPtr);
            return NativeMethods.DefWindowProcW(hwnd, msg, wParam, lParam);
        }

        var userData = NativeMethods.GetWindowLongPtrW(hwnd, NativeMethods.GWLP_USERDATA);
        if (userData != IntPtr.Zero && GCHandle.FromIntPtr(userData).Target is Win32HostWindow win)
            return win.InstanceWndProc(hwnd, msg, wParam, lParam);

        return NativeMethods.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    // ── Instance WndProc ──────────────────────────────────────────────────────

    private IntPtr InstanceWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (_frameStrategy.TryHandleMessage(hwnd, msg, wParam, lParam, out var frameResult))
            return frameResult;

        switch (msg)
        {
            case NativeMethods.WM_APP_INIT:
                // Start async WebView2 initialization (fire-and-forget; errors deferred).
                _ = InitializeAsync(hwnd);
                return IntPtr.Zero;

            case NativeMethods.WM_APP_DELEGATE:
                // Drain continuations posted by Win32SynchronizationContext.
                _syncContext?.DrainQueue();
                return IntPtr.Zero;

            case NativeMethods.WM_APP_TRAY:
                OnTrayMessage((uint)lParam);
                return IntPtr.Zero;

            case NativeMethods.WM_SIZE:
                OnResize(wParam, lParam);
                return IntPtr.Zero;

            case NativeMethods.WM_ERASEBKGND:
                // Suppress background erase — WebView2 paints the entire client area.
                return (IntPtr)1;

            case NativeMethods.WM_CLOSE:
                OnClose(hwnd);
                return IntPtr.Zero;

            case NativeMethods.WM_DESTROY:
                OnDestroy(hwnd);
                return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    // ── Async WebView2 initialization ─────────────────────────────────────────

    private async Task InitializeAsync(IntPtr hwnd)
    {
        try
        {
            // Get the initial client size to size the WebView2 controller correctly.
            if (!NativeMethods.GetClientRect(hwnd, out var rc))
                rc = new NativeMethods.RECT { right = _options.Width, bottom = _options.Height };

            _surfaceWidth = rc.right - rc.left;
            _surfaceHeight = rc.bottom - rc.top;

            await _adapter.InitializeAsync(Surface, _options);
            _adapter.Resize(_surfaceWidth, _surfaceHeight);

            RegisterWindowBridgeHandlers();
            await PublishWindowStateChangedIfNeededAsync("initialized");

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
            Console.Error.WriteLine(ex);

            var text =
                $"OmniHost failed to initialize the browser adapter '{_adapter.AdapterId}':\n\n{ex}";

            NativeMethods.MessageBoxW(
                hwnd, text,
                "OmniHost - Initialization Error",
                NativeMethods.MB_OK | NativeMethods.MB_ICONERROR);

            NativeMethods.DestroyWindow(hwnd);
        }
    }

    // ── Frameless window-control bridge handlers ──────────────────────────────

    /// <summary>
    /// Registers JS bridge handlers that let the web content control the native window.
    /// Exposed as <c>omni.window.minimize()</c>, <c>.maximize()</c>,
    /// <c>.close()</c>, <c>.startDrag()</c>, and <c>.showSystemMenu()</c>.
    /// </summary>
    private void RegisterWindowBridgeHandlers()
    {
        var bridge = _adapter.JsBridge;

        bridge.RegisterHandler("window.minimize", _ =>
        {
            NativeMethods.PostMessageW(_hwnd, NativeMethods.WM_SYSCOMMAND,
                (IntPtr)NativeMethods.SC_MINIMIZE, IntPtr.Zero);
            return Task.FromResult("null");
        });

        bridge.RegisterHandler("window.maximize", _ =>
        {
            var cmd = NativeMethods.IsZoomed(_hwnd)
                ? NativeMethods.SC_RESTORE
                : NativeMethods.SC_MAXIMIZE;
            NativeMethods.PostMessageW(_hwnd, NativeMethods.WM_SYSCOMMAND,
                (IntPtr)cmd, IntPtr.Zero);
            return Task.FromResult("null");
        });

        bridge.RegisterHandler("window.close", _ =>
        {
            NativeMethods.PostMessageW(_hwnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            return Task.FromResult("null");
        });

        bridge.RegisterHandler("window.startDrag", _ =>
        {
            NativeMethods.ReleaseCapture();
            NativeMethods.GetCursorPos(out var pt);
            // Construct lParam using the MAKELPARAM convention:
            // low word = x, high word = y (both as WORD / ushort).
            // Using ushort casts preserves negative coordinates via two's complement
            // truncation to 16 bits, matching the MAKELPARAM(x,y) Windows macro.
            var lParam = (IntPtr)unchecked((int)(((uint)(ushort)pt.y << 16) | (uint)(ushort)pt.x));
            NativeMethods.PostMessageW(_hwnd, NativeMethods.WM_NCLBUTTONDOWN,
                (IntPtr)NativeMethods.HTCAPTION, lParam);
            return Task.FromResult("null");
        });

        bridge.RegisterHandler("window.showSystemMenu", _ =>
        {
            ShowSystemMenuAtCursor();
            return Task.FromResult("null");
        });
    }

    private void ShowSystemMenuAtCursor()
    {
        var menu = NativeMethods.GetSystemMenu(_hwnd, false);
        if (menu == IntPtr.Zero)
            return;

        if (!NativeMethods.GetCursorPos(out var pt))
            return;

        NativeMethods.SetForegroundWindow(_hwnd);
        var command = NativeMethods.TrackPopupMenuEx(
            menu,
            NativeMethods.TPM_LEFTALIGN | NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_RIGHTBUTTON,
            pt.x,
            pt.y,
            _hwnd,
            IntPtr.Zero);

        if (command != 0)
        {
            NativeMethods.PostMessageW(
                _hwnd,
                NativeMethods.WM_SYSCOMMAND,
                (IntPtr)command,
                IntPtr.Zero);
        }

        NativeMethods.PostMessageW(_hwnd, NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);
    }

    // ── Message handlers ──────────────────────────────────────────────────────

    private void OnResize(IntPtr wParam, IntPtr lParam)
    {
        var windowState = MapWindowState((uint)wParam);
        if ((uint)wParam != NativeMethods.SIZE_MINIMIZED)
        {
            int w = (int)((uint)lParam & 0xFFFFu);
            int h = (int)((uint)lParam >> 16);

            _surfaceWidth = w;
            _surfaceHeight = h;

            _adapter.Resize(w, h);
        }

        _ = PublishWindowStateChangedIfNeededAsync("wm_size", windowState);
    }

    private async void OnClose(IntPtr hwnd)
    {
        if (Interlocked.Exchange(ref _closeRequested, 1) != 0)
            return;

        try
        {
            _windowLifetime.Cancel();

            await PublishWindowLifecycleEventAsync(
                "window.closing",
                new WindowLifecyclePayload(GetCurrentWindowState(), "wm_close"));

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
        catch { /* ignore errors during shutdown */ }

        NativeMethods.DestroyWindow(hwnd);
    }

    private void OnDestroy(IntPtr hwnd)
    {
        RemoveTrayIcon();
        PublishWindowLifecycleEventAsync(
            "window.closed",
            new WindowLifecyclePayload(GetCurrentWindowState(), "wm_destroy"))
            .GetAwaiter()
            .GetResult();

        // Free the GCHandle stored in GWLP_USERDATA.
        var ptr = NativeMethods.GetWindowLongPtrW(hwnd, NativeMethods.GWLP_USERDATA);
        if (ptr != IntPtr.Zero)
        {
            NativeMethods.SetWindowLongPtrW(hwnd, NativeMethods.GWLP_USERDATA, IntPtr.Zero);
            GCHandle.FromIntPtr(ptr).Free();
        }

        _windowLifetime.Dispose();
        _adapter.DisposeAsync().AsTask().GetAwaiter().GetResult();
        DestroyOwnedIcons();
        NativeMethods.PostQuitMessage(0);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void InitializeWindowIcon()
    {
        _largeIcon = LoadConfiguredIcon(32, 32);
        _smallIcon = LoadConfiguredIcon(16, 16);

        var largeIcon = _largeIcon != IntPtr.Zero
            ? _largeIcon
            : NativeMethods.LoadIconW(IntPtr.Zero, NativeMethods.IDI_APPLICATION);
        var smallIcon = _smallIcon != IntPtr.Zero
            ? _smallIcon
            : NativeMethods.LoadIconW(IntPtr.Zero, NativeMethods.IDI_APPLICATION);

        if (largeIcon != IntPtr.Zero)
            NativeMethods.SendMessageW(_hwnd, NativeMethods.WM_SETICON, (IntPtr)NativeMethods.ICON_BIG, largeIcon);

        if (smallIcon != IntPtr.Zero)
            NativeMethods.SendMessageW(_hwnd, NativeMethods.WM_SETICON, (IntPtr)NativeMethods.ICON_SMALL, smallIcon);
    }

    private void InitializeTrayIcon()
    {
        if (!_windowContext.IsMainWindow || !_options.EnableTrayIcon)
            return;

        var trayIcon = _smallIcon != IntPtr.Zero
            ? _smallIcon
            : (_largeIcon != IntPtr.Zero ? _largeIcon : NativeMethods.LoadIconW(IntPtr.Zero, NativeMethods.IDI_APPLICATION));
        if (trayIcon == IntPtr.Zero)
            return;

        var data = CreateTrayIconData(trayIcon);
        _trayIconCreated = NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_ADD, ref data);
    }

    private NativeMethods.NOTIFYICONDATAW CreateTrayIconData(IntPtr icon)
        => new()
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = TrayIconId,
            uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
            uCallbackMessage = NativeMethods.WM_APP_TRAY,
            hIcon = icon,
            szTip = TrimTrayText(_options.TrayToolTip ?? _options.Title, 127)
        };

    private void OnTrayMessage(uint message)
    {
        if (message is NativeMethods.WM_LBUTTONDBLCLK)
        {
            RequestActivate();
            return;
        }

        if (message is NativeMethods.WM_RBUTTONUP or NativeMethods.WM_CONTEXTMENU)
        {
            ShowTrayMenuAtCursor();
        }
    }

    private void ShowTrayMenuAtCursor()
    {
        var menu = NativeMethods.CreatePopupMenu();
        if (menu == IntPtr.Zero)
            return;

        try
        {
            NativeMethods.AppendMenuW(menu, NativeMethods.MF_STRING, TrayCommandOpen, _options.TrayOpenText);
            NativeMethods.AppendMenuW(menu, NativeMethods.MF_SEPARATOR, 0, null);
            NativeMethods.AppendMenuW(menu, NativeMethods.MF_STRING, TrayCommandExit, _options.TrayExitText);

            if (!NativeMethods.GetCursorPos(out var pt))
                return;

            NativeMethods.SetForegroundWindow(_hwnd);
            var command = NativeMethods.TrackPopupMenuEx(
                menu,
                NativeMethods.TPM_LEFTALIGN | NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_RIGHTBUTTON,
                pt.x,
                pt.y,
                _hwnd,
                IntPtr.Zero);

            if (command == TrayCommandOpen)
            {
                RequestActivate();
            }
            else if (command == TrayCommandExit)
            {
                NativeMethods.PostMessageW(_hwnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }

            NativeMethods.PostMessageW(_hwnd, NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);
        }
        finally
        {
            NativeMethods.DestroyMenu(menu);
        }
    }

    private void RemoveTrayIcon()
    {
        if (!_trayIconCreated)
            return;

        var data = new NativeMethods.NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = TrayIconId
        };
        NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_DELETE, ref data);
        _trayIconCreated = false;
    }

    private IntPtr LoadConfiguredIcon(int width, int height)
    {
        if (string.IsNullOrWhiteSpace(_options.IconPath) || !File.Exists(_options.IconPath))
            return IntPtr.Zero;

        return NativeMethods.LoadImageW(
            IntPtr.Zero,
            _options.IconPath,
            NativeMethods.IMAGE_ICON,
            width,
            height,
            NativeMethods.LR_LOADFROMFILE | NativeMethods.LR_DEFAULTSIZE);
    }

    private void DestroyOwnedIcons()
    {
        if (_largeIcon != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_largeIcon);
            _largeIcon = IntPtr.Zero;
        }

        if (_smallIcon != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_smallIcon);
            _smallIcon = IntPtr.Zero;
        }
    }

    private static string TrimTrayText(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static void TrySetDpiAwareness()
    {
        try { NativeMethods.SetProcessDpiAwareness(2 /* PROCESS_PER_MONITOR_DPI_AWARE */); }
        catch { /* shcore.dll may be absent on very old Windows versions */ }
    }

    private string GetCurrentWindowState()
    {
        if (_hwnd != IntPtr.Zero)
        {
            if (NativeMethods.IsIconic(_hwnd))
                return "minimized";

            if (NativeMethods.IsZoomed(_hwnd))
                return "maximized";
        }

        return "normal";
    }

    private static string MapWindowState(uint sizeCode)
        => sizeCode switch
        {
            NativeMethods.SIZE_MINIMIZED => "minimized",
            NativeMethods.SIZE_MAXIMIZED => "maximized",
            _ => "normal",
        };

    private async Task PublishWindowStateChangedIfNeededAsync(string reason, string? windowState = null)
    {
        var nextState = windowState ?? GetCurrentWindowState();
        if (string.Equals(_lastWindowState, nextState, StringComparison.Ordinal))
            return;

        _lastWindowState = nextState;
        await PublishWindowLifecycleEventAsync(
            "window.stateChanged",
            new WindowLifecyclePayload(
                nextState,
                reason,
                string.Equals(nextState, "minimized", StringComparison.Ordinal),
                string.Equals(nextState, "maximized", StringComparison.Ordinal),
                _surfaceWidth,
                _surfaceHeight));
    }

    private async Task PublishWindowLifecycleEventAsync(string eventName, WindowLifecyclePayload payload)
    {
        try
        {
            await _adapter.JsBridge.PostMessageAsync(
                eventName,
                JsonSerializer.Serialize(payload, Win32HostWindowJsonContext.Default.WindowLifecyclePayload));
        }
        catch (ObjectDisposedException)
        {
            // Ignore late shutdown publishes once the underlying browser resources are gone.
        }
        catch (InvalidOperationException)
        {
            // Ignore lifecycle publishes before the bridge is initialized or after teardown starts.
        }
    }

    private sealed record WindowLifecyclePayload(
        [property: JsonPropertyName("state")] string State,
        [property: JsonPropertyName("reason")] string Reason,
        [property: JsonPropertyName("isMinimized")] bool? IsMinimized = null,
        [property: JsonPropertyName("isMaximized")] bool? IsMaximized = null,
        [property: JsonPropertyName("width")] int? Width = null,
        [property: JsonPropertyName("height")] int? Height = null);

    [JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(WindowLifecyclePayload))]
    private sealed partial class Win32HostWindowJsonContext : JsonSerializerContext;
}
