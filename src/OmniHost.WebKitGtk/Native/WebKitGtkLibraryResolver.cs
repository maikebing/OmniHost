using System.Reflection;
using System.Runtime.InteropServices;

namespace OmniHost.WebKitGtk.Native;

internal static class WebKitGtkLibraryResolver
{
    private static readonly Assembly Assembly = typeof(WebKitGtkLibraryResolver).Assembly;

    private static readonly IReadOnlyDictionary<string, string[]> CandidateLibraries =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["webkit2gtk"] =
            [
                "libwebkit2gtk-4.1.so.0",
                "libwebkit2gtk-4.0.so.37",
                "libwebkit2gtk-4.0.so.0",
            ],
            ["javascriptcoregtk"] =
            [
                "libjavascriptcoregtk-4.1.so.0",
                "libjavascriptcoregtk-4.0.so.18",
                "libjavascriptcoregtk-4.0.so.0",
            ],
            ["gtk-3"] = ["libgtk-3.so.0"],
            ["gdk-3"] = ["libgdk-3.so.0"],
            ["glib-2.0"] = ["libglib-2.0.so.0"],
            ["gobject-2.0"] = ["libgobject-2.0.so.0"],
            ["gio-2.0"] = ["libgio-2.0.so.0"],
        };

    static WebKitGtkLibraryResolver()
    {
        NativeLibrary.SetDllImportResolver(Assembly, Resolve);
    }

    public static void EnsureRegistered()
    {
    }

    public static bool IsRuntimeAvailable()
    {
        EnsureRegistered();

        return TryLoadAlias("webkit2gtk")
            && TryLoadAlias("javascriptcoregtk")
            && TryLoadAlias("gtk-3")
            && TryLoadAlias("gdk-3")
            && TryLoadAlias("glib-2.0")
            && TryLoadAlias("gobject-2.0")
            && TryLoadAlias("gio-2.0");
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!CandidateLibraries.TryGetValue(libraryName, out var candidates))
            return IntPtr.Zero;

        foreach (var candidate in candidates)
        {
            if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out var handle))
                return handle;
        }

        return IntPtr.Zero;
    }

    private static bool TryLoadAlias(string alias)
    {
        if (!CandidateLibraries.TryGetValue(alias, out var candidates))
            return false;

        foreach (var candidate in candidates)
        {
            if (NativeLibrary.TryLoad(candidate, Assembly, searchPath: null, out _))
                return true;
        }

        return false;
    }
}
