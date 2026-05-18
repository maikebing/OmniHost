namespace NativeWebHost;

/// <summary>
/// Abstraction for the platform-specific desktop runtime (message loop + window host).
/// Implement this interface to support a native OS runtime such as Win32, GTK, or AppKit.
/// </summary>
public interface IDesktopRuntime
{
    /// <summary>
    /// Creates the host window, initialises the adapter, and runs the application message loop.
    /// This method blocks until the user closes the window.
    /// </summary>
    /// <param name="options">Host configuration.</param>
    /// <param name="adapterFactory">Factory used to create the WebView adapter.</param>
    /// <param name="desktopApp">Optional application lifecycle callbacks.</param>
    void Run(NativeWebHostOptions options, IWebViewAdapterFactory adapterFactory, IDesktopApp? desktopApp);
}
