namespace OmniWebHost;

/// <summary>
/// Abstraction for the bidirectional JavaScript bridge between the host and the WebView page.
/// </summary>
public interface IJsBridge
{
    /// <summary>Executes a JavaScript expression in the WebView and returns the result as a string.</summary>
    /// <param name="script">JavaScript expression or statement block to evaluate.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task<string?> ExecuteScriptAsync(string script, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a host-side handler that can be invoked by JavaScript running in the WebView.
    /// </summary>
    /// <param name="name">The name exposed to JavaScript (e.g. <c>omni.&lt;name&gt;</c>).</param>
    /// <param name="handler">Async delegate that receives the JSON payload sent from JS and returns a JSON response.</param>
    void RegisterHandler(string name, Func<string, Task<string>> handler);

    /// <summary>Sends a named message with a JSON payload to the WebView page.</summary>
    /// <param name="eventName">Event name that JavaScript listeners can subscribe to.</param>
    /// <param name="jsonPayload">JSON-serialisable payload.</param>
    Task PostMessageAsync(string eventName, string jsonPayload);
}
