using NativeWebHost.Linux.Native;

namespace NativeWebHost.Linux;

/// <summary>
/// Experimental <see cref="IWebViewAdapter"/> implementation backed by WebKitGTK.
/// </summary>
public sealed class WebKitGtkAdapter : IWebViewAdapter
{
    private readonly WebKitGtkJsBridge _bridge = new();

    private BrowserCapabilities _capabilities = new()
    {
        EngineName = "WebKitGTK",
        EngineVersion = "unknown",
        SupportsJavaScript = true,
        SupportsJsBridge = true,
        SupportsCustomSchemes = true,
        SupportsDevTools = true,
        SupportedHostSurfaces = new[] { HostSurfaceKind.GtkWidget },
    };

    private IntPtr _hostContainerHandle;
    private IntPtr _userContentManagerHandle;
    private IntPtr _webViewHandle;
    private SynchronizationContext? _dispatchContext;
    private NativeWebHostOptions? _options;

    public string AdapterId => "webkitgtk";

    public BrowserCapabilities Capabilities => _capabilities;

    public IJsBridge JsBridge => _bridge;

    public Task InitializeAsync(
        HostSurfaceDescriptor surface,
        NativeWebHostOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("WebKitGtkAdapter is only supported on Linux.");

        if (surface.Kind != HostSurfaceKind.GtkWidget)
            throw new NotSupportedException(
                $"WebKitGtkAdapter only supports {HostSurfaceKind.GtkWidget} host surfaces.");

        if (!surface.IsCreated)
            throw new InvalidOperationException("The supplied GTK host surface has not been created yet.");

        WebKitGtkLibraryResolver.EnsureRegistered();

        _dispatchContext = SynchronizationContext.Current;
        _hostContainerHandle = surface.Handle;
        _options = options;

        try
        {
            _userContentManagerHandle = WebKitGtkNative.WebKitUserContentManagerNew();
            if (_userContentManagerHandle == IntPtr.Zero)
                throw new InvalidOperationException("webkit_user_content_manager_new returned a null handle.");

            _webViewHandle = WebKitGtkNative.WebKitWebViewNewWithUserContentManager(_userContentManagerHandle);
            if (_webViewHandle == IntPtr.Zero)
                throw new InvalidOperationException("webkit_web_view_new_with_user_content_manager returned null.");

            ConfigureSettings(_webViewHandle, options);
            WebKitGtkSchemeRegistry.Register(_webViewHandle, options);

            _bridge.Initialize(_userContentManagerHandle, _webViewHandle);

            if (!WebKitGtkNative.WebKitUserContentManagerRegisterScriptMessageHandler(
                _userContentManagerHandle,
                "nativeWeb"))
                throw new InvalidOperationException(
                    "webkit_user_content_manager_register_script_message_handler failed for 'nativeWeb'.");

            _bridge.AddDocumentStartScript(BuildWindowChromeSupportScript(options));

            var builtInTitleBarScript = BuiltInTitleBarScriptBuilder.Build(options);
            if (!string.IsNullOrWhiteSpace(builtInTitleBarScript))
                _bridge.AddDocumentStartScript(builtInTitleBarScript);

            var hostCss = BuildHostCssInjectionScript(options);
            if (!string.IsNullOrWhiteSpace(hostCss))
                _bridge.AddDocumentStartScript(hostCss);

            WebKitGtkNative.GtkFixedPut(_hostContainerHandle, _webViewHandle, 0, 0);
            WebKitGtkNative.GtkWidgetSetSizeRequest(_webViewHandle, surface.Width, surface.Height);
            WebKitGtkNative.GtkWidgetShow(_webViewHandle);

            _capabilities = new BrowserCapabilities
            {
                EngineName = "WebKitGTK",
                EngineVersion = $"{WebKitGtkNative.WebKitGetMajorVersion()}." +
                    $"{WebKitGtkNative.WebKitGetMinorVersion()}." +
                    $"{WebKitGtkNative.WebKitGetMicroVersion()}",
                SupportsJavaScript = true,
                SupportsJsBridge = true,
                SupportsCustomSchemes = true,
                SupportsDevTools = true,
                SupportedHostSurfaces = new[] { HostSurfaceKind.GtkWidget },
            };

            return Task.CompletedTask;
        }
        catch
        {
            DisposeAsync().GetAwaiter().GetResult();
            throw;
        }
    }

    public Task NavigateAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureInitialized();

