namespace OmniWebHost.Core;

/// <summary>
/// Internal description of a host window to be created and tracked by the coordinator.
/// </summary>
internal sealed record HostWindowDefinition(
    string WindowId,
    OmniWebHostOptions Options,
    bool IsMainWindow);
