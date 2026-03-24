namespace OmniHost;

/// <summary>
/// Controls the default scrollbar behaviour applied to hosted web content.
/// </summary>
public enum OmniScrollBarMode
{
    /// <summary>
    /// Do not inject any host-level scrollbar CSS. The page controls its own overflow.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Hide both horizontal and vertical scrollbars.
    /// </summary>
    Hidden = 1,

    /// <summary>
    /// Allow only the vertical scrollbar; horizontal overflow is suppressed.
    /// </summary>
    VerticalOnly = 2,

    /// <summary>
    /// Inject custom CSS provided via <see cref="OmniHostOptions.ScrollBarCustomCss"/>.
    /// </summary>
    Custom = 3,
}
