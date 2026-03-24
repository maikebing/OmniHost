namespace OmniHost;

/// <summary>
/// Describes a running host window and the services available to it.
/// </summary>
public sealed class OmniWindowContext
{
    /// <summary>
    /// Creates a new running-window context.
    /// </summary>
    public OmniWindowContext(
        string windowId,
        bool isMainWindow,
        OmniHostOptions options,
        IWebViewAdapter adapter,
        IOmniWindowManager windowManager)
    {
        if (string.IsNullOrWhiteSpace(windowId))
            throw new ArgumentException("Window id cannot be null or whitespace.", nameof(windowId));

        WindowId = windowId;
        IsMainWindow = isMainWindow;
        Options = options?.Clone() ?? throw new ArgumentNullException(nameof(options));
        Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        WindowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
    }

    /// <summary>
    /// Gets the stable identifier for the current host window.
    /// </summary>
    public string WindowId { get; }

    /// <summary>
    /// Gets whether this is the main application window.
    /// </summary>
    public bool IsMainWindow { get; }

    /// <summary>
    /// Gets a snapshot of the options used to create this window.
    /// </summary>
    public OmniHostOptions Options { get; }

    /// <summary>
    /// Gets the browser adapter attached to this window.
    /// </summary>
    public IWebViewAdapter Adapter { get; }

    /// <summary>
    /// Gets the current application-wide window manager.
    /// </summary>
    public IOmniWindowManager WindowManager { get; }
}
