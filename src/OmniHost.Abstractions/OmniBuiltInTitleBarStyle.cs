namespace OmniHost;

/// <summary>
/// Built-in title-bar presets rendered by the host itself instead of by page HTML.
/// </summary>
public enum OmniBuiltInTitleBarStyle
{
    /// <summary>
    /// No built-in title bar. The page can render its own chrome if needed.
    /// </summary>
    None = 0,

    /// <summary>
    /// Editor-style title bar inspired by Visual Studio Code.
    /// </summary>
    VsCode = 1,

    /// <summary>
    /// Productivity-suite title bar inspired by Microsoft Office.
    /// </summary>
    Office = 2,
}
