using System.Runtime.InteropServices;
using System.Text;
using OmniHost.WebKitGtk.Native;

namespace OmniHost.WebKitGtk;

internal static class WebKitGtkSchemeRegistry
{
    private static readonly object Gate = new();
    private static readonly Dictionary<nint, SchemeBinding> WebViewBindings = new();
    private static readonly HashSet<string> RegisteredSchemes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly WebKitGtkNative.UriSchemeRequestCallback SchemeRequestCallback = OnUriSchemeRequest;
    private static readonly WebKitGtkNative.DestroyNotify BufferDestroyCallback = FreeBuffer;

    public static void Register(nint webViewHandle, OmniHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (webViewHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(options.ContentRootPath))
            return;

        var contentRoot = Path.GetFullPath(options.ContentRootPath);
        var scheme = options.CustomScheme;
        var context = WebKitGtkNative.WebKitWebContextGetDefault();
        if (context == IntPtr.Zero)
            throw new InvalidOperationException("webkit_web_context_get_default returned a null handle.");

        lock (Gate)
        {
            if (RegisteredSchemes.Add(scheme))
            {
                WebKitGtkNative.WebKitWebContextRegisterUriScheme(
                    context,
                    scheme,
                    SchemeRequestCallback,
                    IntPtr.Zero,
                    IntPtr.Zero);
            }

            WebViewBindings[webViewHandle] = new SchemeBinding(scheme, contentRoot);
        }
    }

    public static void Unregister(nint webViewHandle)
    {
        if (webViewHandle == IntPtr.Zero)
            return;

        lock (Gate)
        {
            WebViewBindings.Remove(webViewHandle);
        }
    }

    private static void OnUriSchemeRequest(IntPtr request, IntPtr userData)
    {
        try
        {
            var requestUriHandle = WebKitGtkNative.WebKitUriSchemeRequestGetUri(request);
            var requestUri = requestUriHandle != IntPtr.Zero
                ? Marshal.PtrToStringUTF8(requestUriHandle)
                : null;

            if (string.IsNullOrWhiteSpace(requestUri))
            {
                FinishTextResponse(request, "Invalid OmniHost URI request.", "text/plain; charset=utf-8");
                return;
            }

            if (!Uri.TryCreate(requestUri, UriKind.Absolute, out var uri))
            {
                FinishTextResponse(request, $"Invalid OmniHost URI: {requestUri}", "text/plain; charset=utf-8");
                return;
            }

            var webView = WebKitGtkNative.WebKitUriSchemeRequestGetWebView(request);
            if (webView == IntPtr.Zero)
            {
                FinishTextResponse(request, $"No WebView associated with URI: {requestUri}", "text/plain; charset=utf-8");
                return;
            }

            SchemeBinding? binding;
            lock (Gate)
            {
                if (!WebViewBindings.TryGetValue(webView, out binding))
                    binding = null;
            }

            if (binding is null || !string.Equals(binding.Scheme, uri.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                FinishTextResponse(request, $"No OmniHost scheme binding found for URI: {requestUri}", "text/plain; charset=utf-8");
                return;
            }

            var fullPath = ResolvePath(binding.ContentRootPath, uri);
            if (!File.Exists(fullPath))
            {
                FinishTextResponse(request, $"OmniHost asset not found: {uri.AbsolutePath}", "text/plain; charset=utf-8");
                return;
            }

            var bytes = File.ReadAllBytes(fullPath);
            FinishBinaryResponse(request, bytes, GetMimeType(fullPath));
        }
        catch (Exception ex)
        {
            FinishTextResponse(request, ex.Message, "text/plain; charset=utf-8");
        }
    }

    private static string ResolvePath(string contentRootPath, Uri uri)
    {
        var relative = uri.AbsolutePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(contentRootPath, relative));

        if (!fullPath.StartsWith(contentRootPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The URI '{uri}' resolves outside the configured content root '{contentRootPath}'.");
        }

        return fullPath;
    }

    private static void FinishTextResponse(IntPtr request, string content, string contentType)
        => FinishBinaryResponse(request, Encoding.UTF8.GetBytes(content), contentType);

    private static void FinishBinaryResponse(IntPtr request, byte[] content, string contentType)
    {
        var buffer = Marshal.AllocHGlobal(content.Length);
        Marshal.Copy(content, 0, buffer, content.Length);

        var stream = WebKitGtkNative.GMemoryInputStreamNewFromData(
            buffer,
            content.Length,
            BufferDestroyCallback);

        if (stream == IntPtr.Zero)
        {
            FreeBuffer(buffer);
            throw new InvalidOperationException("g_memory_input_stream_new_from_data returned a null stream.");
        }

        try
        {
            WebKitGtkNative.WebKitUriSchemeRequestFinish(request, stream, content.Length, contentType);
        }
        finally
        {
            WebKitGtkNative.GObjectUnref(stream);
        }
    }

    private static void FreeBuffer(IntPtr data)
    {
        if (data != IntPtr.Zero)
            Marshal.FreeHGlobal(data);
    }

    private static string GetMimeType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".html" or ".htm" => "text/html; charset=utf-8",
            ".js" => "application/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".txt" => "text/plain; charset=utf-8",
            _ => "application/octet-stream",
        };

    private sealed record SchemeBinding(string Scheme, string ContentRootPath);
}
