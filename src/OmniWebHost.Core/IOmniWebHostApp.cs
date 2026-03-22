namespace OmniWebHost.Core;

/// <summary>
/// Represents a built OmniWebHost application ready to run.
/// </summary>
public interface IOmniWebHostApp
{
    /// <summary>Starts the desktop application and enters the main event loop.</summary>
    Task RunAsync(CancellationToken cancellationToken = default);
}
