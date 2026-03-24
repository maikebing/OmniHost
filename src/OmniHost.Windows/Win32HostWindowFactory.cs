using OmniHost.Windows.Frames;

namespace OmniHost.Windows;

internal sealed class Win32HostWindowFactory : IHostWindowFactory
{
    private readonly IWin32WindowFrameStrategyFactory _frameStrategyFactory;

    public HostSurfaceKind SurfaceKind => HostSurfaceKind.Hwnd;

    internal Win32HostWindowFactory()
        : this(new DefaultWin32WindowFrameStrategyFactory())
    {
    }

    internal Win32HostWindowFactory(IWin32WindowFrameStrategyFactory frameStrategyFactory)
    {
        _frameStrategyFactory = frameStrategyFactory
            ?? throw new ArgumentNullException(nameof(frameStrategyFactory));
    }

    public IHostWindow Create(
        OmniHostOptions options,
        IWebViewAdapter adapter,
        IDesktopApp? desktopApp)
        => new Win32HostWindow(
            options,
            adapter,
            desktopApp,
            _frameStrategyFactory.Create(options.WindowStyle));
}
