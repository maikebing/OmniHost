namespace NativeWebHost.Windows.Frames;

internal interface IWin32WindowFrameStrategyFactory
{
    IWin32WindowFrameStrategy Create(NativeWebWindowStyle windowStyle);
}
