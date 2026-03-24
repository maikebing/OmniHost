namespace OmniWebHost;

/// <summary>
/// Abstraction for a pluggable browser-engine adapter.
/// Each adapter wraps a concrete WebView implementation (WebView2, CEF, WKWebView, …).
/// </summary>
public interface IWebViewAdapter : IAsyncDisposable
{
    /// <summary>Unique identifier for this adapter type (e.g. "webview2", "cef", "wkwebview").</summary>
    string AdapterId { get; }

    /// <summary>Capabilities advertised by this adapter.</summary>
    BrowserCapabilities Capabilities { get; }

    /// <summary>
    /// Initialises the WebView and attaches it to the supplied native host surface.
    /// </summary>
    /// <param name="surface">Typed host-surface information such as an HWND, NSView, or GTK widget.</param>
    /// <param name="options">Host configuration options.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task InitializeAsync(
        HostSurfaceDescriptor surface,
        OmniWebHostOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Legacy convenience overload for adapters that still accept a raw native handle.
    /// </summary>
    /// <param name="hostHandle">Platform-specific handle of the host window (HWND on Windows, NSView handle on macOS, etc.).</param>
    /// <param name="options">Host configuration options.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task InitializeAsync(nint hostHandle, OmniWebHostOptions options, CancellationToken cancellationToken = default)
        => InitializeAsync(
            new HostSurfaceDescriptor(HostSurfaceKind.Hwnd, hostHandle),
            options,
            cancellationToken);

    /// <summary>Navigates the WebView to the specified URL.</summary>
    Task NavigateAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the browser viewport bounds to match the current host-window client size.
    /// </summary>
    void Resize(int width, int height);

    /// <summary>Provides access to the JS bridge for this adapter.</summary>
    IJsBridge JsBridge { get; }
}
