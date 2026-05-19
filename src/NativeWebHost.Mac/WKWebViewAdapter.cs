using AppKit;
using CoreGraphics;
using Foundation;
using ObjCRuntime;
using WebKit;

namespace NativeWebHost.Mac;

/// <summary>
/// macOS <see cref="IWebViewAdapter"/> implementation backed by WKWebView.
/// </summary>
public sealed class WKWebViewAdapter : IWebViewAdapter
{
    private readonly WKWebViewJsBridge _bridge = new();

    private BrowserCapabilities _capabilities = new()
    {
        EngineName = "WKWebView",
        EngineVersion = "system",
        SupportsJavaScript = true,
        SupportsJsBridge = true,
        SupportsCustomSchemes = true,
        SupportsDevTools = false,
        SupportedHostSurfaces = new[] { HostSurfaceKind.NsView },
    };

    private NSView? _hostView;
    private WKWebView? _webView;
    private WKWebViewConfiguration? _configuration;
    private MacSchemeHandler? _schemeHandler;
    private NativeWebHostOptions? _options;

    public string AdapterId => "wkwebview";

    public BrowserCapabilities Capabilities => _capabilities;

    public IJsBridge JsBridge => _bridge;

    public Task InitializeAsync(
        HostSurfaceDescriptor surface,
        NativeWebHostOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("WKWebViewAdapter is only supported on macOS.");

        if (surface.Kind != HostSurfaceKind.NsView)
            throw new NotSupportedException(
                $"WKWebViewAdapter only supports {HostSurfaceKind.NsView} host surfaces.");

        if (!surface.IsCreated)
            throw new InvalidOperationException("The supplied NSView host surface has not been created yet.");

        if (!NSThread.IsMain)
            throw new InvalidOperationException("WKWebViewAdapter must be initialized on the AppKit main thread.");

        _hostView = Runtime.GetNSObject<NSView>(surface.Handle)
            ?? throw new InvalidOperationException("The supplied NSView handle could not be resolved.");
        _options = options;

        _configuration = CreateConfiguration(options);
        _webView = new WKWebView(_hostView.Bounds, _configuration)
        {
            AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable,
            AllowsBackForwardNavigationGestures = false
        };

        _bridge.Initialize(_configuration.UserContentController, _webView);

        _bridge.AddDocumentStartScript(BuildWindowChromeSupportScript(options));

        var hostCss = BuildHostCssInjectionScript(options);
        if (!string.IsNullOrWhiteSpace(hostCss))
            _bridge.AddDocumentStartScript(hostCss);

        _hostView.AddSubview(_webView);
        Resize(surface.Width, surface.Height);

        _capabilities = new BrowserCapabilities
        {
            EngineName = "WKWebView",
            EngineVersion = "system",
            SupportsJavaScript = true,
            SupportsJsBridge = true,
            SupportsCustomSchemes = _schemeHandler is not null,
            SupportsDevTools = false,
            SupportedHostSurfaces = new[] { HostSurfaceKind.NsView },
        };

        return Task.CompletedTask;
    }

    public Task NavigateAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureInitialized();

