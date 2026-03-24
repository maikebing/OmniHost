namespace OmniWebHost;

/// <summary>
/// Identifies the kind of native host surface exposed to a browser adapter.
/// </summary>
public enum HostSurfaceKind
{
    /// <summary>
    /// Unknown or not yet created.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A Win32 <c>HWND</c>.
    /// </summary>
    Hwnd = 1,

    /// <summary>
    /// A macOS <c>NSView</c>.
    /// </summary>
    NsView = 2,

    /// <summary>
    /// A GTK widget handle.
    /// </summary>
    GtkWidget = 3,

    /// <summary>
    /// An offscreen or texture-backed rendering surface.
    /// </summary>
    Offscreen = 4,
}
