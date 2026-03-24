namespace OmniWebHost;

/// <summary>
/// Describes the capabilities reported by a browser adapter at runtime.
/// </summary>
public class BrowserCapabilities
{
    /// <summary>Human-readable name of the underlying browser engine (e.g. "WebView2", "CEF", "WKWebView").</summary>
    public string EngineName { get; init; } = string.Empty;

    /// <summary>Version string of the browser engine.</summary>
    public string EngineVersion { get; init; } = string.Empty;

    /// <summary>Whether the engine supports executing JavaScript and receiving results.</summary>
    public bool SupportsJavaScript { get; init; }

    /// <summary>Whether the engine supports bidirectional JS-to-host messaging.</summary>
    public bool SupportsJsBridge { get; init; }

    /// <summary>Whether the engine can intercept and handle custom URI schemes.</summary>
    public bool SupportsCustomSchemes { get; init; }

    /// <summary>Whether the engine supports DevTools / remote debugging.</summary>
    public bool SupportsDevTools { get; init; }

    /// <summary>
    /// Native host-surface kinds this adapter can attach to.
    /// </summary>
    public IReadOnlyCollection<HostSurfaceKind> SupportedHostSurfaces { get; init; }
        = Array.Empty<HostSurfaceKind>();
}
