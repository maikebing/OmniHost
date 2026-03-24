using System.Runtime.InteropServices;
using System.Text.Json;
using OmniHost.WebKitGtk.Native;

namespace OmniHost.WebKitGtk;

internal sealed class WebKitGtkJsBridge : IJsBridge
{
    private static readonly JsonSerializerOptions BridgeJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private const string BridgeScript = """
        (function () {
            if (!window.webkit || !window.webkit.messageHandlers || !window.webkit.messageHandlers.omni) return;
            if (globalThis.omni && globalThis.__omniReceive) return;

            var _pending = {};

            globalThis.__omniReceive = function (messageText) {
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
                        pending.reject(new Error(msg.error || ('omni invoke failed: ' + msg.id)));
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

                    window.dispatchEvent(new CustomEvent('omni:' + msg.name, { detail: detail }));
                }
            };

            var omniApi = {
                invoke: function (handler, data) {
                    return new Promise(function (resolve, reject) {
                        var id = Math.random().toString(36).slice(2) + Date.now().toString(36);
                        _pending[id] = { resolve: resolve, reject: reject };

                        setTimeout(function () {
                            if (_pending[id]) {
                                delete _pending[id];
                                reject(new Error('omni timeout: ' + handler));
                            }
                        }, 30000);

                        window.webkit.messageHandlers.omni.postMessage(JSON.stringify({
                            type: 'invoke',
                            handler: handler,
                            id: id,
                            data: JSON.stringify(data)
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
                    startDrag: function (data) { return omniApi.invoke('window.startDrag', data); },
                    showSystemMenu: function (data) { return omniApi.invoke('window.showSystemMenu', data); }
                }
            };

            globalThis.omni = omniApi;
        })();
        """;

    private readonly Queue<string> _pendingEventMessages = new();
    private readonly Dictionary<string, Func<string, Task<string>>> _handlers = new(StringComparer.Ordinal);

    private SynchronizationContext? _dispatchContext;
    private IntPtr _managerHandle;
    private IntPtr _webViewHandle;
    private bool _documentReady;

    private WebKitGtkNative.LoadChangedCallback? _loadChangedCallback;
    private WebKitGtkNative.ScriptMessageReceivedCallback? _scriptMessageReceivedCallback;

    private static readonly WebKitGtkNative.GAsyncReadyCallback JavascriptFinished = OnJavascriptFinished;

    internal void Initialize(IntPtr managerHandle, IntPtr webViewHandle)
    {
        _managerHandle = managerHandle;
        _webViewHandle = webViewHandle;
        _dispatchContext = SynchronizationContext.Current;

        AddDocumentStartScript(BridgeScript);

        _loadChangedCallback = OnLoadChanged;
        _scriptMessageReceivedCallback = OnScriptMessageReceived;

        WebKitGtkNative.GSignalConnectData(
            _webViewHandle,
            "load-changed",
            Marshal.GetFunctionPointerForDelegate(_loadChangedCallback),
            IntPtr.Zero,
            IntPtr.Zero,
            0);

        WebKitGtkNative.GSignalConnectData(
            _managerHandle,
            "script-message-received::omni",
            Marshal.GetFunctionPointerForDelegate(_scriptMessageReceivedCallback),
            IntPtr.Zero,
            IntPtr.Zero,
            0);
    }

    internal void AddDocumentStartScript(string script)
    {
        ThrowIfNotInitialized(expectWebView: false);

        var userScript = WebKitGtkNative.WebKitUserScriptNew(
            script,
            WebKitGtkNative.WebKitUserContentInjectTopFrame,
            WebKitGtkNative.WebKitUserScriptInjectAtDocumentStart,
            IntPtr.Zero,
            IntPtr.Zero);

        if (userScript == IntPtr.Zero)
            throw new InvalidOperationException("webkit_user_script_new returned a null script handle.");

        try
        {
            WebKitGtkNative.WebKitUserContentManagerAddScript(_managerHandle, userScript);
        }
        finally
        {
            WebKitGtkNative.WebKitUserScriptUnref(userScript);
        }
    }

    public Task<string?> ExecuteScriptAsync(string script, CancellationToken cancellationToken = default)
    {
        ThrowIfNotInitialized(expectWebView: true);
        cancellationToken.ThrowIfCancellationRequested();

        return RunOnBridgeThreadAsync(() => ExecuteScriptCoreAsync(script));
    }

    public void RegisterHandler(string name, Func<string, Task<string>> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(handler);
        _handlers[name] = handler;
    }

    public Task PostMessageAsync(string eventName, string jsonPayload)
    {
        ThrowIfNotInitialized(expectWebView: true);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(jsonPayload);

        var envelope = JsonSerializer.Serialize(new
        {
            type = "event",
            name = eventName,
            data = jsonPayload,
        });

        return RunOnBridgeThreadAsync(async () =>
        {
            if (!_documentReady)
            {
                _pendingEventMessages.Enqueue(envelope);
                return;
            }

            await DispatchEnvelopeAsync(envelope);
        });
    }

    private void OnLoadChanged(IntPtr webView, int loadEvent, IntPtr userData)
    {
        _documentReady = loadEvent == WebKitGtkNative.WebKitLoadFinished;

        if (!_documentReady)
            return;

        while (_pendingEventMessages.Count > 0)
        {
            var envelope = _pendingEventMessages.Dequeue();
            _ = DispatchEnvelopeAsync(envelope);
        }
    }

