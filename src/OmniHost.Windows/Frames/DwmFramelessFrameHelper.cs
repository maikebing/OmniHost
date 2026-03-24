using OmniHost.Windows.Win32;

namespace OmniHost.Windows.Frames;

internal static class DwmFramelessFrameHelper
{
    public static void ApplyCustomFrame(IntPtr hwnd)
    {
        if (!IsDwmCompositionEnabled())
            return;

        var margins = new NativeMethods.MARGINS
        {
            cxLeftWidth = 0,
            cxRightWidth = 0,
            cyTopHeight = 1,
            cyBottomHeight = 0,
        };

        NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);
        NativeMethods.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOMOVE
            | NativeMethods.SWP_NOSIZE
            | NativeMethods.SWP_NOZORDER
            | NativeMethods.SWP_NOACTIVATE
            | NativeMethods.SWP_FRAMECHANGED);
    }

    public static bool TryHandleMessage(
        IntPtr hwnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        out IntPtr result,
        Action<IntPtr>? reapplyAppearance = null)
    {
        if (TryHandleDwmNonClientMessage(hwnd, msg, wParam, lParam, out result))
            return true;

        switch (msg)
        {
            case NativeMethods.WM_NCCALCSIZE:
                if (IsDwmCompositionEnabled())
                {
                    result = IntPtr.Zero;
                    return true;
                }
                break;

            case NativeMethods.WM_NCHITTEST:
                result = FramelessHitTest(hwnd, lParam);
                return true;

            case NativeMethods.WM_ACTIVATE:
            case NativeMethods.WM_DWMCOMPOSITIONCHANGED:
            case NativeMethods.WM_THEMECHANGED:
                ApplyCustomFrame(hwnd);
                reapplyAppearance?.Invoke(hwnd);
                result = IntPtr.Zero;
                return false;
        }

        result = IntPtr.Zero;
        return false;
    }

    private static bool TryHandleDwmNonClientMessage(
        IntPtr hwnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        out IntPtr result)
    {
        result = IntPtr.Zero;

        if (!IsDwmCompositionEnabled())
            return false;

        return NativeMethods.DwmDefWindowProc(hwnd, msg, wParam, lParam, out result) == 0
            && result != IntPtr.Zero;
    }

    private static bool IsDwmCompositionEnabled()
    {
        try
        {
            return NativeMethods.DwmIsCompositionEnabled(out var enabled) == 0 && enabled;
        }
        catch
        {
            return false;
        }
    }

    private static IntPtr FramelessHitTest(IntPtr hwnd, IntPtr lParam)
    {
        if (!NativeMethods.GetWindowRect(hwnd, out var wr))
            return (IntPtr)NativeMethods.HTCLIENT;

        var x = (short)((uint)lParam & 0xFFFFu);
        var y = (short)((uint)lParam >> 16);

        var left = x < wr.left + NativeMethods.ResizeBorderSize;
        var right = x >= wr.right - NativeMethods.ResizeBorderSize;
        var top = y < wr.top + NativeMethods.ResizeBorderSize;
        var bottom = y >= wr.bottom - NativeMethods.ResizeBorderSize;

        if (top && left) return (IntPtr)NativeMethods.HTTOPLEFT;
        if (top && right) return (IntPtr)NativeMethods.HTTOPRIGHT;
        if (bottom && left) return (IntPtr)NativeMethods.HTBOTTOMLEFT;
        if (bottom && right) return (IntPtr)NativeMethods.HTBOTTOMRIGHT;
        if (top) return (IntPtr)NativeMethods.HTTOP;
        if (bottom) return (IntPtr)NativeMethods.HTBOTTOM;
        if (left) return (IntPtr)NativeMethods.HTLEFT;
        if (right) return (IntPtr)NativeMethods.HTRIGHT;

        return (IntPtr)NativeMethods.HTCLIENT;
    }
}
