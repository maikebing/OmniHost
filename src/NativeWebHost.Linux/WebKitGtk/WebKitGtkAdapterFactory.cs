using NativeWebHost.Linux.Native;

namespace NativeWebHost.Linux;

/// <summary>
/// Factory that produces <see cref="WebKitGtkAdapter"/> instances for Linux GTK hosts.
/// </summary>
public sealed class WebKitGtkAdapterFactory : IWebViewAdapterFactory
{
    public string AdapterId => "webkitgtk";

    public bool IsAvailable
    {
        get
        {
            if (!OperatingSystem.IsLinux())
                return false;

            return WebKitGtkLibraryResolver.IsRuntimeAvailable();
        }
    }

    public IWebViewAdapter Create() => new WebKitGtkAdapter();
}
