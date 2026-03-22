namespace OmniWebHost;

/// <summary>
/// Controls the visual style of the native host window chrome.
/// </summary>
public enum OmniWindowStyle
{
    /// <summary>
    /// Standard OS window with a system title bar, caption buttons (min/max/close),
    /// and a resizable border. This is the default.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Frameless window — no system title bar or caption buttons.
    /// The WebView2 content fills the entire client area.
    /// <para>
    /// Use the <c>omni-drag</c> HTML attribute on elements that should act as drag regions,
    /// and <c>window.omni.window.*</c> bridge methods to implement custom minimize / maximize /
    /// close controls from JavaScript.
    /// </para>
    /// <para>
    /// The CSS variable <c>--omni-window-style</c> is set to <c>"frameless"</c> on the
    /// <c>:root</c> element so page styles can adapt accordingly.
    /// </para>
    /// </summary>
    Frameless = 1,
}
