namespace NativeWebHost.Mac;

/// <summary>
/// macOS native runtime placeholder for the AppKit host-window implementation.
/// </summary>
public sealed class MacRuntime : IMultiWindowDesktopRuntime
{
    public void Run(
        NativeWebHostOptions options,
        IWebViewAdapterFactory adapterFactory,
        IDesktopApp? desktopApp)
        => throw new PlatformNotSupportedException(
            "NativeWebHost.Mac is reserved for the AppKit runtime and WKWebView adapter. The implementation is not available yet.");

    public void Run(
        NativeWebHostOptions options,
        IReadOnlyList<NativeWebWindowDefinition> additionalWindows,
        IWebViewAdapterFactory adapterFactory,
        IDesktopApp? desktopApp)
        => throw new PlatformNotSupportedException(
            "NativeWebHost.Mac is reserved for the AppKit runtime and WKWebView adapter. The implementation is not available yet.");
}
