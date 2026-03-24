namespace OmniHost;

/// <summary>
/// Optional desktop-app contract that receives full window context for each host window.
/// </summary>
public interface IWindowAwareDesktopApp : IDesktopApp
{
    /// <summary>
    /// Called after a host window and its browser adapter have been initialized.
    /// </summary>
    Task OnWindowStartAsync(OmniWindowContext window, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when a host window begins closing.
    /// </summary>
    Task OnWindowClosingAsync(OmniWindowContext window, CancellationToken cancellationToken = default);
}
