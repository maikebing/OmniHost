using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Webkit;
using Android.Widget;

namespace NativeWebHost.Android;

/// <summary>
/// Base Android activity that hosts a NativeWebHost WebView backed by APK assets.
/// </summary>
[Activity(
    Label = "NativeWebHost",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden)]
public class NativeWebHostAndroidActivity : Activity
{
    private AndroidWebViewAdapter? _adapter;
    private HostSurfaceDescriptor? _surface;
    private CancellationTokenSource? _activityLifetime;
    private WebView? _webView;

    protected override async void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        _activityLifetime = new CancellationTokenSource();
        _webView = CreateWebView();
        SetContentView(_webView, new ViewGroup.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.MatchParent));

            _surface = AndroidHostSurfaceRegistry.Register(_webView);

        var androidOptions = CreateNativeWebHostOptions();
        ConfigureNativeWebHostOptions(androidOptions);
        var options = androidOptions.ToNativeWebHostOptions();
        ConfigureNativeWebHostOptions(options);
        options.StartUrl = AndroidWebViewAssetHost.NormalizeStartUrl(options.StartUrl);

        _adapter = (AndroidWebViewAdapter)CreateAdapterFactory().Create();
        await _adapter.InitializeAsync(_surface, options, _activityLifetime.Token);
        RegisterDefaultWindowHandlers(_adapter.JsBridge);
        RegisterFetchHandler(_adapter.JsBridge);
        await OnNativeWebViewInitializedAsync(_adapter, _activityLifetime.Token);
        await _adapter.NavigateAsync(options.StartUrl, _activityLifetime.Token);
    }

    protected override void OnDestroy()
    {
        _activityLifetime?.Cancel();
        if (_surface is not null)
            AndroidHostSurfaceRegistry.Unregister(_surface);

        _adapter?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _adapter = null;
        _webView = null;
        _activityLifetime?.Dispose();
        _activityLifetime = null;

        base.OnDestroy();
    }

    public override void OnBackPressed()
    {
        if (_webView?.CanGoBack() == true)
        {
            _webView.GoBack();
            return;
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            Finish();
        else
            base.OnBackPressed();
    }

    /// <summary>
    /// Creates the WebView instance used by this activity.
    /// </summary>
    protected virtual WebView CreateWebView()
        => new(this)
        {
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent),
        };

    /// <summary>
    /// Creates the adapter factory used for the Android WebView.
    /// </summary>
    protected virtual IWebViewAdapterFactory CreateAdapterFactory()
        => new AndroidWebViewAdapterFactory();

    /// <summary>
    /// Creates the default NativeWebHost options for Android.
    /// </summary>
    protected virtual AndroidNativeWebHostOptions CreateNativeWebHostOptions()
        => new()
        {
            Title = "NativeWebHost",
            StartUrl = AndroidWebViewAssetHost.RootUrl,
            EnableDevTools = ApplicationInfo?.Flags.HasFlag(ApplicationInfoFlags.Debuggable) == true,
            AssetRoot = AndroidWebViewAssetHost.DefaultAssetRoot,
        };

    /// <summary>
    /// Allows derived activities to configure Android host options before the adapter starts.
    /// </summary>
    protected virtual void ConfigureNativeWebHostOptions(AndroidNativeWebHostOptions options)
    {
    }

    /// <summary>
    /// Allows advanced derived activities to configure shared host options before the adapter starts.
    /// </summary>
    protected virtual void ConfigureNativeWebHostOptions(NativeWebHostOptions options)
    {
    }

    /// <summary>
    /// Allows derived activities to register bridge handlers before first navigation.
    /// </summary>
    protected virtual Task OnNativeWebViewInitializedAsync(
        IWebViewAdapter adapter,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Handles same-origin <c>/api/...</c> fetches from the hosted page.
    /// </summary>
    protected virtual Task<AndroidFetchResponse> HandleFetchAsync(
        AndroidFetchRequest request,
        CancellationToken cancellationToken)
        => Task.FromResult(AndroidFetchResponse.Unhandled);

    private void RegisterDefaultWindowHandlers(IJsBridge bridge)
    {
        bridge.RegisterHandler("window.close", _ =>
        {
            RunOnUiThread(Finish);
            return Task.FromResult("null");
        });
        bridge.RegisterHandler("window.exit", _ =>
        {
            RunOnUiThread(Finish);
            return Task.FromResult("null");
        });
    }

    private void RegisterFetchHandler(IJsBridge bridge)
    {
        if (bridge is AndroidWebViewJsBridge androidBridge)
            androidBridge.RegisterFetchHandler(HandleFetchAsync);
    }
}