    private void OnScriptMessageReceived(IntPtr manager, IntPtr javascriptResult, IntPtr userData)
    {
        var raw = ReadJavascriptResult(javascriptResult);
        if (string.IsNullOrWhiteSpace(raw))
            return;

        _ = HandleScriptMessageAsync(raw);
    }

    private async Task HandleScriptMessageAsync(string raw)
    {
        BridgeInvokeMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<BridgeInvokeMessage>(raw, BridgeJsonOptions);
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
            await DispatchEnvelopeAsync(JsonSerializer.Serialize(new
            {
                type = "response",
                id = message.Id,
                ok = false,
                error = $"No JS bridge handler named '{message.Handler}' is registered.",
            }));
            return;
        }

        try
        {
            var result = await handler(message.Data ?? "null");
            await DispatchEnvelopeAsync(JsonSerializer.Serialize(new
            {
                type = "response",
                id = message.Id,
                ok = true,
                result,
            }));
        }
        catch (Exception ex)
        {
            await DispatchEnvelopeAsync(JsonSerializer.Serialize(new
            {
                type = "response",
                id = message.Id,
                ok = false,
                error = ex.Message,
            }));
        }
    }

    private Task DispatchEnvelopeAsync(string envelope)
    {
        var script = $"globalThis.__omniReceive({JsonSerializer.Serialize(envelope)});";
        return RunOnBridgeThreadAsync(() => ExecuteScriptCoreAsync(script));
    }

    private Task<string?> ExecuteScriptCoreAsync(string script)
    {
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var state = new ScriptExecutionState(_webViewHandle, tcs);
        var handle = GCHandle.Alloc(state);

        try
        {
            WebKitGtkNative.WebKitWebViewRunJavascript(
                _webViewHandle,
                script,
                IntPtr.Zero,
                JavascriptFinished,
                GCHandle.ToIntPtr(handle));
        }
        catch
        {
            handle.Free();
            throw;
        }

        return tcs.Task;
    }

    private Task RunOnBridgeThreadAsync(Func<Task> action)
    {
        if (_dispatchContext is null || SynchronizationContext.Current == _dispatchContext)
            return action();

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dispatchContext.Post(async _ =>
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
        }, null);

        return tcs.Task;
    }

    private Task<T> RunOnBridgeThreadAsync<T>(Func<Task<T>> action)
    {
        if (_dispatchContext is null || SynchronizationContext.Current == _dispatchContext)
            return action();

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dispatchContext.Post(async _ =>
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
        }, null);

        return tcs.Task;
    }

    private void ThrowIfNotInitialized(bool expectWebView)
    {
        if (_managerHandle == IntPtr.Zero)
            throw new InvalidOperationException(
                "The WebKitGTK JS bridge has not been initialized yet.");

        if (expectWebView && _webViewHandle == IntPtr.Zero)
            throw new InvalidOperationException(
                "The WebKitGTK web view handle has not been initialized yet.");
    }

    private static void OnJavascriptFinished(IntPtr sourceObject, IntPtr result, IntPtr userData)
    {
        var handle = GCHandle.FromIntPtr(userData);

        try
        {
            if (handle.Target is not ScriptExecutionState state)
                return;

            var javascriptResult = WebKitGtkNative.WebKitWebViewRunJavascriptFinish(
                state.WebViewHandle,
                result,
                out var errorHandle);

            if (errorHandle != IntPtr.Zero)
            {
                var exception = CreateNativeException(errorHandle);
                WebKitGtkNative.GErrorFree(errorHandle);
                state.CompletionSource.TrySetException(exception);
                return;
            }

            if (javascriptResult == IntPtr.Zero)
            {
                state.CompletionSource.TrySetResult(null);
                return;
            }

            try
            {
                state.CompletionSource.TrySetResult(ReadJavascriptResult(javascriptResult));
            }
            finally
            {
                WebKitGtkNative.WebKitJavascriptResultUnref(javascriptResult);
            }
        }
        catch (Exception ex)
        {
            if (handle.Target is ScriptExecutionState state)
                state.CompletionSource.TrySetException(ex);
        }
        finally
        {
            if (handle.IsAllocated)
                handle.Free();
        }
    }

    private static string? ReadJavascriptResult(IntPtr javascriptResult)
    {
        if (javascriptResult == IntPtr.Zero)
            return null;

        var value = WebKitGtkNative.WebKitJavascriptResultGetJsValue(javascriptResult);
        if (value == IntPtr.Zero)
            return null;

        var textHandle = WebKitGtkNative.JscValueToString(value);
        if (textHandle == IntPtr.Zero)
            return null;

        try
        {
            return Marshal.PtrToStringUTF8(textHandle);
        }
        finally
        {
            WebKitGtkNative.GFree(textHandle);
        }
    }

    private static Exception CreateNativeException(IntPtr errorHandle)
    {
        var error = Marshal.PtrToStructure<WebKitGtkNative.GError>(errorHandle);
        var message = error.message != IntPtr.Zero
            ? Marshal.PtrToStringUTF8(error.message)
            : null;

        return new InvalidOperationException(message ?? "A native WebKitGTK error occurred.");
    }

    private sealed class ScriptExecutionState
    {
        public ScriptExecutionState(IntPtr webViewHandle, TaskCompletionSource<string?> completionSource)
        {
            WebViewHandle = webViewHandle;
            CompletionSource = completionSource;
        }

        public IntPtr WebViewHandle { get; }

        public TaskCompletionSource<string?> CompletionSource { get; }
    }

    private sealed class BridgeInvokeMessage
    {
        public string? Type { get; init; }
        public string? Handler { get; init; }
        public string? Id { get; init; }
        public string? Data { get; init; }
    }
}
