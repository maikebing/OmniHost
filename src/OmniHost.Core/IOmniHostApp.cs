namespace OmniHost.Core;

/// <summary>
/// Represents a built OmniHost application ready to run.
/// </summary>
public interface IOmniHostApp
{
    /// <summary>Starts the desktop application and enters the main event loop.</summary>
    Task RunAsync(CancellationToken cancellationToken = default);
}
