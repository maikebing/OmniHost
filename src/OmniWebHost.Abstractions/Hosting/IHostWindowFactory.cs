namespace OmniWebHost;

/// <summary>
/// Creates platform-specific host windows for a runtime.
/// </summary>
public interface IHostWindowFactory
{
    /// <summary>
    /// Gets the host-surface kind produced by windows created from this factory.
    /// </summary>
    HostSurfaceKind SurfaceKind { get; }

    /// <summary>
    /// Creates a host window that will own the browser attachment surface.
    /// </summary>
    IHostWindow Create(
        OmniWebHostOptions options,
        IWebViewAdapter adapter,
        IDesktopApp? desktopApp);
}
