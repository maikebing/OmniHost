using System.Runtime.InteropServices;

namespace OmniHost.Gtk.Gtk;

internal static class GtkNative
{
    private const string GtkLib = "libgtk-3.so.0";
    private const string GObjectLib = "libgobject-2.0.so.0";
    private const string GLibLib = "libglib-2.0.so.0";

    internal const int GtkWindowToplevel = 0;

    [StructLayout(LayoutKind.Sequential)]
    internal struct GtkAllocation
    {
        public int x;
        public int y;
        public int width;
        public int height;
    }

    internal delegate int IdleCallback(IntPtr data);
    internal delegate int DeleteEventCallback(IntPtr widget, IntPtr eventData, IntPtr userData);
    internal delegate void DestroyCallback(IntPtr widget, IntPtr userData);
    internal delegate void SizeAllocateCallback(IntPtr widget, IntPtr allocation, IntPtr userData);

    [DllImport(GtkLib, EntryPoint = "gtk_init_check")]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool GtkInitCheck(ref int argc, ref IntPtr argv);

    [DllImport(GtkLib, EntryPoint = "gtk_window_new")]
    internal static extern IntPtr GtkWindowNew(int windowType);

    [DllImport(GtkLib, EntryPoint = "gtk_fixed_new")]
    internal static extern IntPtr GtkFixedNew();

    [DllImport(GtkLib, EntryPoint = "gtk_container_add")]
    internal static extern void GtkContainerAdd(IntPtr container, IntPtr widget);

    [DllImport(GtkLib, EntryPoint = "gtk_widget_show_all")]
    internal static extern void GtkWidgetShowAll(IntPtr widget);

    [DllImport(GtkLib, EntryPoint = "gtk_widget_destroy")]
    internal static extern void GtkWidgetDestroy(IntPtr widget);

    [DllImport(GtkLib, EntryPoint = "gtk_window_set_title")]
    internal static extern void GtkWindowSetTitle(IntPtr window, string title);

    [DllImport(GtkLib, EntryPoint = "gtk_window_set_default_size")]
    internal static extern void GtkWindowSetDefaultSize(IntPtr window, int width, int height);

    [DllImport(GtkLib, EntryPoint = "gtk_window_maximize")]
    internal static extern void GtkWindowMaximize(IntPtr window);

    [DllImport(GtkLib, EntryPoint = "gtk_window_present")]
    internal static extern void GtkWindowPresent(IntPtr window);

    [DllImport(GLibLib, EntryPoint = "g_main_loop_new")]
    internal static extern IntPtr GMainLoopNew(IntPtr context, [MarshalAs(UnmanagedType.I1)] bool isRunning);

    [DllImport(GLibLib, EntryPoint = "g_main_loop_run")]
    internal static extern void GMainLoopRun(IntPtr loop);

    [DllImport(GLibLib, EntryPoint = "g_main_loop_quit")]
    internal static extern void GMainLoopQuit(IntPtr loop);

    [DllImport(GLibLib, EntryPoint = "g_main_loop_unref")]
    internal static extern void GMainLoopUnref(IntPtr loop);

    [DllImport(GLibLib, EntryPoint = "g_idle_add")]
    internal static extern uint GIdleAdd(IntPtr function, IntPtr data);

    [DllImport(GObjectLib, EntryPoint = "g_signal_connect_data")]
    internal static extern ulong GSignalConnectData(
        IntPtr instance,
        string detailedSignal,
        IntPtr callback,
        IntPtr data,
        IntPtr destroyData,
        int connectFlags);
}
