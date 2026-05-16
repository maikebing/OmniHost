using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using DirectN;
using DirectN.Extensions.Com;
using WebView2;
using WebView2.Utilities;

namespace OmniHost.NativeWebView2;

/// <summary>
/// 基于 WebView2Aot generated COM binding 的 WebView2 适配器。
/// </summary>
public sealed class NativeWebView2Adapter : IWebViewAdapter
{
    private readonly NativeWebView2JsBridge _bridge = new();
    private ComObject<ICoreWebView2Environment>? _environment;
    private ComObject<ICoreWebView2Controller>? _controller;
    private ComObject<ICoreWebView2>? _webView;
    private CoreWebView2NavigationStartingEventHandler? _navigationStartingHandler;
    private CoreWebView2NavigationCompletedEventHandler? _navigationCompletedHandler;
    private EventRegistrationToken _navigationStartingToken;
    private EventRegistrationToken _navigationCompletedToken;
    private BrowserCapabilities _capabilities = new()
    {
        EngineName = "Native WebView2",
        EngineVersion = "unknown",
        SupportsJavaScript = true,
        SupportsJsBridge = true,
        SupportsCustomSchemes = false,
        SupportsDevTools = true,
        SupportedHostSurfaces = new[] { HostSurfaceKind.Hwnd },
    };

    public string AdapterId => "native-webview2";

    public BrowserCapabilities Capabilities => _capabilities;

    public IJsBridge JsBridge => _bridge;

    public async Task InitializeAsync(
        HostSurfaceDescriptor surface,
        OmniHostOptions options,
        CancellationToken cancellationToken = default)
    {
        if (surface.Kind != HostSurfaceKind.Hwnd)
            throw new NotSupportedException(
                $"NativeWebView2Adapter only supports {HostSurfaceKind.Hwnd} host surfaces.");

        if (!surface.IsCreated)
            throw new InvalidOperationException(
                "The supplied host surface has not been created yet.");

        WebView2Utilities.Initialize(typeof(NativeWebView2Adapter).Assembly);

        var browserVersion = WebView2Utilities.GetAvailableCoreWebView2BrowserVersionString();
        if (browserVersion is null)
            throw new InvalidOperationException("WebView2 Runtime is not installed.");

        var userDataFolder = options.UserDataFolder
            ?? Path.Combine(Path.GetTempPath(), "OmniHost", SanitizeFolderName(options.Title));

        _environment = await CreateEnvironmentAsync(userDataFolder, cancellationToken);
        _controller = await CreateControllerAsync(_environment.Object, surface.Handle, cancellationToken);
        _controller.Object.put_IsVisible(BOOL.TRUE).ThrowOnError();
        _controller.Object.get_CoreWebView2(out var core).ThrowOnError();
        _webView = new ComObject<ICoreWebView2>(core);

        _capabilities = new BrowserCapabilities
        {
            EngineName = "Native WebView2",
            EngineVersion = browserVersion,
            SupportsJavaScript = true,
            SupportsJsBridge = true,
            SupportsCustomSchemes = false,
            SupportsDevTools = true,
            SupportedHostSurfaces = new[] { HostSurfaceKind.Hwnd },
        };

        ConfigureSettings(_webView.Object, options);
        RegisterNavigationReadyTracking(_webView.Object);
        await _bridge.InitializeAsync(_webView.Object, cancellationToken);
        await InjectWindowChromeSupportAsync(_webView.Object, options, cancellationToken);
        await InjectHostCssAsync(_webView.Object, options, cancellationToken);
    }

    public async Task NavigateAsync(string url, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventRegistrationToken token = default;

        using var registration = cancellationToken.Register(
            static state => ((TaskCompletionSource)state!).TrySetCanceled(),
            completion);

        var handler = new CoreWebView2NavigationCompletedEventHandler((_, _) =>
        {
            // 首屏显示只需要等主文档完成一次导航；成功和失败都交给页面自身或 WebView2 错误页呈现。
            completion.TrySetResult();
        });

        _webView!.Object.add_NavigationCompleted(handler, ref token).ThrowOnError();

        try
        {
            _webView.Object.Navigate(PWSTR.From(url)).ThrowOnError();
            await completion.Task;
        }
        finally
        {
            _webView?.Object.remove_NavigationCompleted(token);
        }
    }

