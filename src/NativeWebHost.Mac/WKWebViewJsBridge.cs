using System.Text.Json;
using AppKit;
using Foundation;
using WebKit;

namespace NativeWebHost.Mac;

internal sealed class WKWebViewJsBridge : NSObject, IJsBridge, IWKScriptMessageHandler
{
    internal const string HandlerName = "nativeWeb";

    private const string BridgeScript = """
        (function () {
            if (!window.webkit || !window.webkit.messageHandlers || !window.webkit.messageHandlers.nativeWeb) return;
            if (globalThis.nativeWeb && globalThis.__nativeWebReceive) return;

            var _pending = {};

            globalThis.__nativeWebReceive = function (messageText) {
                var msg;
                try {
                    msg = typeof messageText === 'string' ? JSON.parse(messageText) : messageText;
                } catch {
                    return;
                }

                if (!msg || typeof msg !== 'object') return;

                if (msg.type === 'response' && msg.id && _pending[msg.id]) {
                    var pending = _pending[msg.id];
                    delete _pending[msg.id];

                    if (msg.ok === false) {
                        pending.reject(new Error(msg.error || ('nativeWeb invoke failed: ' + msg.id)));
                        return;
                    }

                    var result = msg.result;
                    if (typeof result === 'string') {
                        try { result = JSON.parse(result); } catch { }
                    }

                    pending.resolve(result);
                    return;
                }

                if (msg.type === 'event' && msg.name) {
                    var detail = msg.data;
                    if (typeof detail === 'string') {
                        try { detail = JSON.parse(detail); } catch { }
                    }

                    window.dispatchEvent(new CustomEvent('nativeWeb:' + msg.name, { detail: detail }));
                }
            };

            var nativeWebApi = {
                invoke: function (handler, data) {
                    return new Promise(function (resolve, reject) {
                        var id = Math.random().toString(36).slice(2) + Date.now().toString(36);
                        _pending[id] = { resolve: resolve, reject: reject };

                        setTimeout(function () {
                            if (_pending[id]) {
                                delete _pending[id];
                                reject(new Error('nativeWeb timeout: ' + handler));
                            }
                        }, 30000);

                        window.webkit.messageHandlers.nativeWeb.postMessage(JSON.stringify({
                            type: 'invoke',
                            handler: handler,
                            id: id,
                            data: JSON.stringify(data)
                        }));
                    });
                },
                on: function (eventName, callback) {
                    window.addEventListener('nativeWeb:' + eventName, function (e) { callback(e.detail); });
                },
                window: {
                    minimize: function () { return nativeWebApi.invoke('window.minimize'); },
                    maximize: function () { return nativeWebApi.invoke('window.maximize'); },
                    close: function () { return nativeWebApi.invoke('window.close'); },
                    exit: function () { return nativeWebApi.invoke('window.exit'); },
                    startDrag: function (data) { return nativeWebApi.invoke('window.startDrag', data); },
                    showSystemMenu: function (data) { return nativeWebApi.invoke('window.showSystemMenu', data); }
                }
            };

            globalThis.nativeWeb = nativeWebApi;
        })();
        """;

    private readonly Queue<string> _pendingEventMessages = new();
    private readonly Dictionary<string, Func<string, Task<string>>> _handlers = new(StringComparer.Ordinal);

    private WKWebView? _webView;
    private bool _initialized;

