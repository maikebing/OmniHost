namespace OmniHost.Core;

/// <summary>
/// Placeholder core implementation of <see cref="IJsBridge"/>.
/// Replace with a real implementation backed by a concrete WebView engine.
/// </summary>
internal sealed class NullJsBridge : IJsBridge
{
    public Task<string?> ExecuteScriptAsync(string script, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    public void RegisterHandler(string name, Func<string, Task<string>> handler) { }

    public Task PostMessageAsync(string eventName, string jsonPayload)
        => Task.CompletedTask;
}
