namespace NativeWebHost.Windows.Frames;

internal sealed class DefaultWin32WindowFrameStrategyFactory : IWin32WindowFrameStrategyFactory
{
    public IWin32WindowFrameStrategy Create(NativeWebWindowStyle windowStyle)
        => windowStyle switch
        {
            NativeWebWindowStyle.Frameless => new DwmCustomFrameStrategy(),
            NativeWebWindowStyle.DwmBlurGlass => new DwmBlurGlassFrameStrategy(),
            NativeWebWindowStyle.VsCode => new VsCodeFrameStrategy(),
            _ => new SystemFrameStrategy(),
        };
}