        return RunOnMainThreadAsync(() =>
        {
            var targetUrl = TranslateUrl(url);
            var nsUrl = new NSUrl(targetUrl);
            var request = NSUrlRequest.FromUrl(nsUrl);
            _webView!.LoadRequest(request);
            return Task.CompletedTask;
        });
    }

    public void Resize(int width, int height)
    {
        if (_webView is null || _hostView is null)
            return;

        PostToMainThread(() =>
        {
            if (_webView is null || _hostView is null)
                return;

            var bounds = _hostView.Bounds;
            _webView.Frame = bounds;
        });
    }

    public ValueTask DisposeAsync()
    {
        PostToMainThread(() =>
        {
            if (_configuration?.UserContentController is not null)
                _bridge.Dispose(_configuration.UserContentController);

            _webView?.StopLoading();
            _webView?.RemoveFromSuperview();
            _webView?.Dispose();
            _webView = null;

            _schemeHandler?.Dispose();
            _schemeHandler = null;

            _configuration?.Dispose();
            _configuration = null;
            _hostView = null;
            _options = null;
        });

        return ValueTask.CompletedTask;
    }

    private WKWebViewConfiguration CreateConfiguration(NativeWebHostOptions options)
    {
        var userContentController = new WKUserContentController();
        userContentController.AddScriptMessageHandler(_bridge, WKWebViewJsBridge.HandlerName);

        var configuration = new WKWebViewConfiguration
        {
            UserContentController = userContentController,
            WebsiteDataStore = string.IsNullOrWhiteSpace(options.UserDataFolder)
                ? WKWebsiteDataStore.DefaultDataStore
                : WKWebsiteDataStore.NonPersistentDataStore,
            Preferences = new WKPreferences
            {
                JavaScriptCanOpenWindowsAutomatically = true,
            },
            DefaultWebpagePreferences = new WKWebpagePreferences
            {
                AllowsContentJavaScript = true,
            },
            SuppressesIncrementalRendering = false,
        };

        if (!string.IsNullOrWhiteSpace(options.ContentRootPath))
        {
            _schemeHandler = new MacSchemeHandler(options);
            configuration.SetUrlSchemeHandler(_schemeHandler, options.CustomScheme);
        }

        return configuration;
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

    private static Task RunOnMainThreadAsync(Func<Task> action)
    {
        if (NSThread.IsMain)
            return action();

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        NSApplication.SharedApplication.BeginInvokeOnMainThread(async () =>
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
        });

        return tcs.Task;
    }

    private static void PostToMainThread(Action action)
    {
        if (NSThread.IsMain)
        {
            action();
            return;
        }

        NSApplication.SharedApplication.BeginInvokeOnMainThread(action);
    }

    private void EnsureInitialized()
    {
        if (_webView is null)
            throw new InvalidOperationException(
                "WKWebViewAdapter has not been initialized. Call InitializeAsync first.");
    }

    private static string BuildWindowChromeSupportScript(NativeWebHostOptions options)
    {
        var windowStyle = options.WindowStyle.ToCssToken();

        return $$"""
            (function () {
                var style = {{System.Text.Json.JsonSerializer.Serialize(windowStyle, MacJsonContext.Default.String)}};

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
                var css = {{System.Text.Json.JsonSerializer.Serialize(css, MacJsonContext.Default.String)}};
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

    private sealed class MacSchemeHandler : NSObject, IWKUrlSchemeHandler
    {
        private readonly string _rootPath;

        public MacSchemeHandler(NativeWebHostOptions options)
        {
            _rootPath = Path.GetFullPath(options.ContentRootPath!);
        }

        public void StartUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
        {
            try
            {
                var requestUrl = urlSchemeTask.Request.Url;
                var path = ResolvePath(requestUrl);
                if (path is null || !File.Exists(path))
                {
                    FinishText(urlSchemeTask, requestUrl, 404, "Not Found", "text/plain; charset=utf-8");
                    return;
                }

                var data = NSData.FromFile(path);
                var response = new NSHttpUrlResponse(
                    requestUrl,
                    GetContentType(path),
                    new IntPtr((long)(data?.Length ?? 0)),
                    "utf-8");

                urlSchemeTask.DidReceiveResponse(response);
                if (data is not null)
                    urlSchemeTask.DidReceiveData(data);
                urlSchemeTask.DidFinish();
            }
            catch (Exception ex)
            {
                urlSchemeTask.DidFailWithError(NSError.FromDomain(
                    new NSString("NativeWebHost.Mac"),
                    new IntPtr(1),
                    NSDictionary.FromObjectAndKey(new NSString(ex.Message), new NSString("message"))));
            }
        }

        public void StopUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
        {
        }

        private string? ResolvePath(NSUrl? requestUrl)
        {
            if (requestUrl is null)
                return null;

            var relativePath = Uri.UnescapeDataString(requestUrl.Path ?? string.Empty)
                .TrimStart('/');

            if (string.IsNullOrWhiteSpace(relativePath))
                relativePath = "index.html";

            var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
            return fullPath.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase)
                ? fullPath
                : null;
        }

        private static void FinishText(
            IWKUrlSchemeTask task,
            NSUrl? requestUrl,
            int statusCode,
            string text,
            string contentType)
        {
            var data = NSData.FromString(text, NSStringEncoding.UTF8);
            var response = new NSHttpUrlResponse(
                requestUrl ?? new NSUrl("about:blank"),
                contentType,
                new IntPtr((long)data.Length),
                "utf-8");

            task.DidReceiveResponse(response);
            task.DidReceiveData(data);
            task.DidFinish();
        }

        private static string GetContentType(string path)
            => Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".html" or ".htm" => "text/html; charset=utf-8",
                ".js" => "text/javascript; charset=utf-8",
                ".mjs" => "text/javascript; charset=utf-8",
                ".css" => "text/css; charset=utf-8",
                ".json" => "application/json; charset=utf-8",
                ".wasm" => "application/wasm",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                ".webp" => "image/webp",
                ".avif" => "image/avif",
                ".woff" => "font/woff",
                ".woff2" => "font/woff2",
                ".ttf" => "font/ttf",
                ".otf" => "font/otf",
                _ => "application/octet-stream",
            };
    }
}