    internal void Initialize(WKUserContentController userContentController, WKWebView webView)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        _initialized = true;
        AddDocumentStartScript(BridgeScript);
    }

    internal void AddDocumentStartScript(string script)
    {
        if (!_initialized)
            throw new InvalidOperationException("The WKWebView JS bridge has not been initialized yet.");

        var userScript = new WKUserScript(
            new NSString(script),
            WKUserScriptInjectionTime.AtDocumentStart,
            false);

        _webView!.Configuration.UserContentController.AddUserScript(userScript);
    }

    public Task<string?> ExecuteScriptAsync(string script, CancellationToken cancellationToken = default)
    {
        ThrowIfNotInitialized();
        cancellationToken.ThrowIfCancellationRequested();

        return RunOnMainThreadAsync(async () =>
        {
            var result = await _webView!.EvaluateJavaScriptAsync(script);
            return result?.ToString();
        });
    }

    public void RegisterHandler(string name, Func<string, Task<string>> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(handler);
        _handlers[name] = handler;
    }

    public Task PostMessageAsync(string eventName, string jsonPayload)
    {
        ThrowIfNotInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(jsonPayload);

        var envelope = JsonSerializer.Serialize(
            new WKBridgeEventEnvelope("event", eventName, jsonPayload),
            MacJsonContext.Default.WKBridgeEventEnvelope);

        return RunOnMainThreadAsync(async () =>
        {
            if (_webView is null)
                return;

            if (!_webView.IsLoading)
            {
                await DispatchEnvelopeAsync(envelope);
                return;
            }

            _pendingEventMessages.Enqueue(envelope);
        });
    }

    public void DidReceiveScriptMessage(
        WKUserContentController userContentController,
        WKScriptMessage message)
    {
        var raw = message.Body?.ToString();
        if (string.IsNullOrWhiteSpace(raw))
            return;

        _ = HandleScriptMessageAsync(raw);
    }

    internal void Dispose(WKUserContentController userContentController)
    {
        if (_initialized)
            userContentController.RemoveScriptMessageHandler(HandlerName);

        _pendingEventMessages.Clear();
        _webView = null;
        _initialized = false;
    }

    private async Task HandleScriptMessageAsync(string raw)
    {
        WKBridgeInvokeMessage? message;
        try
        {
            message = JsonSerializer.Deserialize(raw, MacJsonContext.Default.WKBridgeInvokeMessage);
        }
        catch
        {
            return;
        }

        if (message is null
            || !string.Equals(message.Type, "invoke", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(message.Handler)
            || string.IsNullOrWhiteSpace(message.Id))
            return;

        if (!_handlers.TryGetValue(message.Handler, out var handler))
        {
            await DispatchEnvelopeAsync(JsonSerializer.Serialize(
                new WKBridgeResponseEnvelope(
                    "response",
                    message.Id,
                    Ok: false,
                    Error: $"No JS bridge handler named '{message.Handler}' is registered."),
                MacJsonContext.Default.WKBridgeResponseEnvelope));
            return;
        }

        try
        {
            var result = await handler(message.Data ?? "null");
            await DispatchEnvelopeAsync(JsonSerializer.Serialize(
                new WKBridgeResponseEnvelope("response", message.Id, Ok: true, Result: result),
                MacJsonContext.Default.WKBridgeResponseEnvelope));
        }
        catch (Exception ex)
        {
            await DispatchEnvelopeAsync(JsonSerializer.Serialize(
                new WKBridgeResponseEnvelope("response", message.Id, Ok: false, Error: ex.Message),
                MacJsonContext.Default.WKBridgeResponseEnvelope));
        }
    }

    private Task DispatchEnvelopeAsync(string envelope)
    {
        var script =
            $"globalThis.__nativeWebReceive({JsonSerializer.Serialize(envelope, MacJsonContext.Default.String)});";
        return RunOnMainThreadAsync(async () =>
        {
            if (_webView is not null)
                await _webView.EvaluateJavaScriptAsync(script);
        });
    }

    private static Task RunOnMainThreadAsync(Func<Task> action)
    {
        if (NSThread.IsMain)
            return action();

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        NSApplication.SharedApplication.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await action();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }

    private static Task<T> RunOnMainThreadAsync<T>(Func<Task<T>> action)
    {
        if (NSThread.IsMain)
            return action();

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        NSApplication.SharedApplication.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var result = await action();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }

    private void ThrowIfNotInitialized()
    {
        if (!_initialized || _webView is null)
            throw new InvalidOperationException(
                "The WKWebView JS bridge has not been initialized yet.");
    }
}
