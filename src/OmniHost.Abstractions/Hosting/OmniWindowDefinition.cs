namespace OmniHost;

/// <summary>
/// Describes an additional startup window to create alongside the main application window.
/// </summary>
public sealed class OmniWindowDefinition
{
    /// <summary>
    /// Creates a new startup-window definition.
    /// </summary>
    public OmniWindowDefinition(string windowId, OmniHostOptions options)
    {
        if (string.IsNullOrWhiteSpace(windowId))
            throw new ArgumentException("Window id cannot be null or whitespace.", nameof(windowId));

        if (string.Equals(windowId, "main", StringComparison.Ordinal))
            throw new ArgumentException("The window id 'main' is reserved for the primary window.", nameof(windowId));

        WindowId = windowId;
        Options = options?.Clone() ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Gets the stable identifier for this window.
    /// </summary>
    public string WindowId { get; }

    /// <summary>
    /// Gets the startup options for this window.
    /// </summary>
    public OmniHostOptions Options { get; }
}
