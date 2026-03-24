using OmniHost;
using OmniHost.Windows;

namespace OmniHost.WinForms;

/// <summary>
/// Windows Forms-based runtime for applications that prefer a WinForms host window
/// instead of the raw Win32 window implementation.
/// </summary>
public sealed class WinFormsRuntime : IMultiWindowDesktopRuntime
{
    private readonly Win32Runtime _innerRuntime = new(new WinFormsHostWindowFactory());

    /// <inheritdoc/>
    public void Run(
        OmniHostOptions options,
        IWebViewAdapterFactory adapterFactory,
        IDesktopApp? desktopApp)
        => _innerRuntime.Run(options, adapterFactory, desktopApp);

    /// <inheritdoc/>
    public void Run(
        OmniHostOptions options,
        IReadOnlyList<OmniWindowDefinition> additionalWindows,
        IWebViewAdapterFactory adapterFactory,
        IDesktopApp? desktopApp)
        => _innerRuntime.Run(options, additionalWindows, adapterFactory, desktopApp);
}
