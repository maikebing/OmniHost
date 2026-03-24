namespace OmniHost;

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
    /// and <c>omni.window.*</c> bridge methods to implement custom minimize / maximize /
    /// close controls from JavaScript.
    /// </para>
    /// <para>
    /// The CSS variable <c>--omni-window-style</c> is set to <c>"frameless"</c> on the
    /// <c>:root</c> element so page styles can adapt accordingly.
    /// </para>
    /// </summary>
    Frameless = 1,

    /// <summary>
    /// Standard system title bar and border with a DWM blur-glass inspired backdrop
    /// when the host OS exposes a supported public API.
    /// <para>
    /// On newer Windows 11 builds this maps to the system backdrop APIs. On older
    /// or unsupported Windows versions it gracefully falls back to
    /// <see cref="Normal"/>.
    /// </para>
    /// </summary>
    DwmBlurGlass = 2,

    /// <summary>
    /// A frameless, custom-chrome window intended for a VS Code-like HTML title bar.
    /// <para>
    /// This keeps the raw Win32 resize + DWM custom-frame behaviour used by
    /// <see cref="Frameless"/>, while also applying a darker Windows backdrop when
    /// available so the native frame better matches editor-style shells.
    /// </para>
    /// </summary>
    VsCode = 3,
}
