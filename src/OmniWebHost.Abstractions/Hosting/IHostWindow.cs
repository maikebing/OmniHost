namespace OmniWebHost;

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
}
