using Microsoft.Web.WebView2.Core;
using System.Text.Json;

namespace OmniWebHost.WebView2;

/// <summary>
/// Real <see cref="IJsBridge"/> implementation backed by the Microsoft WebView2 engine.
/// </summary>
internal sealed class WebView2JsBridge : IJsBridge
{
    // ── Bridge script injected into every page on document creation ─────────
    private const string BridgeScript = """
        (function () {
            if (!window.chrome || !window.chrome.webview) return;
            var _pending = {};
            window.chrome.webview.addEventListener('message', function (e) {
                var msg;
                try { msg = JSON.parse(e.data); } catch { return; }
                if (msg.type === 'response' && _pending[msg.id]) {
                    var cb = _pending[msg.id];
                    delete _pending[msg.id];
                    cb.resolve(msg.result);
                } else if (msg.type === 'event') {
                    var detail;
                    try { detail = JSON.parse(msg.data); } catch { detail = msg.data; }
                    window.dispatchEvent(new CustomEvent('omni:' + msg.name, { detail: detail }));
                }
            });
            window.omni = {
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
                }
            };
        })();
        """;

    private CoreWebView2? _core;
    private readonly Dictionary<string, Func<string, Task<string>>> _handlers = new();

    // ── Initialisation ───────────────────────────────────────────────────────

    internal async Task InitializeAsync(CoreWebView2 core)
    {
        _core = core;
        _core.WebMessageReceived += OnWebMessageReceived;
        await _core.AddScriptToExecuteOnDocumentCreatedAsync(BridgeScript);
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
        return await _core!.ExecuteScriptAsync(script);
    }

    public void RegisterHandler(string name, Func<string, Task<string>> handler)
        => _handlers[name] = handler;

    public async Task PostMessageAsync(string eventName, string jsonPayload)
    {
        ThrowIfNotInitialized();
        // Send as a typed envelope so the bridge script routes it correctly.
        var envelope = JsonSerializer.Serialize(new
        {
            type = "event",
            name = eventName,
            data = jsonPayload
        });
        _core!.PostWebMessageAsJson(envelope);
        await Task.CompletedTask;
    }

    // ── WebMessageReceived dispatcher ────────────────────────────────────────

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string raw;
        try { raw = e.TryGetWebMessageAsString(); }
        catch { return; }

        BridgeInvokeMessage? msg;
        try { msg = JsonSerializer.Deserialize<BridgeInvokeMessage>(raw); }
        catch { return; }

        if (msg is null || msg.Type != "invoke" || msg.Handler is null || msg.Id is null) return;

        if (!_handlers.TryGetValue(msg.Handler, out var handler)) return;

        string result;
        try { result = await handler(msg.Data ?? "null"); }
        catch (Exception ex) { result = JsonSerializer.Serialize(new { error = ex.Message }); }

        var response = JsonSerializer.Serialize(new
        {
            type = "response",
            id = msg.Id,
            result
        });
        _core?.PostWebMessageAsJson(response);
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

