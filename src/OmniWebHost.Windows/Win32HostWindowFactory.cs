namespace OmniWebHost.Windows;

internal sealed class Win32HostWindowFactory : IHostWindowFactory
{
    public IHostWindow Create(
        OmniWebHostOptions options,
        IWebViewAdapter adapter,
        IDesktopApp? desktopApp)
        => new Win32HostWindow(options, adapter, desktopApp);
}
