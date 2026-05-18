namespace NativeWebHost;

/// <summary>
/// Optional runtime contract for hosting multiple windows in a single NativeWebHost application run.
/// </summary>
public interface IMultiWindowDesktopRuntime : IDesktopRuntime
{
    /// <summary>
    /// Creates the main window and any additional startup windows, then blocks until all windows close.
    /// </summary>
    /// <param name="options">Main-window host configuration.</param>
    /// <param name="additionalWindows">Additional windows to create during startup.</param>
    /// <param name="adapterFactory">Factory used to create WebView adapters.</param>
    /// <param name="desktopApp">Optional application lifecycle callbacks.</param>
    void Run(
        NativeWebHostOptions options,
        IReadOnlyList<NativeWebWindowDefinition> additionalWindows,
        IWebViewAdapterFactory adapterFactory,
        IDesktopApp? desktopApp);
}
