namespace OmniWebHost.WebView2;

/// <summary>
/// Factory that produces <see cref="WebView2Adapter"/> instances.
/// Register this factory via DI or pass directly to <see cref="OmniWebHostBuilder.UseAdapter"/>.
/// </summary>
public sealed class WebView2AdapterFactory : IWebViewAdapterFactory
{
    public string AdapterId => "webview2";

    /// <summary>
    /// Returns <see langword="true"/> when the WebView2 runtime is available on this machine.
    /// Full runtime detection will be implemented in a future step.
    /// </summary>
    public bool IsAvailable =>
        OperatingSystem.IsWindows(); // TODO: also check for installed WebView2 runtime

    public IWebViewAdapter Create() => new WebView2Adapter();
}
