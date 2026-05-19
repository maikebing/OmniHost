using System.Text.Json;
using Android.Webkit;
using Java.Interop;

namespace NativeWebHost.Android;

internal sealed class AndroidWebViewJsBridge : Java.Lang.Object, IJsBridge
{
    internal const string InterfaceName = "__nativeWebAndroid";

    internal const string BridgeScript = """
        (function () {
            if (!globalThis.__nativeWebAndroid || !globalThis.__nativeWebAndroid.postMessage) return;
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

                        globalThis.__nativeWebAndroid.postMessage(JSON.stringify({
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

    internal const string FetchBridgeScript = """
        (function () {
            if (globalThis.__nativeWebFetchInstalled) return;
            globalThis.__nativeWebFetchInstalled = true;

            var originalFetch = globalThis.fetch ? globalThis.fetch.bind(globalThis) : null;
            if (!originalFetch) return;

            function shouldBridge(input) {
                try {
                    var rawUrl = typeof input === 'string' ? input : input && input.url;
                    if (!rawUrl) return false;
                    var url = new URL(rawUrl, location.href);
                    return url.origin === location.origin && url.pathname.indexOf('/api/') === 0;
                } catch {
                    return false;
                }
            }

            function arrayBufferToBase64(buffer) {
                var bytes = new Uint8Array(buffer);
                var chunkSize = 0x8000;
                var binary = '';
                for (var i = 0; i < bytes.length; i += chunkSize) {
                    binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunkSize));
                }
                return btoa(binary);
            }

            function base64ToArrayBuffer(value) {
                var binary = atob(value);
                var bytes = new Uint8Array(binary.length);
                for (var i = 0; i < binary.length; i++) {
                    bytes[i] = binary.charCodeAt(i);
                }
                return bytes.buffer;
            }

            async function toPayload(input, init) {
                var request = new Request(input, init);
                var headers = {};
                request.headers.forEach(function (value, key) {
                    headers[key] = value;
                });

                var bodyBase64 = null;
                if (request.method !== 'GET' && request.method !== 'HEAD') {
                    var body = await request.clone().arrayBuffer();
                    if (body.byteLength > 0) {
                        bodyBase64 = arrayBufferToBase64(body);
                    }
                }

                return {
                    url: new URL(request.url, location.href).href,
                    method: request.method,
                    headers: headers,
                    bodyBase64: bodyBase64
                };
            }

