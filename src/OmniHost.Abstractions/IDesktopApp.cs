namespace OmniHost;

/// <summary>
/// Represents the top-level desktop application managed by OmniHost.
/// Implement this interface to customise application lifecycle and window behaviour.
/// In multi-window runs, these callbacks are invoked once per created host window.
/// Implement <see cref="IWindowAwareDesktopApp"/> instead when you need window ids,
/// window-manager access, or other per-window context.
/// </summary>
public interface IDesktopApp
{
    /// <summary>
    /// Called after a host window and WebView adapter have been initialized,
    /// but before that window's message loop starts.
    /// The supplied cancellation token is cancelled when that specific host window begins closing.
    /// </summary>
    Task OnStartAsync(IWebViewAdapter adapter, CancellationToken cancellationToken = default);

    /// <summary>Called when a host window receives a close request.</summary>
    Task OnClosingAsync(CancellationToken cancellationToken = default);
}