    public void Resize(int width, int height)
    {
        _controller?.Object.put_Bounds(RECT.Sized(0, 0, width, height)).ThrowOnError();
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            _bridge.Dispose();
            UnregisterNavigationReadyTracking();
            _controller?.Object.Close();
        }
        finally
        {
            _webView?.Dispose();
            _controller?.Dispose();
            _environment?.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private static Task<ComObject<ICoreWebView2Environment>> CreateEnvironmentAsync(
        string userDataFolder,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<ComObject<ICoreWebView2Environment>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(
            static state => ((TaskCompletionSource<ComObject<ICoreWebView2Environment>>)state!).TrySetCanceled(),
            tcs);

        var hr = WebView2.Functions.CreateCoreWebView2EnvironmentWithOptions(
            PWSTR.Null,
            PWSTR.From(userDataFolder),
            null!,
            new CoreWebView2CreateCoreWebView2EnvironmentCompletedHandler((result, environment) =>
            {
                if (result.IsError)
                {
                    tcs.TrySetException(Marshal.GetExceptionForHR(result)!);
                    return;
                }

                tcs.TrySetResult(new ComObject<ICoreWebView2Environment>(environment));
            }));

        if (hr.IsError)
            tcs.TrySetException(Marshal.GetExceptionForHR(hr)!);

        return tcs.Task;
    }

    private static Task<ComObject<ICoreWebView2Controller>> CreateControllerAsync(
        ICoreWebView2Environment environment,
        nint parentWindow,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<ComObject<ICoreWebView2Controller>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(
            static state => ((TaskCompletionSource<ComObject<ICoreWebView2Controller>>)state!).TrySetCanceled(),
            tcs);

        var hr = environment.CreateCoreWebView2Controller(
            new HWND(parentWindow),
            new CoreWebView2CreateCoreWebView2ControllerCompletedHandler((result, controller) =>
            {
                if (result.IsError)
                {
                    tcs.TrySetException(Marshal.GetExceptionForHR(result)!);
                    return;
                }

                tcs.TrySetResult(new ComObject<ICoreWebView2Controller>(controller));
            }));

        if (hr.IsError)
            tcs.TrySetException(Marshal.GetExceptionForHR(hr)!);

        return tcs.Task;
    }

    private static void ConfigureSettings(ICoreWebView2 webView, OmniHostOptions options)
    {
        webView.get_Settings(out var settings).ThrowOnError();
        using var settingsObject = new ComObject<ICoreWebView2Settings>(settings);
        var devTools = options.EnableDevTools ? BOOL.TRUE : BOOL.FALSE;

        settingsObject.Object.put_IsScriptEnabled(BOOL.TRUE).ThrowOnError();
        settingsObject.Object.put_IsWebMessageEnabled(BOOL.TRUE).ThrowOnError();
        settingsObject.Object.put_AreDevToolsEnabled(devTools).ThrowOnError();
        settingsObject.Object.put_AreDefaultContextMenusEnabled(devTools).ThrowOnError();
    }

    private void RegisterNavigationReadyTracking(ICoreWebView2 webView)
    {
        _navigationStartingHandler = new CoreWebView2NavigationStartingEventHandler(
            (_, _) => _bridge.SetDocumentReady(false));
        webView.add_NavigationStarting(_navigationStartingHandler, ref _navigationStartingToken).ThrowOnError();

        _navigationCompletedHandler = new CoreWebView2NavigationCompletedEventHandler(
            (_, _) => _bridge.SetDocumentReady(true));
        webView.add_NavigationCompleted(_navigationCompletedHandler, ref _navigationCompletedToken).ThrowOnError();
    }

    private void UnregisterNavigationReadyTracking()
    {
        if (_webView is null)
            return;

        if (_navigationStartingHandler is not null)
            _webView.Object.remove_NavigationStarting(_navigationStartingToken);

        if (_navigationCompletedHandler is not null)
            _webView.Object.remove_NavigationCompleted(_navigationCompletedToken);

        _navigationStartingHandler = null;
        _navigationCompletedHandler = null;
    }

    private static Task InjectWindowChromeSupportAsync(
        ICoreWebView2 webView,
        OmniHostOptions options,
        CancellationToken cancellationToken)
    {
        var windowStyle = options.WindowStyle.ToCssToken();
        var script = $$"""
            (function () {
                var style = {{JsonSerializer.Serialize(windowStyle, NativeWebView2JsonContext.Default.String)}};

                function applyWindowStyle() {
                    if (!document.documentElement) return;
                    document.documentElement.style.setProperty('--omni-window-style', style);
                    document.documentElement.setAttribute('data-omni-window-style', style);
                }

                function isInteractive(target) {
                    return !!(target && target.closest('button, input, textarea, select, option, a, label, summary, [omni-no-drag]'));
                }

                function getDragRegion(target) {
                    if (!target || isInteractive(target)) return null;
                    if (style !== 'frameless' && style !== 'vscode') return null;
                    return target.closest('[omni-drag]');
                }

                var resizeBorder = 8;
                var resizeCursors = {
                    'top-left': 'nwse-resize',
                    'bottom-right': 'nwse-resize',
                    'top-right': 'nesw-resize',
                    'bottom-left': 'nesw-resize',
                    top: 'ns-resize',
                    bottom: 'ns-resize',
                    left: 'ew-resize',
                    right: 'ew-resize'
                };

                function getResizeDirection(e) {
                    if (style !== 'frameless' && style !== 'vscode') return null;
                    if (!e || window.innerWidth <= 0 || window.innerHeight <= 0) return null;
                    if (isInteractive(e.target)) return null;

                    var x = e.clientX;
                    var y = e.clientY;
                    var left = x <= resizeBorder;
                    var right = x >= window.innerWidth - resizeBorder;
                    var top = y <= resizeBorder;
                    var bottom = y >= window.innerHeight - resizeBorder;

                    if (top && left) return 'top-left';
                    if (top && right) return 'top-right';
                    if (bottom && left) return 'bottom-left';
                    if (bottom && right) return 'bottom-right';
                    if (top) return 'top';
                    if (bottom) return 'bottom';
                    if (left) return 'left';
                    if (right) return 'right';
                    return null;
                }

                function setResizeCursor(direction) {
                    if (!document.body) return;
                    document.body.style.cursor = direction ? resizeCursors[direction] : '';
                }

                document.addEventListener('DOMContentLoaded', applyWindowStyle);
                applyWindowStyle();

                document.addEventListener('mousedown', function (e) {
                    if (e.button !== 0) return;
                    var resizeDirection = getResizeDirection(e);
                    if (resizeDirection) {
                        e.preventDefault();
                        omni.window.startResize({ direction: resizeDirection });
                        return;
                    }

                    if (!getDragRegion(e.target)) return;
                    e.preventDefault();
                    omni.window.startDrag({
                        button: e.button + 1,
                        screenX: e.screenX,
                        screenY: e.screenY
                    });
                }, true);

                document.addEventListener('mousemove', function (e) {
                    setResizeCursor(getResizeDirection(e));
                }, true);

                document.addEventListener('mouseleave', function () {
                    setResizeCursor(null);
                }, true);

                document.addEventListener('dblclick', function (e) {
                    if (e.button !== 0) return;
                    if (!getDragRegion(e.target)) return;
                    e.preventDefault();
                    omni.window.maximize();
                }, true);

                document.addEventListener('contextmenu', function (e) {
                    if (!getDragRegion(e.target)) return;
                    e.preventDefault();
                    omni.window.showSystemMenu({
                        screenX: e.screenX,
                        screenY: e.screenY
                    });
                }, true);
            })();
            """;

        return AddScriptToExecuteOnDocumentCreatedAsync(webView, script, cancellationToken);
    }

    private static Task InjectHostCssAsync(
        ICoreWebView2 webView,
        OmniHostOptions options,
        CancellationToken cancellationToken)
    {
        var css = BuildHostCss(options);
        if (string.IsNullOrWhiteSpace(css))
            return Task.CompletedTask;

        var script = $$"""
            (function () {
                var css = {{JsonSerializer.Serialize(css, NativeWebView2JsonContext.Default.String)}};
                var styleId = 'omni-host-style';

                function ensureHostStyle() {
                    var existing = document.getElementById(styleId);
                    if (existing) {
                        existing.textContent = css;
                        return;
                    }

                    var style = document.createElement('style');
                    style.id = styleId;
                    style.textContent = css;
                    (document.head || document.documentElement).appendChild(style);
                }

                if (document.readyState === 'loading') {
                    document.addEventListener('DOMContentLoaded', ensureHostStyle, { once: true });
                }

                ensureHostStyle();
            })();
            """;

        return AddScriptToExecuteOnDocumentCreatedAsync(webView, script, cancellationToken);
    }

    private static string? BuildHostCss(OmniHostOptions options) =>
        options.ScrollBarMode switch
        {
            OmniScrollBarMode.Auto => null,
            OmniScrollBarMode.Hidden => """
                html, body {
                  overflow: hidden !important;
                }

                ::-webkit-scrollbar {
                  width: 0 !important;
                  height: 0 !important;
                }
                """,
            OmniScrollBarMode.VerticalOnly => """
                html, body {
                  overflow-x: hidden !important;
                  overflow-y: auto !important;
                }
                """,
            OmniScrollBarMode.Custom => !string.IsNullOrWhiteSpace(options.ScrollBarCustomCss)
                ? options.ScrollBarCustomCss
                : throw new InvalidOperationException(
                    "ScrollBarMode is Custom, but ScrollBarCustomCss was not provided."),
            _ => null,
        };

    private static async Task AddScriptToExecuteOnDocumentCreatedAsync(
        ICoreWebView2 webView,
        string script,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(
            static state => ((TaskCompletionSource)state!).TrySetCanceled(),
            tcs);

        var hr = webView.AddScriptToExecuteOnDocumentCreated(
            PWSTR.From(script),
            new CoreWebView2AddScriptToExecuteOnDocumentCreatedCompletedHandler((result, _) =>
            {
                if (result.IsError)
                {
                    tcs.TrySetException(Marshal.GetExceptionForHR(result)!);
                    return;
                }

                tcs.TrySetResult();
            }));

        if (hr.IsError)
            tcs.TrySetException(Marshal.GetExceptionForHR(hr)!);

        await tcs.Task;
    }

    private void EnsureInitialized()
    {
        if (_webView is null)
            throw new InvalidOperationException(
                "NativeWebView2Adapter has not been initialized. Call InitializeAsync first.");
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray());
    }
}
