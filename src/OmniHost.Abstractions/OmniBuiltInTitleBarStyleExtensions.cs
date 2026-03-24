namespace OmniHost;

/// <summary>
/// Helper mappings for <see cref="OmniBuiltInTitleBarStyle"/>.
/// </summary>
public static class OmniBuiltInTitleBarStyleExtensions
{
    /// <summary>
    /// Converts the built-in title-bar style to a CSS-friendly token.
    /// </summary>
    public static string ToCssToken(this OmniBuiltInTitleBarStyle style)
        => style switch
        {
            OmniBuiltInTitleBarStyle.VsCode => "vscode",
            OmniBuiltInTitleBarStyle.Office => "office",
            _ => "none",
        };
}
