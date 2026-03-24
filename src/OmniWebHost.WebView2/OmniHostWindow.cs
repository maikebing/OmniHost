using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using OmniWebHost.WebView2.Win32;

namespace OmniWebHost.WebView2;

/// <summary>
/// AOT-compatible raw Win32 window that hosts a <see cref="WebView2Adapter"/>.
/// No WinForms or WPF dependency — window creation and the message loop are
/// implemented entirely via P/Invoke.
/// </summary>
internal sealed class OmniHostWindow
{
    // ── Class registration ────────────────────────────────────────────────────

    private const string WindowClassName = "OmniWebHostWindow";

    /// <summary>
    /// Static WndProc delegate stored in a field to prevent GC collection.
    /// Used for all <see cref="OmniHostWindow"/> instances.
    /// </summary>
    private static readonly NativeMethods.WndProcDelegate _sharedWndProc = StaticWndProc;

    /// <summary>Guard ensuring RegisterClassEx is called at most once per process.</summary>
    private static int _classRegistered;

    // ── Instance state ────────────────────────────────────────────────────────

    private readonly OmniWebHostOptions _options;
    private readonly IWebViewAdapter    _adapter;
    private readonly IDesktopApp?       _desktopApp;

    private IntPtr                      _hwnd;
    private Win32SynchronizationContext? _syncContext;
    private Exception?                  _deferredError;

    // ── Construction ──────────────────────────────────────────────────────────

