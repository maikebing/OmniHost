namespace OmniHost;

/// <summary>
/// Helper methods and constants for coordinating a dedicated splash screen window.
/// </summary>
public static class OmniSplashScreen
{
    /// <summary>
    /// Default startup window id used by <see cref="OmniHostBuilder.UseSplashScreen(Action{OmniHostOptions})"/>.
    /// </summary>
    public const string DefaultWindowId = "splash";

    /// <summary>
    /// Default JS bridge handler name that closes the configured splash window.
    /// Call it from JavaScript via <c>omni.invoke("splash.close")</c>.
    /// </summary>
    public const string CloseHandlerName = "splash.close";

    /// <summary>
    /// Host event posted to the splash window when the main window finishes its host-side startup callback.
    /// </summary>
    public const string MainWindowStartedEventName = "splash.mainWindowStarted";

    /// <summary>
    /// Requests that the configured splash window close.
    /// </summary>
    public static bool TryClose(
        IOmniWindowManager windowManager,
        string windowId = DefaultWindowId)
    {
        ArgumentNullException.ThrowIfNull(windowManager);
        return windowManager.TryCloseWindow(windowId);
    }

    /// <summary>
    /// Requests that the configured splash window close using the current window context.
    /// </summary>
    public static bool TryClose(
        OmniWindowContext window,
        string windowId = DefaultWindowId)
    {
        ArgumentNullException.ThrowIfNull(window);
        return TryClose(window.WindowManager, windowId);
    }
}
