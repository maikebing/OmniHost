namespace OmniWebHost;

/// <summary>
/// Describes the native surface a browser adapter should attach to.
/// </summary>
public sealed record HostSurfaceDescriptor(
    HostSurfaceKind Kind,
    nint Handle,
    int Width = 0,
    int Height = 0)
{
    /// <summary>
    /// Returns <see langword="true"/> when the descriptor points at a created native surface.
    /// </summary>
    public bool IsCreated => Handle != 0;
}
