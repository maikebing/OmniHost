using Android.OS;
using Android.Webkit;

namespace NativeWebHost.Android;

/// <summary>
/// Android <see cref="IWebViewAdapter"/> implementation backed by the system WebView.
/// </summary>
public sealed class AndroidWebViewAdapter : IWebViewAdapter
{
    private readonly AndroidWebViewJsBridge _bridge = new();
    private BrowserCapabilities _capabilities = CreateCapabilities();
    private WebView? _webView;
    private AndroidAssetWebViewClient? _client;
    private NativeWebHostOptions? _options;

    public string AdapterId => "android-webview";

    public BrowserCapabilities Capabilities => _capabilities;

    public IJsBridge JsBridge => _bridge;

    public Task InitializeAsync(
        HostSurfaceDescriptor surface,
        NativeWebHostOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsAndroid())
            throw new PlatformNotSupportedException("AndroidWebViewAdapter is only supported on Android.");

        var webView = AndroidHostSurfaceRegistry.Resolve(surface);
        _options = options;

        return RunOnMainThreadAsync(webView, () =>
        {
            ConfigureWebView(webView, options);
            _webView = webView;
            _capabilities = CreateCapabilities();
        });
    }

    public Task NavigateAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureInitialized();

        var targetUrl = AndroidWebViewAssetHost.NormalizeStartUrl(url);
        return RunOnMainThreadAsync(_webView!, () => _webView!.LoadUrl(targetUrl));
    }

    public void Resize(int width, int height)
    {
    }

    public ValueTask DisposeAsync()
    {
        if (_webView is null)
            return ValueTask.CompletedTask;

        var webView = _webView;
        _webView = null;

        _ = RunOnMainThreadAsync(webView, () =>
        {
            webView.StopLoading();
            webView.RemoveJavascriptInterface(AndroidWebViewJsBridge.InterfaceName);
            webView.SetWebChromeClient(new WebChromeClient());
            webView.SetWebViewClient(new WebViewClient());
            webView.Destroy();
            _client?.Dispose();
            _client = null;
            _bridge.Dispose();
            _options = null;
        });

        return ValueTask.CompletedTask;
    }

    private void ConfigureWebView(WebView webView, NativeWebHostOptions options)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(19) && options.EnableDevTools)
            WebView.SetWebContentsDebuggingEnabled(true);

        var settings = webView.Settings;
        settings.JavaScriptEnabled = true;
        settings.DomStorageEnabled = true;
        settings.DatabaseEnabled = true;
        settings.MediaPlaybackRequiresUserGesture = false;
        settings.AllowFileAccess = false;
        settings.AllowContentAccess = false;
        settings.LoadsImagesAutomatically = true;
        settings.CacheMode = CacheModes.Default;

        if (OperatingSystem.IsAndroidVersionAtLeast(21))
            settings.MixedContentMode = MixedContentHandling.NeverAllow;

        _bridge.Initialize(webView);
        var context = webView.Context ??
            throw new InvalidOperationException("Android WebView context is not available.");
        _client = new AndroidAssetWebViewClient(context, options, _bridge);
        webView.SetWebChromeClient(new WebChromeClient());
        webView.SetWebViewClient(_client);
        webView.AddJavascriptInterface(_bridge, AndroidWebViewJsBridge.InterfaceName);
    }

    private void EnsureInitialized()
    {
        if (_webView is null)
            throw new InvalidOperationException(
                "AndroidWebViewAdapter has not been initialized. Call InitializeAsync first.");
    }

    private static BrowserCapabilities CreateCapabilities()
        => new()
        {
            EngineName = "Android WebView",
            EngineVersion = OperatingSystem.IsAndroidVersionAtLeast(26)
                ? WebView.CurrentWebViewPackage?.VersionName ?? "system"
                : "system",
            SupportsJavaScript = true,
            SupportsJsBridge = true,
            SupportsCustomSchemes = true,
            SupportsDevTools = true,
            SupportedHostSurfaces = new[] { HostSurfaceKind.AndroidView },
        };

    private static Task RunOnMainThreadAsync(WebView webView, Action action)
    {
        if (Looper.MyLooper() == Looper.MainLooper)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        webView.Post(() =>
        {
            try
            {
                action();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }
}
