using Microsoft.Web.WebView2.Core;

namespace OmniWebHost.WebView2;

/// <summary>
/// Real <see cref="IWebViewAdapter"/> implementation backed by Microsoft WebView2.
/// </summary>
public sealed class WebView2Adapter : IWebViewAdapter
{
    private readonly WebView2JsBridge _bridge = new();
    private CoreWebView2Environment? _environment;
    private CoreWebView2Controller? _controller;
    private BrowserCapabilities _capabilities = new()
    {
        EngineName = "WebView2",
        EngineVersion = "unknown",
        SupportsJavaScript = true,
        SupportsJsBridge = true,
        SupportsCustomSchemes = true,
        SupportsDevTools = true,
        SupportedHostSurfaces = new[] { HostSurfaceKind.Hwnd },
    };

    public string AdapterId => "webview2";
    public BrowserCapabilities Capabilities => _capabilities;
    public IJsBridge JsBridge => _bridge;

    // ── IWebViewAdapter ──────────────────────────────────────────────────────

    public async Task InitializeAsync(
        HostSurfaceDescriptor surface,
        OmniWebHostOptions options,
        CancellationToken cancellationToken = default)
    {
        if (surface.Kind != HostSurfaceKind.Hwnd)
            throw new NotSupportedException(
                $"WebView2Adapter only supports {HostSurfaceKind.Hwnd} host surfaces.");

        if (!surface.IsCreated)
            throw new InvalidOperationException(
                "The supplied host surface has not been created yet.");

        var userDataFolder = options.UserDataFolder
            ?? Path.Combine(Path.GetTempPath(), "OmniWebHost", SanitizeFolderName(options.Title));

        var environmentOptions = CreateEnvironmentOptions(options);
        _environment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: userDataFolder,
            options: environmentOptions);

        _controller = await _environment.CreateCoreWebView2ControllerAsync(surface.Handle);
        _controller.IsVisible = true;

        var core = _controller.CoreWebView2;

        // Update capabilities with the real engine version.
        _capabilities = new BrowserCapabilities
        {
            EngineName = "WebView2",
            EngineVersion = _environment.BrowserVersionString,
            SupportsJavaScript = true,
            SupportsJsBridge = true,
            SupportsCustomSchemes = true,
            SupportsDevTools = true,
            SupportedHostSurfaces = new[] { HostSurfaceKind.Hwnd },
        };

        // Developer tools.
        core.Settings.AreDefaultContextMenusEnabled = options.EnableDevTools;
        core.Settings.AreDevToolsEnabled = options.EnableDevTools;
        core.Settings.IsWebMessageEnabled = true;

        core.NavigationStarting += (_, _) => _bridge.SetDocumentReady(false);
        core.NavigationCompleted += (_, _) => _bridge.SetDocumentReady(true);

        // Custom scheme / local asset serving.
        if (options.ContentRootPath is not null)
            RegisterCustomScheme(core, options);

