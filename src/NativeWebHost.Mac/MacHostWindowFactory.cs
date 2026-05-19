namespace NativeWebHost.Mac;

internal sealed class MacHostWindowFactory : IHostWindowFactory
{
    public HostSurfaceKind SurfaceKind => HostSurfaceKind.NsView;

    public IHostWindow Create(
        NativeWebWindowContext windowContext,
        IDesktopApp? desktopApp)
        => new MacHostWindow(windowContext, desktopApp);
}
