namespace OmniHost;

/// <summary>
/// Global configuration options for an OmniHost application.
/// </summary>
public class OmniHostOptions
{
    /// <summary>The title shown in the host window's title bar.</summary>
    public string Title { get; set; } = "OmniHost App";

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
    /// Host-level scrollbar behaviour applied to hosted pages.
    /// Defaults to <see cref="OmniScrollBarMode.Auto"/>, which leaves overflow styling
    /// entirely up to the page.
    /// </summary>
    public OmniScrollBarMode ScrollBarMode { get; set; } = OmniScrollBarMode.Auto;

    /// <summary>
    /// Custom CSS injected when <see cref="ScrollBarMode"/> is set to
    /// <see cref="OmniScrollBarMode.Custom"/>.
    /// </summary>
    public string? ScrollBarCustomCss { get; set; }

    /// <summary>
    /// Visual style of the native host window chrome.
    /// Defaults to <see cref="OmniWindowStyle.Normal"/> (standard OS title bar).
    /// Set to <see cref="OmniWindowStyle.Frameless"/> to remove the system title bar
    /// and implement a fully custom HTML/CSS chrome.
    /// </summary>
    public OmniWindowStyle WindowStyle { get; set; } = OmniWindowStyle.Normal;

    /// <summary>
    /// Creates a detached copy of the current options instance.
    /// </summary>
    public OmniHostOptions Clone()
        => new()
        {
            Title = Title,
            StartUrl = StartUrl,
            Width = Width,
            Height = Height,
            StartMaximized = StartMaximized,
            EnableDevTools = EnableDevTools,
            CustomScheme = CustomScheme,
            ContentRootPath = ContentRootPath,
            UserDataFolder = UserDataFolder,
            AdapterSettings = new Dictionary<string, string>(AdapterSettings, StringComparer.Ordinal),
            ScrollBarMode = ScrollBarMode,
            ScrollBarCustomCss = ScrollBarCustomCss,
            WindowStyle = WindowStyle,
        };
}
