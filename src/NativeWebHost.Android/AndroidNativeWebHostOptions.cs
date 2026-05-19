namespace NativeWebHost.Android;

/// <summary>
/// Android-specific host options for the system WebView runtime.
/// </summary>
public sealed class AndroidNativeWebHostOptions
{
    /// <summary>
    /// Window/activity title used by the host application.
    /// </summary>
    public string Title { get; set; } = "NativeWebHost";

    /// <summary>
    /// Initial page loaded by the Android WebView.
    /// </summary>
    public string StartUrl { get; set; } = AndroidWebViewAssetHost.RootUrl;

    /// <summary>
    /// Enables system WebView debugging for debug builds.
    /// </summary>
    public bool EnableDevTools { get; set; }

    /// <summary>
    /// APK/AAB asset root that contains the published web application.
    /// </summary>
    public string AssetRoot { get; set; } = AndroidWebViewAssetHost.DefaultAssetRoot;

    internal NativeWebHostOptions ToNativeWebHostOptions()
    {
        var options = new NativeWebHostOptions
        {
            Title = Title,
            StartUrl = StartUrl,
            EnableDevTools = EnableDevTools,
            ScrollBarMode = NativeWebScrollBarMode.Auto,
        };
        options.AdapterSettings["AndroidAssetRoot"] = AssetRoot;
        return options;
    }
}
