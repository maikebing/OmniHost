namespace OmniHost.Core;

/// <summary>
/// Immutable snapshot of a currently tracked host window.
/// </summary>
public sealed record HostWindowSnapshot(
    string WindowId,
    string AdapterId,
    bool IsMainWindow,
    OmniHostOptions Options);
