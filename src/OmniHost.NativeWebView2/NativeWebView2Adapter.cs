using System.Reflection;
using System.Runtime.InteropServices;
using DirectN;
using DirectN.Extensions.Com;
using WebView2;
using WebView2.Utilities;

namespace OmniHost.NativeWebView2;

/// <summary>
/// 基于 WebView2Aot generated COM binding 的 WebView2 适配器。
/// </summary>
public sealed class NativeWebView2Adapter : IWebViewAdapter
{
    private readonly NativeWebView2JsBridge _bridge = new();
    private ComObject<ICoreWebView2Environment>? _environment;
    private ComObject<ICoreWebView2Controller>? _controller;
    private ComObject<ICoreWebView2>? _webView;
    private BrowserCapabilities _capabilities = new()
    {
        EngineName = "Native WebView2",
        EngineVersion = "unknown",
        SupportsJavaScript = true,
        SupportsJsBridge = true,
        SupportsCustomSchemes = false,
        SupportsDevTools = true,
        SupportedHostSurfaces = new[] { HostSurfaceKind.Hwnd },
    };

    public string AdapterId => "native-webview2";

    public BrowserCapabilities Capabilities => _capabilities;

    public IJsBridge JsBridge => _bridge;

    public async Task InitializeAsync(
        HostSurfaceDescriptor surface,
        OmniHostOptions options,
        CancellationToken cancellationToken = default)
    {
        if (surface.Kind != HostSurfaceKind.Hwnd)
            throw new NotSupportedException(
                $"NativeWebView2Adapter only supports {HostSurfaceKind.Hwnd} host surfaces.");

        if (!surface.IsCreated)
            throw new InvalidOperationException(
                "The supplied host surface has not been created yet.");

        WebView2Utilities.Initialize(typeof(NativeWebView2Adapter).Assembly);

        var browserVersion = WebView2Utilities.GetAvailableCoreWebView2BrowserVersionString();
        if (browserVersion is null)
            throw new InvalidOperationException("WebView2 Runtime is not installed.");

        var userDataFolder = options.UserDataFolder
            ?? Path.Combine(Path.GetTempPath(), "OmniHost", SanitizeFolderName(options.Title));

        _environment = await CreateEnvironmentAsync(userDataFolder, cancellationToken);
        _controller = await CreateControllerAsync(_environment.Object, surface.Handle, cancellationToken);
        _controller.Object.put_IsVisible(BOOL.TRUE).ThrowOnError();
        _controller.Object.get_CoreWebView2(out var core).ThrowOnError();
        _webView = new ComObject<ICoreWebView2>(core);

        _capabilities = new BrowserCapabilities
        {
            EngineName = "Native WebView2",
            EngineVersion = browserVersion,
            SupportsJavaScript = true,
            SupportsJsBridge = true,
            SupportsCustomSchemes = false,
            SupportsDevTools = true,
            SupportedHostSurfaces = new[] { HostSurfaceKind.Hwnd },
        };

        ConfigureSettings(_webView.Object, options);
        await _bridge.InitializeAsync(_webView.Object, cancellationToken);
    }

    public Task NavigateAsync(string url, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        _webView!.Object.Navigate(PWSTR.From(url)).ThrowOnError();
        return Task.CompletedTask;
    }

    public void Resize(int width, int height)
    {
        _controller?.Object.put_Bounds(RECT.Sized(0, 0, width, height)).ThrowOnError();
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            _bridge.Dispose();
            _controller?.Object.Close();
        }
        finally
        {
            _webView?.Dispose();
            _controller?.Dispose();
            _environment?.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private static Task<ComObject<ICoreWebView2Environment>> CreateEnvironmentAsync(
        string userDataFolder,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<ComObject<ICoreWebView2Environment>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(
            static state => ((TaskCompletionSource<ComObject<ICoreWebView2Environment>>)state!).TrySetCanceled(),
            tcs);

        var hr = WebView2.Functions.CreateCoreWebView2EnvironmentWithOptions(
            PWSTR.Null,
            PWSTR.From(userDataFolder),
            null!,
            new CoreWebView2CreateCoreWebView2EnvironmentCompletedHandler((result, environment) =>
            {
                if (result.IsError)
                {
                    tcs.TrySetException(Marshal.GetExceptionForHR(result)!);
                    return;
                }

                tcs.TrySetResult(new ComObject<ICoreWebView2Environment>(environment));
            }));

        if (hr.IsError)
            tcs.TrySetException(Marshal.GetExceptionForHR(hr)!);

        return tcs.Task;
    }

    private static Task<ComObject<ICoreWebView2Controller>> CreateControllerAsync(
        ICoreWebView2Environment environment,
        nint parentWindow,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<ComObject<ICoreWebView2Controller>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(
            static state => ((TaskCompletionSource<ComObject<ICoreWebView2Controller>>)state!).TrySetCanceled(),
            tcs);

        var hr = environment.CreateCoreWebView2Controller(
            new HWND(parentWindow),
            new CoreWebView2CreateCoreWebView2ControllerCompletedHandler((result, controller) =>
            {
                if (result.IsError)
                {
                    tcs.TrySetException(Marshal.GetExceptionForHR(result)!);
                    return;
                }

                tcs.TrySetResult(new ComObject<ICoreWebView2Controller>(controller));
            }));

        if (hr.IsError)
            tcs.TrySetException(Marshal.GetExceptionForHR(hr)!);

        return tcs.Task;
    }

    private static void ConfigureSettings(ICoreWebView2 webView, OmniHostOptions options)
    {
        webView.get_Settings(out var settings).ThrowOnError();
        using var settingsObject = new ComObject<ICoreWebView2Settings>(settings);
        var devTools = options.EnableDevTools ? BOOL.TRUE : BOOL.FALSE;

        settingsObject.Object.put_IsScriptEnabled(BOOL.TRUE).ThrowOnError();
        settingsObject.Object.put_IsWebMessageEnabled(BOOL.TRUE).ThrowOnError();
        settingsObject.Object.put_AreDevToolsEnabled(devTools).ThrowOnError();
        settingsObject.Object.put_AreDefaultContextMenusEnabled(devTools).ThrowOnError();
    }

    private void EnsureInitialized()
    {
        if (_webView is null)
            throw new InvalidOperationException(
                "NativeWebView2Adapter has not been initialized. Call InitializeAsync first.");
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray());
    }
}
