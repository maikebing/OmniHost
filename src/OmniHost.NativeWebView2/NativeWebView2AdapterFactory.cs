using WebView2.Utilities;

namespace OmniHost.NativeWebView2;

/// <summary>
/// 使用 WebView2Aot 的 Native AOT 友好 WebView2 适配器工厂。
/// </summary>
public sealed class NativeWebView2AdapterFactory : IWebViewAdapterFactory
{
    public string AdapterId => "native-webview2";

    public bool IsAvailable
    {
        get
        {
            try
            {
                return WebView2Utilities.GetAvailableCoreWebView2BrowserVersionString() is not null;
            }
            catch
            {
                return false;
            }
        }
    }

    public IWebViewAdapter Create() => new NativeWebView2Adapter();
}
