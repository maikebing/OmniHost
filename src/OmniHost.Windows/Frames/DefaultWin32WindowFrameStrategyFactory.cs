namespace OmniHost.Windows.Frames;

internal sealed class DefaultWin32WindowFrameStrategyFactory : IWin32WindowFrameStrategyFactory
{
    public IWin32WindowFrameStrategy Create(OmniWindowStyle windowStyle)
        => windowStyle switch
        {
            OmniWindowStyle.Frameless => new DwmCustomFrameStrategy(),
            _ => new SystemFrameStrategy(),
        };
}
