namespace NativeWebHost;

/// <summary>
/// Helper mappings for <see cref="NativeWebBuiltInTitleBarStyle"/>.
/// </summary>
public static class NativeWebBuiltInTitleBarStyleExtensions
{
    /// <summary>
    /// Converts the built-in title-bar style to a CSS-friendly token.
    /// </summary>
    public static string ToCssToken(this NativeWebBuiltInTitleBarStyle style)
        => style switch
        {
            NativeWebBuiltInTitleBarStyle.VsCode => "vscode",
            NativeWebBuiltInTitleBarStyle.Office => "office",
            _ => "none",
        };
}
