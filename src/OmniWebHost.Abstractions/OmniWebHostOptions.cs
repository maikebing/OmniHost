namespace OmniWebHost;

/// <summary>
/// Global configuration options for an OmniWebHost application.
/// </summary>
public class OmniWebHostOptions
{
    /// <summary>The title shown in the host window's title bar.</summary>
    public string Title { get; set; } = "OmniWebHost App";

    /// <summary>The initial URL loaded when the application starts.</summary>
    public string StartUrl { get; set; } = "about:blank";

    /// <summary>Initial width of the host window in device-independent pixels.</summary>
    public int Width { get; set; } = 1280;

    /// <summary>Initial height of the host window in device-independent pixels.</summary>
    public int Height { get; set; } = 800;

    /// <summary>Whether the host window should start maximised.</summary>
    public bool StartMaximized { get; set; }

    /// <summary>Whether browser DevTools are enabled.</summary>
    public bool EnableDevTools { get; set; }

    /// <summary>Additional adapter-specific settings as key-value pairs.</summary>
    public Dictionary<string, string> AdapterSettings { get; set; } = new();
}
