namespace NativeWebHost;

/// <summary>
/// Represents a built NativeWebHost application ready to run.
/// </summary>
public interface INativeWebHostApp
{
    /// <summary>Starts the desktop application and enters the main event loop.</summary>
    Task RunAsync(CancellationToken cancellationToken = default);
}
