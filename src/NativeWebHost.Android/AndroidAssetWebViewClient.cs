using Android.Content;
using Android.Webkit;
namespace NativeWebHost.Android;

internal sealed class AndroidAssetWebViewClient : WebViewClient
{
    private const string NotFoundJson = """{"message":"Not Found"}""";

    private readonly Context _context;
    private readonly NativeWebHostOptions _options;
    private readonly AndroidWebViewJsBridge _bridge;
    private readonly string _assetRoot;

    public AndroidAssetWebViewClient(
        Context context,
        NativeWebHostOptions options,
        AndroidWebViewJsBridge bridge)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _assetRoot = ResolveAssetRoot(options);
    }

    public override WebResourceResponse? ShouldInterceptRequest(WebView? view, IWebResourceRequest? request)
        => CreateResponse(request?.Url?.ToString());

    [Obsolete("Use ShouldInterceptRequest(WebView?, IWebResourceRequest?) on supported Android versions.")]
    public override WebResourceResponse? ShouldInterceptRequest(WebView? view, string? url)
        => CreateResponse(url);

    public override void OnPageStarted(WebView? view, string? url, global::Android.Graphics.Bitmap? favicon)
    {
        _bridge.SetDocumentLoading();
        base.OnPageStarted(view, url, favicon);
    }

    public override async void OnPageFinished(WebView? view, string? url)
    {
        base.OnPageFinished(view, url);
        try
        {
            await _bridge.InjectDocumentScriptsAsync();
        }
        catch
        {
        }
    }

    private WebResourceResponse? CreateResponse(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl) ||
            !Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, new Uri(AndroidWebViewAssetHost.Origin).Host, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var requestPath = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
        if (requestPath.Length == 0)
            requestPath = "index.html";

        if (requestPath.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
            return CreateTextResponse(404, "Not Found", NotFoundJson, "application/json; charset=utf-8");

        if (!IsSafeRelativePath(requestPath))
            return CreateTextResponse(404, "Not Found", "Not Found", "text/plain; charset=utf-8");

        var assetPath = CombineAssetPath(_assetRoot, requestPath);
        try
        {
            var stream = _context.Assets?.Open(assetPath);
            if (stream is not null)
                return CreateAssetResponse(requestPath, stream);
        }
        catch (System.IO.FileNotFoundException)
        {
        }
        catch (Java.IO.FileNotFoundException)
        {
        }

        if (ShouldFallbackToIndex(requestPath))
        {
            try
            {
                var stream = _context.Assets?.Open(CombineAssetPath(_assetRoot, "index.html"));
                if (stream is not null)
                    return CreateAssetResponse("index.html", stream);
            }
            catch
            {
            }
        }

        return CreateTextResponse(404, "Not Found", "Not Found", "text/plain; charset=utf-8");
    }

    private static string ResolveAssetRoot(NativeWebHostOptions options)
        => options.AdapterSettings.TryGetValue("AndroidAssetRoot", out var root) &&
           !string.IsNullOrWhiteSpace(root)
            ? root.Trim().Trim('/').Replace('\\', '/')
            : AndroidWebViewAssetHost.DefaultAssetRoot;

    private static WebResourceResponse CreateAssetResponse(string path, Stream stream)
        => new(GetContentType(path), GetEncoding(path), PrepareAssetStream(path, stream))
        {
            ResponseHeaders = new Dictionary<string, string>
            {
                ["Cache-Control"] = "no-cache",
                ["Access-Control-Allow-Origin"] = AndroidWebViewAssetHost.Origin,
            },
        };

    private static Stream PrepareAssetStream(string path, Stream stream)
    {
        if (!Path.GetFileName(path).Equals("index.html", StringComparison.OrdinalIgnoreCase))
            return stream;

        using (stream)
        using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            var html = reader.ReadToEnd();
            var script = "<script>" +
                AndroidWebViewJsBridge.BridgeScript +
                Environment.NewLine +
                AndroidWebViewJsBridge.FetchBridgeScript +
                "</script>";

            var marker = "</head>";
            var index = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            var patched = index >= 0
                ? html.Insert(index, script)
                : script + html;
            return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(patched));
        }
    }

    private static WebResourceResponse CreateTextResponse(
        int statusCode,
        string statusText,
        string text,
        string contentType)
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));
        var parts = contentType.Split(';', 2, StringSplitOptions.TrimEntries);
        return new WebResourceResponse(parts[0], "utf-8", statusCode, statusText, new Dictionary<string, string>
        {
            ["Cache-Control"] = "no-cache",
            ["Access-Control-Allow-Origin"] = AndroidWebViewAssetHost.Origin,
        }, stream);
    }

    private static bool IsSafeRelativePath(string path)
        => !string.IsNullOrWhiteSpace(path) &&
           !path.Contains("..", StringComparison.Ordinal) &&
           !path.Contains('\\', StringComparison.Ordinal) &&
           !path.Contains("://", StringComparison.Ordinal);

    private static bool ShouldFallbackToIndex(string path)
        => !Path.HasExtension(path) &&
           !path.StartsWith("_framework/", StringComparison.OrdinalIgnoreCase) &&
           !path.StartsWith("_content/", StringComparison.OrdinalIgnoreCase);

    private static string CombineAssetPath(string root, string path)
        => string.IsNullOrWhiteSpace(root)
            ? path.Replace('\\', '/')
            : $"{root.TrimEnd('/')}/{path.TrimStart('/')}".Replace('\\', '/');

    private static string? GetEncoding(string path)
    {
        var contentType = GetContentType(path);
        return contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("application/javascript", StringComparison.OrdinalIgnoreCase) ||
               contentType.Equals("text/javascript", StringComparison.OrdinalIgnoreCase)
            ? "utf-8"
            : null;
    }

    private static string GetContentType(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".html" or ".htm" => "text/html",
            ".js" or ".mjs" => "text/javascript",
            ".css" => "text/css",
            ".json" => "application/json",
            ".wasm" => "application/wasm",
            ".dll" => "application/octet-stream",
            ".dat" => "application/octet-stream",
            ".pdb" => "application/octet-stream",
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
