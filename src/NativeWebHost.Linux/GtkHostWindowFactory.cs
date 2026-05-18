namespace NativeWebHost.Linux;

internal sealed class GtkHostWindowFactory : IHostWindowFactory
{
    public HostSurfaceKind SurfaceKind => HostSurfaceKind.GtkWidget;

    public IHostWindow Create(NativeWebWindowContext windowContext, IDesktopApp? desktopApp)
        => new GtkHostWindow(windowContext, desktopApp);
}
