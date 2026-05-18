namespace NativeWebHost.Mac;

/// <summary>
/// macOS WKWebView adapter placeholder.
/// </summary>
public sealed class WKWebViewAdapterFactory : IWebViewAdapterFactory
{
    public string AdapterId => "wkwebview";

    public bool IsAvailable => OperatingSystem.IsMacOS();

    public IWebViewAdapter Create()
        => throw new PlatformNotSupportedException(
            "NativeWebHost.Mac is reserved for the AppKit runtime and WKWebView adapter. The implementation is not available yet.");
}
