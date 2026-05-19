using System.Collections.Concurrent;
using Android.Webkit;

namespace NativeWebHost.Android;

internal static class AndroidHostSurfaceRegistry
{
    private static readonly ConcurrentDictionary<nint, WebView> WebViews = new();
    private static int _nextId;

    public static HostSurfaceDescriptor Register(WebView webView)
    {
        ArgumentNullException.ThrowIfNull(webView);
        var id = (nint)Interlocked.Increment(ref _nextId);
        WebViews[id] = webView;
        return new HostSurfaceDescriptor(
            HostSurfaceKind.AndroidView,
            id,
            Math.Max(1, webView.Width),
            Math.Max(1, webView.Height));
    }

    public static void Unregister(HostSurfaceDescriptor surface)
    {
        if (surface.Kind == HostSurfaceKind.AndroidView && surface.Handle != 0)
            WebViews.TryRemove(surface.Handle, out _);
    }

    public static WebView Resolve(HostSurfaceDescriptor surface)
    {
        if (surface.Kind != HostSurfaceKind.AndroidView)
            throw new NotSupportedException(
                $"Android WebView hosting only supports {HostSurfaceKind.AndroidView} host surfaces.");

        if (surface.Handle == 0 || !WebViews.TryGetValue(surface.Handle, out var webView))
            throw new InvalidOperationException("The Android host surface has not been registered.");

        return webView;
    }
}
