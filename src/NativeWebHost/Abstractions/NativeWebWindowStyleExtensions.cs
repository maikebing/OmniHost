namespace NativeWebHost;

/// <summary>
/// Helper mappings for <see cref="NativeWebWindowStyle"/> across host runtimes and adapters.
/// </summary>
public static class NativeWebWindowStyleExtensions
{
    /// <summary>
    /// Converts the window style to a CSS-friendly token exposed to hosted pages.
    /// </summary>
    public static string ToCssToken(this NativeWebWindowStyle windowStyle)
        => windowStyle switch
        {
            NativeWebWindowStyle.Frameless => "frameless",
            NativeWebWindowStyle.DwmBlurGlass => "dwm-blur-glass",
            NativeWebWindowStyle.VsCode => "vscode",
            _ => "normal",
        };

    /// <summary>
    /// Returns <see langword="true"/> when the window style expects a custom HTML drag region.
    /// </summary>
    public static bool UsesCustomDragRegions(this NativeWebWindowStyle windowStyle)
        => windowStyle is NativeWebWindowStyle.Frameless or NativeWebWindowStyle.VsCode;
}
