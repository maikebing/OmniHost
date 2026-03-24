using Microsoft.Web.WebView2.Core;

namespace OmniHost.WebView2;

/// <summary>
/// Factory that produces <see cref="WebView2Adapter"/> instances.
/// Register this factory via DI or pass directly to <see cref="OmniHostBuilder.UseAdapter"/>.
/// </summary>
public sealed class WebView2AdapterFactory : IWebViewAdapterFactory
{
    public string AdapterId => "webview2";

    /// <summary>
    /// Returns <see langword="true"/> when the WebView2 Evergreen runtime is installed
    /// on the current Windows machine.
    /// </summary>
    public bool IsAvailable
    {
        get
        {
            if (!OperatingSystem.IsWindows()) return false;
            try
            {
                CoreWebView2Environment.GetAvailableBrowserVersionString();
                return true;
            }
            catch (WebView2RuntimeNotFoundException)
            {
                return false;
            }
        }
    }

    public IWebViewAdapter Create() => new WebView2Adapter();
}

