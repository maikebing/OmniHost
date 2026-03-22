namespace OmniWebHost.WebView2;

/// <summary>
/// Placeholder <see cref="IWebViewAdapter"/> that will wrap Microsoft WebView2.
/// Actual CoreWebView2 initialisation is deferred until a future implementation step.
/// </summary>
public sealed class WebView2Adapter : IWebViewAdapter
{
    public string AdapterId => "webview2";

    public BrowserCapabilities Capabilities { get; } = new BrowserCapabilities
    {
        EngineName = "WebView2",
        EngineVersion = "0.0.0-placeholder",
        SupportsJavaScript = true,
        SupportsJsBridge = true,
        SupportsCustomSchemes = true,
        SupportsDevTools = true,
    };

    public IJsBridge JsBridge { get; } = new WebView2JsBridge();

    public Task InitializeAsync(nint hostHandle, OmniWebHostOptions options, CancellationToken cancellationToken = default)
    {
        // TODO: create CoreWebView2Environment + CoreWebView2Controller for hostHandle
        return Task.CompletedTask;
    }

    public Task NavigateAsync(string url, CancellationToken cancellationToken = default)
    {
        // TODO: call CoreWebView2.Navigate(url)
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        // TODO: dispose CoreWebView2Controller
        return ValueTask.CompletedTask;
    }
}