    internal OmniHostWindow(OmniWebHostOptions options, IWebViewAdapter adapter, IDesktopApp? desktopApp)
    {
        _options    = options;
        _adapter    = adapter;
        _desktopApp = desktopApp;
    }

    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Creates the native window, runs the Win32 message loop, and returns only
    /// after the window is closed.  Throws if async WebView2 initialization failed.
    /// </summary>
    internal void Run()
    {
        // Best-effort per-monitor DPI awareness (requires shcore.dll on Win 8.1+).
        TrySetDpiAwareness();

        RegisterWindowClass();
        _hwnd = CreateNativeWindow();
        ApplyCustomFrame(_hwnd);

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

        (uint style, uint exStyle) = _options.WindowStyle == OmniWindowStyle.Frameless
            ? (NativeMethods.WS_OVERLAPPEDWINDOW
               | NativeMethods.WS_CLIPCHILDREN | NativeMethods.WS_CLIPSIBLINGS,
               NativeMethods.WS_EX_APPWINDOW)
            : (NativeMethods.WS_OVERLAPPEDWINDOW
               | NativeMethods.WS_CLIPCHILDREN | NativeMethods.WS_CLIPSIBLINGS,
               0u);

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
        // can reach the OmniHostWindow instance.
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
        if (userData != IntPtr.Zero && GCHandle.FromIntPtr(userData).Target is OmniHostWindow win)
            return win.InstanceWndProc(hwnd, msg, wParam, lParam);

        return NativeMethods.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    // ── Instance WndProc ──────────────────────────────────────────────────────

    private IntPtr InstanceWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (TryHandleDwmNonClientMessage(hwnd, msg, wParam, lParam, out var dwmResult))
            return dwmResult;

        switch (msg)
        {
            case NativeMethods.WM_CREATE:
                ApplyCustomFrame(hwnd);
                return IntPtr.Zero;

            case NativeMethods.WM_APP_INIT:
                // Start async WebView2 initialization (fire-and-forget; errors deferred).
                _ = InitializeAsync(hwnd);
                return IntPtr.Zero;

            case NativeMethods.WM_APP_DELEGATE:
                // Drain continuations posted by Win32SynchronizationContext.
                _syncContext?.DrainQueue();
                return IntPtr.Zero;

            case NativeMethods.WM_SIZE:
                OnResize(wParam, lParam);
                return IntPtr.Zero;

            case NativeMethods.WM_ERASEBKGND:
                // Suppress background erase — WebView2 paints the entire client area.
                return (IntPtr)1;

            case NativeMethods.WM_NCCALCSIZE when _options.WindowStyle == OmniWindowStyle.Frameless:
                if (IsDwmCompositionEnabled())
                    return IntPtr.Zero;
                break;

            case NativeMethods.WM_NCHITTEST when _options.WindowStyle == OmniWindowStyle.Frameless:
                return FramelessHitTest(hwnd, lParam);

            case NativeMethods.WM_ACTIVATE when _options.WindowStyle == OmniWindowStyle.Frameless:
            case NativeMethods.WM_DWMCOMPOSITIONCHANGED when _options.WindowStyle == OmniWindowStyle.Frameless:
                ApplyCustomFrame(hwnd);
                break;

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

            await _adapter.InitializeAsync(hwnd, _options);

            if (_adapter is WebView2Adapter wv2)
                wv2.Resize(rc.right - rc.left, rc.bottom - rc.top);

            RegisterWindowBridgeHandlers();

            if (_desktopApp is not null)
                await _desktopApp.OnStartAsync(_adapter);

            await _adapter.NavigateAsync(_options.StartUrl);
        }
        catch (Exception ex)
        {
            _deferredError = ex;

            var text =
                $"OmniWebHost failed to initialize WebView2:\n\n{ex.Message}\n\n" +
                "Make sure the Microsoft Edge WebView2 Runtime is installed.\n" +
                "Download: https://developer.microsoft.com/microsoft-edge/webview2/";

            NativeMethods.MessageBoxW(
                hwnd, text,
                "OmniWebHost – Initialization Error",
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

    private bool TryHandleDwmNonClientMessage(
        IntPtr hwnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        out IntPtr result)
    {
        result = IntPtr.Zero;

        if (_options.WindowStyle != OmniWindowStyle.Frameless || !IsDwmCompositionEnabled())
            return false;

        return NativeMethods.DwmDefWindowProc(hwnd, msg, wParam, lParam, out result) == 0
            && result != IntPtr.Zero;
    }

    private bool IsDwmCompositionEnabled()
    {
        try
        {
            return NativeMethods.DwmIsCompositionEnabled(out var enabled) == 0 && enabled;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyCustomFrame(IntPtr hwnd)
    {
        if (_options.WindowStyle != OmniWindowStyle.Frameless || !IsDwmCompositionEnabled())
            return;

        var margins = new NativeMethods.MARGINS
        {
            cxLeftWidth = 0,
            cxRightWidth = 0,
            cyTopHeight = 1,
            cyBottomHeight = 0,
        };

        NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);
        NativeMethods.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOMOVE
            | NativeMethods.SWP_NOSIZE
            | NativeMethods.SWP_NOZORDER
            | NativeMethods.SWP_NOACTIVATE
            | NativeMethods.SWP_FRAMECHANGED);
    }

    private void OnResize(IntPtr wParam, IntPtr lParam)
    {
        if ((uint)wParam == NativeMethods.SIZE_MINIMIZED) return;

        int w = (int)((uint)lParam & 0xFFFFu);
        int h = (int)((uint)lParam >> 16);

        if (_adapter is WebView2Adapter wv2)
            wv2.Resize(w, h);
    }

    private async void OnClose(IntPtr hwnd)
    {
        try
        {
            if (_desktopApp is not null)
                await _desktopApp.OnClosingAsync();
        }
        catch { /* ignore errors during shutdown */ }
        NativeMethods.DestroyWindow(hwnd);
    }

    private void OnDestroy(IntPtr hwnd)
    {
        // Free the GCHandle stored in GWLP_USERDATA.
        var ptr = NativeMethods.GetWindowLongPtrW(hwnd, NativeMethods.GWLP_USERDATA);
        if (ptr != IntPtr.Zero)
        {
            NativeMethods.SetWindowLongPtrW(hwnd, NativeMethods.GWLP_USERDATA, IntPtr.Zero);
            GCHandle.FromIntPtr(ptr).Free();
        }

        _adapter.DisposeAsync().AsTask().GetAwaiter().GetResult();
        NativeMethods.PostQuitMessage(0);
    }

    // ── Frameless hit-test ────────────────────────────────────────────────────

    /// <summary>
    /// Returns edge/corner hit-test values for the resize border of a frameless window.
    /// Uses <c>short</c> casts for correct sign extension on negative-coordinate monitors.
    /// </summary>
    private static IntPtr FramelessHitTest(IntPtr hwnd, IntPtr lParam)
    {
        if (!NativeMethods.GetWindowRect(hwnd, out var wr))
            return (IntPtr)NativeMethods.HTCLIENT;

        // GET_X_LPARAM / GET_Y_LPARAM (screen coordinates, may be negative).
        int x = (short)((uint)lParam & 0xFFFFu);
        int y = (short)((uint)lParam >> 16);

        bool left   = x < wr.left   + NativeMethods.ResizeBorderSize;
        bool right  = x >= wr.right  - NativeMethods.ResizeBorderSize;
        bool top    = y < wr.top    + NativeMethods.ResizeBorderSize;
        bool bottom = y >= wr.bottom - NativeMethods.ResizeBorderSize;

        if (top    && left)  return (IntPtr)NativeMethods.HTTOPLEFT;
        if (top    && right) return (IntPtr)NativeMethods.HTTOPRIGHT;
        if (bottom && left)  return (IntPtr)NativeMethods.HTBOTTOMLEFT;
        if (bottom && right) return (IntPtr)NativeMethods.HTBOTTOMRIGHT;
        if (top)             return (IntPtr)NativeMethods.HTTOP;
        if (bottom)          return (IntPtr)NativeMethods.HTBOTTOM;
        if (left)            return (IntPtr)NativeMethods.HTLEFT;
        if (right)           return (IntPtr)NativeMethods.HTRIGHT;

        return (IntPtr)NativeMethods.HTCLIENT;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void TrySetDpiAwareness()
    {
        try { NativeMethods.SetProcessDpiAwareness(2 /* PROCESS_PER_MONITOR_DPI_AWARE */); }
        catch { /* shcore.dll may be absent on very old Windows versions */ }
    }
}
