using OmniHost.Windows.Win32;

namespace OmniHost.Windows.Frames;

internal static class DwmWindowAppearance
{
    private const uint ColorNone = 0xFFFFFFFEu;

    public static void ApplyBlurGlass(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;

        TrySetCornerPreference(hwnd, NativeMethods.DWMWCP_ROUND);

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
        {
            TrySetSystemBackdrop(hwnd, NativeMethods.DWMSBT_TRANSIENTWINDOW);
            return;
        }

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            TrySetSystemBackdrop(hwnd, NativeMethods.DWMSBT_MAINWINDOW);
        }
    }

    public static void ApplyVsCode(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;

        TryEnableImmersiveDarkMode(hwnd, enabled: true);
        TrySetCornerPreference(hwnd, NativeMethods.DWMWCP_ROUND);
        TrySetBorderColor(hwnd, ColorNone);
        TrySetCaptionColor(hwnd, MakeColorRef(0x20, 0x22, 0x25));
        TrySetTextColor(hwnd, MakeColorRef(0xF3, 0xF3, 0xF3));

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
        {
            TrySetSystemBackdrop(hwnd, NativeMethods.DWMSBT_TABBEDWINDOW);
            return;
        }

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            TrySetSystemBackdrop(hwnd, NativeMethods.DWMSBT_MAINWINDOW);
        }
    }

    private static void TryEnableImmersiveDarkMode(IntPtr hwnd, bool enabled)
    {
        var enabledValue = enabled ? 1 : 0;
        TrySetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, enabledValue);
    }

    private static void TrySetSystemBackdrop(IntPtr hwnd, int backdropType)
        => TrySetWindowAttribute(hwnd, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, backdropType);

    private static void TrySetCornerPreference(IntPtr hwnd, uint cornerPreference)
        => TrySetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, cornerPreference);

    private static void TrySetBorderColor(IntPtr hwnd, uint colorRef)
        => TrySetWindowAttribute(hwnd, NativeMethods.DWMWA_BORDER_COLOR, colorRef);

    private static void TrySetCaptionColor(IntPtr hwnd, uint colorRef)
        => TrySetWindowAttribute(hwnd, NativeMethods.DWMWA_CAPTION_COLOR, colorRef);

    private static void TrySetTextColor(IntPtr hwnd, uint colorRef)
        => TrySetWindowAttribute(hwnd, NativeMethods.DWMWA_TEXT_COLOR, colorRef);

    private static void TrySetWindowAttribute(IntPtr hwnd, uint attribute, int value)
    {
        try
        {
            NativeMethods.DwmSetWindowAttribute(
                hwnd,
                attribute,
                ref value,
                sizeof(int));
        }
        catch
        {
        }
    }

    private static void TrySetWindowAttribute(IntPtr hwnd, uint attribute, uint value)
    {
        try
        {
            NativeMethods.DwmSetWindowAttribute(
                hwnd,
                attribute,
                ref value,
                sizeof(uint));
        }
        catch
        {
        }
    }

    private static uint MakeColorRef(byte red, byte green, byte blue)
        => (uint)(red | (green << 8) | (blue << 16));
}
