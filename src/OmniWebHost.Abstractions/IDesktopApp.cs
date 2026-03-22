namespace OmniWebHost;

/// <summary>
/// Represents the top-level desktop application managed by OmniWebHost.
/// Implement this interface to customise application lifecycle and window behaviour.
/// </summary>
public interface IDesktopApp
{
    /// <summary>
    /// Called after the host window and WebView adapter have been initialised,
    /// but before the main message loop starts.
    /// </summary>
    Task OnStartAsync(IWebViewAdapter adapter, CancellationToken cancellationToken = default);

    /// <summary>Called when the user requests the application to close.</summary>
    Task OnClosingAsync(CancellationToken cancellationToken = default);
}
