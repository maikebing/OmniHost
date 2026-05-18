using NativeWebHost.Windows.Frames;

namespace NativeWebHost.Windows;

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
        NativeWebWindowContext windowContext,
        IDesktopApp? desktopApp)
        => new Win32HostWindow(
            windowContext,
            desktopApp,
            _frameStrategyFactory.Create(windowContext.Options.WindowStyle));
}
