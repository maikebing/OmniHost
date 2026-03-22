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
    };

    public string AdapterId => "webview2";
    public BrowserCapabilities Capabilities => _capabilities;
    public IJsBridge JsBridge => _bridge;

    // ── IWebViewAdapter ──────────────────────────────────────────────────────

    public async Task InitializeAsync(
        nint hostHandle,
        OmniWebHostOptions options,
        CancellationToken cancellationToken = default)
    {
        var userDataFolder = options.UserDataFolder
            ?? Path.Combine(Path.GetTempPath(), "OmniWebHost", SanitizeFolderName(options.Title));

        _environment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: userDataFolder);

        _controller = await _environment.CreateCoreWebView2ControllerAsync(hostHandle);
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
        };

        // Developer tools.
        core.Settings.AreDefaultContextMenusEnabled = options.EnableDevTools;
        core.Settings.AreDevToolsEnabled = options.EnableDevTools;

        // Custom scheme / local asset serving.
        if (options.ContentRootPath is not null)
            RegisterCustomScheme(core, options);

        // Initialise the JS bridge (injects helper script).
        await _bridge.InitializeAsync(core);
    }

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

    // ── Helpers ──────────────────────────────────────────────────────────────

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