        var targetUrl = TranslateUrl(url);
        return RunOnUiThreadAsync(() =>
        {
            WebKitGtkNative.WebKitWebViewLoadUri(_webViewHandle, targetUrl);
            return Task.CompletedTask;
        });
    }

    public void Resize(int width, int height)
    {
        if (_webViewHandle == IntPtr.Zero)
            return;

        if (_dispatchContext is null || SynchronizationContext.Current == _dispatchContext)
        {
            WebKitGtkNative.GtkWidgetSetSizeRequest(_webViewHandle, width, height);
            return;
        }

        _dispatchContext.Post(_ =>
        {
            if (_webViewHandle != IntPtr.Zero)
                WebKitGtkNative.GtkWidgetSetSizeRequest(_webViewHandle, width, height);
        }, null);
    }

    public ValueTask DisposeAsync()
    {
        if (_webViewHandle != IntPtr.Zero)
        {
            WebKitGtkSchemeRegistry.Unregister(_webViewHandle);
            WebKitGtkNative.GObjectUnref(_webViewHandle);
            _webViewHandle = IntPtr.Zero;
        }

        if (_userContentManagerHandle != IntPtr.Zero)
        {
            WebKitGtkNative.GObjectUnref(_userContentManagerHandle);
            _userContentManagerHandle = IntPtr.Zero;
        }

        _hostContainerHandle = IntPtr.Zero;
        _dispatchContext = null;
        _options = null;
        return ValueTask.CompletedTask;
    }

    private static void ConfigureSettings(IntPtr webViewHandle, NativeWebHostOptions options)
    {
        var settings = WebKitGtkNative.WebKitWebViewGetSettings(webViewHandle);
        if (settings == IntPtr.Zero)
            return;

        WebKitGtkNative.WebKitSettingsSetEnableJavascript(settings, enabled: true);
        WebKitGtkNative.WebKitSettingsSetEnableDeveloperExtras(settings, options.EnableDevTools);
    }

    private Task RunOnUiThreadAsync(Func<Task> action)
    {
        if (_dispatchContext is null || SynchronizationContext.Current == _dispatchContext)
            return action();

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dispatchContext.Post(async _ =>
        {
            try
            {
                await action();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);

        return tcs.Task;
    }

    private string TranslateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(_options?.ContentRootPath))
            return url;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        if (!string.Equals(uri.Scheme, _options.CustomScheme, StringComparison.OrdinalIgnoreCase))
            return url;

        return url;
    }

    private static string BuildWindowChromeSupportScript(NativeWebHostOptions options)
    {
        var windowStyle = options.WindowStyle.ToCssToken();

        return $$"""
            (function () {
                var style = {{System.Text.Json.JsonSerializer.Serialize(windowStyle)}};

                function applyWindowStyle() {
                    if (!document.documentElement) return;
                    document.documentElement.style.setProperty('--native-web-window-style', style);
                    document.documentElement.setAttribute('data-native-web-window-style', style);
                }

                function isInteractive(target) {
                    return !!(target && target.closest('button, input, textarea, select, option, a, label, summary, [native-web-no-drag]'));
                }

                function getDragRegion(target) {
                    if (!target || isInteractive(target)) return null;
                    if (style !== 'frameless' && style !== 'vscode') return null;
                    return target.closest('[native-web-drag]');
                }

                document.addEventListener('DOMContentLoaded', applyWindowStyle);
                applyWindowStyle();

                document.addEventListener('mousedown', function (e) {
                    if (e.button !== 0) return;
                    if (!getDragRegion(e.target)) return;
                    e.preventDefault();
                    nativeWeb.window.startDrag({
                        button: e.button + 1,
                        screenX: e.screenX,
                        screenY: e.screenY
                    });
                }, true);

                document.addEventListener('dblclick', function (e) {
                    if (e.button !== 0) return;
                    if (!getDragRegion(e.target)) return;
                    e.preventDefault();
                    nativeWeb.window.maximize();
                }, true);

                document.addEventListener('contextmenu', function (e) {
                    if (!getDragRegion(e.target)) return;
                    e.preventDefault();
                    nativeWeb.window.showSystemMenu({
                        screenX: e.screenX,
                        screenY: e.screenY
                    });
                }, true);
            })();
            """;
    }

    private static string? BuildHostCssInjectionScript(NativeWebHostOptions options)
    {
        var css = options.ScrollBarMode switch
        {
            NativeWebScrollBarMode.Auto => null,
            NativeWebScrollBarMode.Hidden => """
                html, body {
                  overflow: hidden !important;
                }

                ::-webkit-scrollbar {
                  width: 0 !important;
                  height: 0 !important;
                }
                """,
            NativeWebScrollBarMode.VerticalOnly => """
                html, body {
                  overflow-x: hidden !important;
                  overflow-y: auto !important;
                }
                """,
            NativeWebScrollBarMode.Custom => !string.IsNullOrWhiteSpace(options.ScrollBarCustomCss)
                ? options.ScrollBarCustomCss
                : throw new InvalidOperationException(
                    "ScrollBarMode is Custom, but ScrollBarCustomCss was not provided."),
            _ => null,
        };

        if (string.IsNullOrWhiteSpace(css))
            return null;

        return $$"""
            (function () {
                var css = {{System.Text.Json.JsonSerializer.Serialize(css)}};
                var styleId = 'native-web-host-style';

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
    }

    private void EnsureInitialized()
    {
        if (_webViewHandle == IntPtr.Zero)
            throw new InvalidOperationException(
                "WebKitGtkAdapter has not been initialized. Call InitializeAsync first.");
    }
}
