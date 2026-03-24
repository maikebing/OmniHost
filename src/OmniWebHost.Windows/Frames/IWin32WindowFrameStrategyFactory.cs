namespace OmniWebHost.Windows.Frames;

internal interface IWin32WindowFrameStrategyFactory
{
    IWin32WindowFrameStrategy Create(OmniWindowStyle windowStyle);
}
