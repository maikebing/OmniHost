namespace OmniHost;

/// <summary>
/// Helper mappings for <see cref="OmniWindowStyle"/> across host runtimes and adapters.
/// </summary>
public static class OmniWindowStyleExtensions
{
    /// <summary>
    /// Converts the window style to a CSS-friendly token exposed to hosted pages.
    /// </summary>
    public static string ToCssToken(this OmniWindowStyle windowStyle)
        => windowStyle switch
        {
            OmniWindowStyle.Frameless => "frameless",
            OmniWindowStyle.DwmBlurGlass => "dwm-blur-glass",
            OmniWindowStyle.VsCode => "vscode",
            _ => "normal",
        };

    /// <summary>
    /// Returns <see langword="true"/> when the window style expects a custom HTML drag region.
    /// </summary>
    public static bool UsesCustomDragRegions(this OmniWindowStyle windowStyle)
        => windowStyle is OmniWindowStyle.Frameless or OmniWindowStyle.VsCode;
}
