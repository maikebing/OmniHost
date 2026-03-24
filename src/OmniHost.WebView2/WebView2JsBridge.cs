using Microsoft.Web.WebView2.Core;
using System.Text.Json;

namespace OmniHost.WebView2;

/// <summary>
/// Real <see cref="IJsBridge"/> implementation backed by the Microsoft WebView2 engine.
/// </summary>
internal sealed class WebView2JsBridge : IJsBridge
{
    private static readonly JsonSerializerOptions BridgeJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── Bridge script injected into every page on document creation ─────────
    private const string BridgeScript = """
        (function () {
            if (!window.chrome || !window.chrome.webview) return;
            var _pending = {};
            window.chrome.webview.addEventListener('message', function (e) {
                var msg;
                try {
                    msg = typeof e.data === 'string' ? JSON.parse(e.data) : e.data;
                } catch {
                    return;
                }
                if (!msg || typeof msg !== 'object') return;
                if (msg.type === 'response' && _pending[msg.id]) {
                    var cb = _pending[msg.id];
                    delete _pending[msg.id];
                    if (msg.ok === false) {
                        cb.reject(new Error(msg.error || 'omni invoke failed: ' + msg.id));
                        return;
                    }
                    var result = msg.result;
                    if (typeof result === 'string') {
                        try { result = JSON.parse(result); } catch { }
                    }
                    cb.resolve(result);
                } else if (msg.type === 'event') {
                    var detail;
                    if (typeof msg.data === 'string') {
                        try { detail = JSON.parse(msg.data); } catch { detail = msg.data; }
                    } else {
                        detail = msg.data;
                    }
                    window.dispatchEvent(new CustomEvent('omni:' + msg.name, { detail: detail }));
                }
            });
            var omniApi = {
                invoke: function (handler, data) {
                    return new Promise(function (resolve, reject) {
                        var id = Math.random().toString(36).slice(2) + Date.now().toString(36);
                        _pending[id] = { resolve: resolve, reject: reject };
                        setTimeout(function () {
                            if (_pending[id]) { delete _pending[id]; reject(new Error('omni timeout: ' + handler)); }
                        }, 30000);
                        window.chrome.webview.postMessage(JSON.stringify({
                            type: 'invoke', handler: handler, id: id, data: JSON.stringify(data)
                        }));
                    });
                },
                on: function (eventName, callback) {
                    window.addEventListener('omni:' + eventName, function (e) { callback(e.detail); });
                },
                window: {
                    minimize: function () { return omniApi.invoke('window.minimize'); },
                    maximize: function () { return omniApi.invoke('window.maximize'); },
                    close: function () { return omniApi.invoke('window.close'); },
                    startDrag: function () { return omniApi.invoke('window.startDrag'); },
                    showSystemMenu: function () { return omniApi.invoke('window.showSystemMenu'); }
                }
            };
            globalThis.omni = omniApi;
        })();
        """;

    private CoreWebView2? _core;
    private SynchronizationContext? _dispatchContext;
    private readonly Queue<string> _pendingEventMessages = new();
    private readonly Dictionary<string, Func<string, Task<string>>> _handlers = new();
    private bool _documentReady;

    // ── Initialisation ───────────────────────────────────────────────────────

    internal async Task InitializeAsync(CoreWebView2 core)
    {
        _core = core;
        _dispatchContext = SynchronizationContext.Current;
        _core.WebMessageReceived += OnWebMessageReceived;
        await _core.AddScriptToExecuteOnDocumentCreatedAsync(BridgeScript);
    }

    internal void SetDocumentReady(bool isReady)
    {
        _documentReady = isReady;

        if (!isReady)
            return;

        while (_pendingEventMessages.Count > 0)
            _core?.PostWebMessageAsString(_pendingEventMessages.Dequeue());
    }

    private void ThrowIfNotInitialized()
    {
        if (_core is null)
            throw new InvalidOperationException(
                "JsBridge is not initialized. Ensure the adapter has been initialized first.");
    }

    // ── IJsBridge ────────────────────────────────────────────────────────────

    public async Task<string?> ExecuteScriptAsync(string script, CancellationToken cancellationToken = default)
    {
        ThrowIfNotInitialized();
        var execution = await RunOnBridgeThreadAsync(() => _core!.ExecuteScriptAsync(script));
        return await execution;
    }

    public void RegisterHandler(string name, Func<string, Task<string>> handler)
        => _handlers[name] = handler;

    public async Task PostMessageAsync(string eventName, string jsonPayload)
    {
        ThrowIfNotInitialized();
        var envelope = JsonSerializer.Serialize(new
        {
            type = "event",
            name = eventName,
            data = jsonPayload
        });

        await RunOnBridgeThreadAsync(() =>
        {
            if (!_documentReady)
            {
                _pendingEventMessages.Enqueue(envelope);
                return;
            }

            _core!.PostWebMessageAsString(envelope);
        });
    }

    // ── WebMessageReceived dispatcher ────────────────────────────────────────

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string raw;
        try { raw = e.TryGetWebMessageAsString(); }
        catch { return; }

        BridgeInvokeMessage? msg;
        try { msg = JsonSerializer.Deserialize<BridgeInvokeMessage>(raw, BridgeJsonOptions); }
        catch { return; }

        if (msg is null || msg.Type != "invoke" || msg.Handler is null || msg.Id is null)
            return;

        if (!_handlers.TryGetValue(msg.Handler, out var handler))
        {
            PostResponse(new
            {
                type = "response",
                id = msg.Id,
                ok = false,
                error = $"No JS bridge handler named '{msg.Handler}' is registered."
            });
            return;
        }

        try
        {
            var result = await handler(msg.Data ?? "null");
            PostResponse(new
            {
                type = "response",
                id = msg.Id,
                ok = true,
                result
            });
        }
        catch (Exception ex)
        {
            PostResponse(new
            {
                type = "response",
                id = msg.Id,
                ok = false,
                error = ex.Message
            });
        }
    }

    private void PostResponse(object payload)
    {
        var response = JsonSerializer.Serialize(payload);
        _core?.PostWebMessageAsString(response);
    }

    private Task RunOnBridgeThreadAsync(Action action)
    {
        if (_dispatchContext is null || SynchronizationContext.Current == _dispatchContext)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dispatchContext.Post(static state =>
        {
            var (callback, completionSource) = ((Action, TaskCompletionSource<object?>))state!;

            try
            {
                callback();
                completionSource.SetResult(null);
            }
            catch (Exception ex)
            {
                completionSource.SetException(ex);
            }
        }, (action, tcs));

        return tcs.Task;
    }

    private Task<T> RunOnBridgeThreadAsync<T>(Func<T> func)
    {
        if (_dispatchContext is null || SynchronizationContext.Current == _dispatchContext)
            return Task.FromResult(func());

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dispatchContext.Post(static state =>
        {
            var (callback, completionSource) = ((Func<T>, TaskCompletionSource<T>))state!;

            try
            {
                completionSource.SetResult(callback());
            }
            catch (Exception ex)
            {
                completionSource.SetException(ex);
            }
        }, (func, tcs));

        return tcs.Task;
    }

    // ── Internal message model ───────────────────────────────────────────────

    private sealed class BridgeInvokeMessage
    {
        public string? Type { get; init; }
        public string? Handler { get; init; }
        public string? Id { get; init; }
        public string? Data { get; init; }
    }
}
