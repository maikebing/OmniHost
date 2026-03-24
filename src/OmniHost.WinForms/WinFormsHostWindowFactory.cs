using OmniHost;

namespace OmniHost.WinForms;

internal sealed class WinFormsHostWindowFactory : IHostWindowFactory
{
    public HostSurfaceKind SurfaceKind => HostSurfaceKind.Hwnd;

    public IHostWindow Create(OmniWindowContext windowContext, IDesktopApp? desktopApp)
        => new WinFormsHostWindow(windowContext, desktopApp);
}
