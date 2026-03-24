namespace OmniHost.Gtk;

internal sealed class GtkHostWindowFactory : IHostWindowFactory
{
    public HostSurfaceKind SurfaceKind => HostSurfaceKind.GtkWidget;

    public IHostWindow Create(OmniWindowContext windowContext, IDesktopApp? desktopApp)
        => new GtkHostWindow(windowContext, desktopApp);
}
