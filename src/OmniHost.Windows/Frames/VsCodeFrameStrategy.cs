namespace OmniHost.Windows.Frames;

internal sealed class VsCodeFrameStrategy : IWin32WindowFrameStrategy
{
    public string StrategyId => "win32.vscode";

    public (uint Style, uint ExStyle) GetWindowStyles()
        => new DwmCustomFrameStrategy().GetWindowStyles();

    public void OnWindowCreated(IntPtr hwnd)
    {
        DwmFramelessFrameHelper.ApplyCustomFrame(hwnd);
        DwmWindowAppearance.ApplyVsCode(hwnd);
    }

    public bool TryHandleMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, out IntPtr result)
        => DwmFramelessFrameHelper.TryHandleMessage(
            hwnd,
            msg,
            wParam,
            lParam,
            out result,
            DwmWindowAppearance.ApplyVsCode);
}
