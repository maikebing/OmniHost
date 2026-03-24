using OmniHost.Windows.Win32;

namespace OmniHost.Windows.Frames;

internal sealed class SystemFrameStrategy : IWin32WindowFrameStrategy
{
    public string StrategyId => "win32.system";

    public (uint Style, uint ExStyle) GetWindowStyles()
        => (NativeMethods.WS_OVERLAPPEDWINDOW
            | NativeMethods.WS_CLIPCHILDREN
            | NativeMethods.WS_CLIPSIBLINGS,
            0u);

    public void OnWindowCreated(IntPtr hwnd)
    {
    }

    public bool TryHandleMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, out IntPtr result)
    {
        result = IntPtr.Zero;
        return false;
    }
}