        // Initialise the JS bridge (injects helper script).
        await _bridge.InitializeAsync(core);
        await InjectWindowChromeSupportAsync(core, options);
        await InjectHostCssAsync(core, options);
    }

    public Task InitializeAsync(
        nint hostHandle,
        OmniWebHostOptions options,
        CancellationToken cancellationToken = default)
        => InitializeAsync(
            new HostSurfaceDescriptor(HostSurfaceKind.Hwnd, hostHandle),
            options,
            cancellationToken);

    public Task NavigateAsync(string url, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        _controller!.CoreWebView2.Navigate(url);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Updates the WebView2 controller bounds to fill the host client area.
    /// Call this from the host window's resize handler.
    /// </summary>
    public void Resize(int width, int height)
    {
        if (_controller is not null)
            _controller.Bounds = new System.Drawing.Rectangle(0, 0, width, height);
    }

    public ValueTask DisposeAsync()
    {
        _controller?.Close();
        return ValueTask.CompletedTask;
    }

    // ── Custom scheme handler ────────────────────────────────────────────────

    private void RegisterCustomScheme(CoreWebView2 core, OmniWebHostOptions options)
    {
        var filter = $"{options.CustomScheme}://*";
        core.AddWebResourceRequestedFilter(filter, CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += (_, args) =>
            HandleWebResourceRequest(args, options.ContentRootPath!, _environment!);
    }

    private static CoreWebView2EnvironmentOptions CreateEnvironmentOptions(OmniWebHostOptions options)
    {
        // WebView2 only raises WebResourceRequested for custom schemes that were
        // registered when the environment was created. On this package version,
        // CustomSchemeRegistrations is exposed as a read-only property and may be
        // null until the constructor overload populates it, so pass the list here.
        var customScheme = new CoreWebView2CustomSchemeRegistration(options.CustomScheme)
        {
            HasAuthorityComponent = true,
            TreatAsSecure = true,
        };

        // Allow requests originating from the hosted app pages themselves.
        customScheme.AllowedOrigins.Add($"{options.CustomScheme}://*");

        return new CoreWebView2EnvironmentOptions(
            additionalBrowserArguments: null,
            language: null,
            targetCompatibleBrowserVersion: null,
            allowSingleSignOnUsingOSPrimaryAccount: false,
            customSchemeRegistrations: [customScheme]);
    }

    private static void HandleWebResourceRequest(
        CoreWebView2WebResourceRequestedEventArgs args,
        string contentRootPath,
        CoreWebView2Environment environment)
    {
        var uri = new Uri(args.Request.Uri);
        // Map app://localhost/path/to/file  →  contentRootPath/path/to/file
        var relative = uri.AbsolutePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(contentRootPath);
        var fullPath = Path.GetFullPath(Path.Combine(root, relative));

        // Guard against path-traversal.
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            args.Response = environment.CreateWebResourceResponse(
                Stream.Null, 403, "Forbidden", string.Empty);
            return;
        }

        if (!File.Exists(fullPath))
        {
            args.Response = environment.CreateWebResourceResponse(
                Stream.Null, 404, "Not Found", string.Empty);
            return;
        }

        var contentType = GetMimeType(fullPath);
        var stream = File.OpenRead(fullPath);
        args.Response = environment.CreateWebResourceResponse(
            stream, 200, "OK", $"Content-Type: {contentType}");
    }

    private static string GetMimeType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".html" or ".htm" => "text/html; charset=utf-8",
            ".js"             => "application/javascript; charset=utf-8",
            ".css"            => "text/css; charset=utf-8",
            ".json"           => "application/json; charset=utf-8",
            ".png"            => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif"            => "image/gif",
            ".svg"            => "image/svg+xml",
            ".ico"            => "image/x-icon",
            ".woff"           => "font/woff",
            ".woff2"          => "font/woff2",
            ".txt"            => "text/plain; charset=utf-8",
            _                 => "application/octet-stream",
        };

    private static Task InjectWindowChromeSupportAsync(CoreWebView2 core, OmniWebHostOptions options)
    {
        var windowStyle = options.WindowStyle == OmniWindowStyle.Frameless ? "frameless" : "normal";
        var script = $$"""
            (function () {
                var style = {{System.Text.Json.JsonSerializer.Serialize(windowStyle)}};

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
                    return target.closest('[omni-drag]');
                }

                document.addEventListener('DOMContentLoaded', applyWindowStyle);
                applyWindowStyle();

                document.addEventListener('mousedown', function (e) {
                    if (e.button !== 0) return;
                    if (!getDragRegion(e.target)) return;
                    e.preventDefault();
                    omni.window.startDrag();
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
                    omni.window.showSystemMenu();
                }, true);
            })();
            """;

        return core.AddScriptToExecuteOnDocumentCreatedAsync(script);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Task InjectHostCssAsync(CoreWebView2 core, OmniWebHostOptions options)
    {
        var css = BuildHostCss(options);
        if (string.IsNullOrWhiteSpace(css))
            return Task.CompletedTask;

        var script = $$"""
            (function () {
                var css = {{System.Text.Json.JsonSerializer.Serialize(css)}};
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

        return core.AddScriptToExecuteOnDocumentCreatedAsync(script);
    }

    private static string? BuildHostCss(OmniWebHostOptions options) =>
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

    private void EnsureInitialized()
    {
        if (_controller is null)
            throw new InvalidOperationException(
                "WebView2Adapter has not been initialized. Call InitializeAsync first.");
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray());
    }
}
