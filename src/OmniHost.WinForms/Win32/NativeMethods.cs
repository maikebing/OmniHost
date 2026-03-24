using System.Runtime.InteropServices;

namespace OmniHost.WinForms.Win32;

internal static class NativeMethods
{
    internal const uint WM_NCLBUTTONDOWN = 0x00A1u;
    internal const uint WM_SYSCOMMAND = 0x0112u;
    internal const uint WM_CLOSE = 0x0010u;
    internal const uint WM_NULL = 0x0000u;

    internal const uint SC_MINIMIZE = 0xF020u;
    internal const uint SC_MAXIMIZE = 0xF030u;
    internal const uint SC_RESTORE = 0xF120u;

    internal const nint HTCAPTION = 2;

    internal const uint TPM_LEFTALIGN = 0x0000u;
    internal const uint TPM_RETURNCMD = 0x0100u;
    internal const uint TPM_RIGHTBUTTON = 0x0002u;

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int x;
        public int y;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern bool ReleaseCapture();

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern IntPtr SendMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern uint TrackPopupMenuEx(
        IntPtr hmenu,
        uint uFlags,
        int x,
        int y,
        IntPtr hwnd,
        IntPtr lptpm);

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);
}