            globalThis.fetch = async function (input, init) {
                if (!globalThis.nativeWeb || !shouldBridge(input)) {
                    return originalFetch(input, init);
                }

                try {
                    var result = await globalThis.nativeWeb.invoke('http.fetch', await toPayload(input, init));
                    if (!result || result.handled === false) {
                        return originalFetch(input, init);
                    }

                    var headers = result.headers || {};
                    var status = result.status || 200;
                    var body = null;
                    if (status !== 204 && status !== 304) {
                        body = result.bodyBase64 ? base64ToArrayBuffer(result.bodyBase64) : (result.body || '');
                    }

                    return new Response(body, {
                        status: status,
                        statusText: result.statusText || 'OK',
                        headers: headers
                    });
                } catch {
                    return originalFetch(input, init);
                }
            };
        })();
        """;

    private readonly Queue<string> _pendingEventMessages = new();
    private readonly Dictionary<string, Func<string, Task<string>>> _handlers = new(StringComparer.Ordinal);
    private WebView? _webView;
    private bool _documentReady;

    internal void Initialize(WebView webView)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        RegisterWindowHandlers();
    }

    [JavascriptInterface]
    [Export("postMessage")]
    public void PostMessage(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return;

        _ = HandleScriptMessageAsync(raw);
    }

    public Task<string?> ExecuteScriptAsync(string script, CancellationToken cancellationToken = default)
    {
        ThrowIfNotInitialized();
        cancellationToken.ThrowIfCancellationRequested();

        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _webView!.Post(() =>
        {
            try
            {
                _webView.EvaluateJavascript(script, new ValueCallback(value => tcs.TrySetResult(value)));
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    public void RegisterHandler(string name, Func<string, Task<string>> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(handler);
        _handlers[name] = handler;
    }

    internal void RegisterFetchHandler(Func<AndroidFetchRequest, CancellationToken, Task<AndroidFetchResponse>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        RegisterHandler("http.fetch", async payload =>
        {
            var request = JsonSerializer.Deserialize(payload, AndroidJsonContext.Default.AndroidFetchRequest);
            if (request is null)
                return JsonSerializer.Serialize(AndroidFetchResponse.Unhandled, AndroidJsonContext.Default.AndroidFetchResponse);

            var response = await handler(request, CancellationToken.None);
            return JsonSerializer.Serialize(response, AndroidJsonContext.Default.AndroidFetchResponse);
        });
    }

    public Task PostMessageAsync(string eventName, string jsonPayload)
    {
        ThrowIfNotInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(jsonPayload);

        var envelope = JsonSerializer.Serialize(
            new AndroidBridgeEventEnvelope("event", eventName, jsonPayload),
            AndroidJsonContext.Default.AndroidBridgeEventEnvelope);

        if (!_documentReady)
        {
            _pendingEventMessages.Enqueue(envelope);
            return Task.CompletedTask;
        }

        return DispatchEnvelopeAsync(envelope);
    }

    internal async Task InjectDocumentScriptsAsync()
    {
        ThrowIfNotInitialized();
        await ExecuteScriptAsync(BridgeScript);
        await ExecuteScriptAsync(FetchBridgeScript);
        _documentReady = true;

        while (_pendingEventMessages.Count > 0)
            await DispatchEnvelopeAsync(_pendingEventMessages.Dequeue());
    }

    internal void SetDocumentLoading()
        => _documentReady = false;

    private async Task HandleScriptMessageAsync(string raw)
    {
        AndroidBridgeInvokeMessage? message;
        try
        {
            message = JsonSerializer.Deserialize(raw, AndroidJsonContext.Default.AndroidBridgeInvokeMessage);
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
                new AndroidBridgeResponseEnvelope(
                    "response",
                    message.Id,
                    Ok: false,
                    Error: $"No JS bridge handler named '{message.Handler}' is registered."),
                AndroidJsonContext.Default.AndroidBridgeResponseEnvelope));
            return;
        }

        try
        {
            var result = await handler(message.Data ?? "null");
            await DispatchEnvelopeAsync(JsonSerializer.Serialize(
                new AndroidBridgeResponseEnvelope("response", message.Id, Ok: true, Result: result),
                AndroidJsonContext.Default.AndroidBridgeResponseEnvelope));
        }
        catch (Exception ex)
        {
            await DispatchEnvelopeAsync(JsonSerializer.Serialize(
                new AndroidBridgeResponseEnvelope("response", message.Id, Ok: false, Error: ex.Message),
                AndroidJsonContext.Default.AndroidBridgeResponseEnvelope));
        }
    }

    private Task DispatchEnvelopeAsync(string envelope)
    {
        var script =
            $"globalThis.__nativeWebReceive({JsonSerializer.Serialize(envelope, AndroidJsonContext.Default.String)});";
        return ExecuteScriptAsync(script);
    }

    private void RegisterWindowHandlers()
    {
        RegisterHandler("window.minimize", _ => Task.FromResult("null"));
        RegisterHandler("window.maximize", _ => Task.FromResult("null"));
        RegisterHandler("window.startDrag", _ => Task.FromResult("null"));
        RegisterHandler("window.showSystemMenu", _ => Task.FromResult("null"));
    }

    private void ThrowIfNotInitialized()
    {
        if (_webView is null)
            throw new InvalidOperationException(
                "The Android WebView JS bridge has not been initialized yet.");
    }

    private sealed class ValueCallback : Java.Lang.Object, IValueCallback
    {
        private readonly Action<string?> _callback;

        public ValueCallback(Action<string?> callback)
            => _callback = callback;

        public void OnReceiveValue(Java.Lang.Object? value)
            => _callback(value?.ToString());
    }
}
