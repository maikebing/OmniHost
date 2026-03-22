namespace OmniWebHost.WebView2;

/// <summary>
/// Placeholder <see cref="IJsBridge"/> backed by Microsoft WebView2.
/// Full implementation will wrap <c>CoreWebView2.ExecuteScriptAsync</c> and the
/// <c>WebMessageReceived</c> event.
/// </summary>
internal sealed class WebView2JsBridge : IJsBridge
{
    private readonly Dictionary<string, Func<string, Task<string>>> _handlers = new();

    public Task<string?> ExecuteScriptAsync(string script, CancellationToken cancellationToken = default)
    {
        // TODO: delegate to CoreWebView2.ExecuteScriptAsync(script)
        throw new NotImplementedException("WebView2 runtime not yet wired up.");
    }

    public void RegisterHandler(string name, Func<string, Task<string>> handler)
        => _handlers[name] = handler;

    public Task PostMessageAsync(string eventName, string jsonPayload)
    {
        // TODO: delegate to CoreWebView2.PostWebMessageAsJson(...)
        throw new NotImplementedException("WebView2 runtime not yet wired up.");
    }
}
