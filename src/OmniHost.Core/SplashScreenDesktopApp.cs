using System.Text.Json;
using System.Text.Json.Serialization;

namespace OmniHost.Core;

internal sealed partial class SplashScreenDesktopApp : IWindowAwareDesktopApp
{
    private readonly IDesktopApp? _innerApp;
    private readonly string _splashWindowId;

    public SplashScreenDesktopApp(IDesktopApp? innerApp, string splashWindowId)
    {
        _innerApp = innerApp;
        _splashWindowId = splashWindowId;
    }

    public Task OnStartAsync(IWebViewAdapter adapter, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task OnClosingAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task OnWindowStartAsync(
        OmniWindowContext window,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(window);

        RegisterSplashBridgeHandlers(window);

        if (window.IsMainWindow
            && !string.Equals(window.WindowId, _splashWindowId, StringComparison.Ordinal)
            && window.WindowManager.GetOpenWindowIds().Contains(_splashWindowId, StringComparer.Ordinal))
        {
            var payload = JsonSerializer.Serialize(
                new SplashMainWindowStartedPayload(
                    _splashWindowId,
                    window.WindowId,
                    window.Options.Title,
                    window.Options.StartUrl),
                SplashScreenJsonContext.Default.SplashMainWindowStartedPayload);

            _ = window.WindowManager.PostEventAsync(
                _splashWindowId,
                OmniSplashScreen.MainWindowStartedEventName,
                payload,
                cancellationToken);
        }

        if (_innerApp is IWindowAwareDesktopApp windowAwareDesktopApp)
        {
            await windowAwareDesktopApp.OnWindowStartAsync(window, cancellationToken);
            return;
        }

        if (_innerApp is not null)
            await _innerApp.OnStartAsync(window.Adapter, cancellationToken);
    }

    public async Task OnWindowClosingAsync(
        OmniWindowContext window,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (_innerApp is IWindowAwareDesktopApp windowAwareDesktopApp)
        {
            await windowAwareDesktopApp.OnWindowClosingAsync(window, cancellationToken);
            return;
        }

        if (_innerApp is not null)
            await _innerApp.OnClosingAsync(cancellationToken);
    }

    private void RegisterSplashBridgeHandlers(OmniWindowContext window)
    {
        window.Adapter.JsBridge.RegisterHandler(OmniSplashScreen.CloseHandlerName, _ =>
        {
            var closed = window.WindowManager.TryCloseWindow(_splashWindowId);

            return Task.FromResult(JsonSerializer.Serialize(
                new SplashCloseResult(_splashWindowId, closed),
                SplashScreenJsonContext.Default.SplashCloseResult));
        });
    }

    private sealed record SplashMainWindowStartedPayload(
        [property: JsonPropertyName("splashWindowId")] string SplashWindowId,
        [property: JsonPropertyName("mainWindowId")] string MainWindowId,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("startUrl")] string StartUrl);

    private sealed record SplashCloseResult(
        [property: JsonPropertyName("windowId")] string WindowId,
        [property: JsonPropertyName("closed")] bool Closed);

    [JsonSerializable(typeof(SplashMainWindowStartedPayload))]
    [JsonSerializable(typeof(SplashCloseResult))]
    private sealed partial class SplashScreenJsonContext : JsonSerializerContext;
}
