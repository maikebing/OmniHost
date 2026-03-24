namespace OmniHost;

/// <summary>
/// Manages the currently running host windows for an OmniHost application.
/// </summary>
public interface IOmniWindowManager
{
    /// <summary>
    /// Gets the number of currently open host windows.
    /// </summary>
    int OpenWindowCount { get; }

    /// <summary>
    /// Gets the identifier of the main host window when one is currently open.
    /// </summary>
    string? MainWindowId { get; }

    /// <summary>
    /// Returns a snapshot of the currently open window identifiers.
    /// </summary>
    IReadOnlyCollection<string> GetOpenWindowIds();

    /// <summary>
    /// Returns a snapshot of the currently open windows.
    /// </summary>
    IReadOnlyCollection<HostWindowSnapshot> GetOpenWindows();

    /// <summary>
    /// Opens an additional non-main window while the runtime is active.
    /// </summary>
    void OpenWindow(OmniWindowDefinition window);

    /// <summary>
    /// Requests that a currently open window close.
    /// </summary>
    bool TryCloseWindow(string windowId);
}
