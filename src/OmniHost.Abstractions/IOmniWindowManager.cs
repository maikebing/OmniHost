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
    /// Returns the live window context for a running window when it is available.
    /// </summary>
    OmniWindowContext? GetWindowContext(string windowId);

    /// <summary>
    /// Opens an additional non-main window while the runtime is active.
    /// </summary>
    void OpenWindow(OmniWindowDefinition window);

    /// <summary>
    /// Requests that a currently open window close.
    /// </summary>
    bool TryCloseWindow(string windowId);

    /// <summary>
    /// Requests that a currently open window become active.
    /// </summary>
    bool TryActivateWindow(string windowId);

    /// <summary>
    /// Posts a host event to one specific running window.
    /// </summary>
    Task<bool> PostEventAsync(string windowId, string eventName, string jsonPayload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a host event to all currently open windows and returns the number of recipients.
    /// </summary>
    Task<int> BroadcastEventAsync(string eventName, string jsonPayload, CancellationToken cancellationToken = default);
}
