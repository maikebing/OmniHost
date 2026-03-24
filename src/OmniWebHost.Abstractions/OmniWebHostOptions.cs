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

    /// <summary>Whether the host window should start maximized.</summary>
    public bool StartMaximized { get; set; }

    /// <summary>Whether browser DevTools are enabled.</summary>
    public bool EnableDevTools { get; set; }

    /// <summary>
    /// Custom URI scheme name used to serve local assets (e.g. <c>"app"</c> → <c>app://localhost/</c>).
    /// Defaults to <c>"app"</c>.
    /// </summary>
    public string CustomScheme { get; set; } = "app";

    /// <summary>
    /// Root directory on disk that is mapped to <see cref="CustomScheme"/>://.
    /// When <see langword="null"/> the custom scheme is not registered.
    /// </summary>
    public string? ContentRootPath { get; set; }

    /// <summary>
    /// Directory used to store the browser profile / user data.
    /// When <see langword="null"/> a temporary folder is used.
    /// </summary>
    public string? UserDataFolder { get; set; }

    /// <summary>Additional adapter-specific settings as key-value pairs.</summary>
    public Dictionary<string, string> AdapterSettings { get; set; } = new();

    /// <summary>
    /// Visual style of the native host window chrome.
    /// Defaults to <see cref="OmniWindowStyle.Normal"/> (standard OS title bar).
    /// Set to <see cref="OmniWindowStyle.Frameless"/> to remove the system title bar
    /// and implement a fully custom HTML/CSS chrome.
    /// </summary>
    public OmniWindowStyle WindowStyle { get; set; } = OmniWindowStyle.Normal;
}

