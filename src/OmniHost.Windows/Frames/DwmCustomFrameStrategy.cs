using OmniHost.Windows.Win32;

namespace OmniHost.Windows.Frames;

internal sealed class DwmCustomFrameStrategy : IWin32WindowFrameStrategy
{
    public string StrategyId => "win32.dwm-custom-frame";

    public (uint Style, uint ExStyle) GetWindowStyles()
        => (NativeMethods.WS_OVERLAPPEDWINDOW
            | NativeMethods.WS_CLIPCHILDREN
            | NativeMethods.WS_CLIPSIBLINGS,
            NativeMethods.WS_EX_APPWINDOW);

    public void OnWindowCreated(IntPtr hwnd) => DwmFramelessFrameHelper.ApplyCustomFrame(hwnd);

    public bool TryHandleMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, out IntPtr result)
        => DwmFramelessFrameHelper.TryHandleMessage(hwnd, msg, wParam, lParam, out result);
}
