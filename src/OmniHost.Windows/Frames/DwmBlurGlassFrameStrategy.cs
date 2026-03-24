using OmniHost.Windows.Win32;

namespace OmniHost.Windows.Frames;

internal sealed class DwmBlurGlassFrameStrategy : IWin32WindowFrameStrategy
{
    public string StrategyId => "win32.dwm-blur-glass";

    public (uint Style, uint ExStyle) GetWindowStyles()
        => (NativeMethods.WS_OVERLAPPEDWINDOW
            | NativeMethods.WS_CLIPCHILDREN
            | NativeMethods.WS_CLIPSIBLINGS,
            0u);

    public void OnWindowCreated(IntPtr hwnd)
        => DwmWindowAppearance.ApplyBlurGlass(hwnd);

    public bool TryHandleMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, out IntPtr result)
    {
        switch (msg)
        {
            case NativeMethods.WM_ACTIVATE:
            case NativeMethods.WM_DWMCOMPOSITIONCHANGED:
            case NativeMethods.WM_THEMECHANGED:
                DwmWindowAppearance.ApplyBlurGlass(hwnd);
                break;
        }

        result = IntPtr.Zero;
        return false;
    }
}
