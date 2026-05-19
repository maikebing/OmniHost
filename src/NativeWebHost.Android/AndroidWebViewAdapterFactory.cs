namespace NativeWebHost.Android;

/// <summary>
/// Factory for Android WebView adapters.
/// </summary>
public sealed class AndroidWebViewAdapterFactory : IWebViewAdapterFactory
{
    public string AdapterId => "android-webview";

    public bool IsAvailable => OperatingSystem.IsAndroid();

    public IWebViewAdapter Create()
        => new AndroidWebViewAdapter();
}
