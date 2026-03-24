using OmniHost.Windows.Win32;

namespace OmniHost.Windows.Frames;

internal interface IWin32WindowFrameStrategy : IWindowFrameStrategy
{
    (uint Style, uint ExStyle) GetWindowStyles();

    void OnWindowCreated(IntPtr hwnd);

    bool TryHandleMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, out IntPtr result);
}
