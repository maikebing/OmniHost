namespace OmniHost;

/// <summary>
/// Represents a concrete native host window created by a platform runtime.
/// </summary>
public interface IHostWindow
{
    /// <summary>
    /// Gets the native surface exposed by this window for browser attachment.
    /// </summary>
    HostSurfaceDescriptor Surface { get; }

    /// <summary>
    /// Creates the native window, runs its event loop, and blocks until it closes.
    /// </summary>
    void Run();

    /// <summary>
    /// Requests that the host window begin its normal close flow.
    /// </summary>
    void RequestClose();

    /// <summary>
    /// Requests that the host window become the active foreground window.
    /// </summary>
    void RequestActivate();
}
