namespace NativeWebHost.Mac;

/// <summary>
/// Factory that produces <see cref="WKWebViewAdapter"/> instances for macOS AppKit hosts.
/// </summary>
public sealed class WKWebViewAdapterFactory : IWebViewAdapterFactory
{
    public string AdapterId => "wkwebview";

    public bool IsAvailable => OperatingSystem.IsMacOS();

    public IWebViewAdapter Create() => new WKWebViewAdapter();
}
