using System.Runtime.InteropServices;
using System.Text.Json;
using DirectN;
using WebView2;
using WebView2.Utilities;

namespace NativeWebHost.Windows;

/// <summary>
/// WebView2Aot 版本的 JavaScript 桥接实现。
/// </summary>
internal sealed class NativeWebView2JsBridge : IJsBridge, IDisposable
{
    private const string BridgeScriptResourceName =
        "NativeWebHost.Windows.WebView2.Scripts.native-webview2-bridge.js";

    private readonly Queue<string> _pendingEventMessages = new();
    private readonly Dictionary<string, Func<string, Task<string>>> _handlers = new();
    private ICoreWebView2? _core;
    private SynchronizationContext? _dispatchContext;
    private CoreWebView2WebMessageReceivedEventHandler? _messageHandler;
    private EventRegistrationToken _messageToken;
    private bool _documentReady;

    internal async Task InitializeAsync(ICoreWebView2 core, CancellationToken cancellationToken)
    {
        _core = core ?? throw new ArgumentNullException(nameof(core));
        _dispatchContext = SynchronizationContext.Current;
        _messageHandler = new CoreWebView2WebMessageReceivedEventHandler(OnWebMessageReceived);
        _core.add_WebMessageReceived(_messageHandler, ref _messageToken).ThrowOnError();
        await AddScriptToExecuteOnDocumentCreatedAsync(LoadBridgeScript(), cancellationToken);
        SetDocumentReady(true);
    }

    private static string LoadBridgeScript()
    {
        var assembly = typeof(NativeWebView2JsBridge).Assembly;
        using var stream = assembly.GetManifestResourceStream(BridgeScriptResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded bridge script not found: {BridgeScriptResourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public async Task<string?> ExecuteScriptAsync(string script, CancellationToken cancellationToken = default)
    {
        ThrowIfNotInitialized();
        return await await RunOnBridgeThreadAsync(
            () => ExecuteScriptOnBridgeThreadAsync(script, cancellationToken));
    }

    public void RegisterHandler(string name, Func<string, Task<string>> handler)
        => _handlers[name] = handler;

    public async Task PostMessageAsync(string eventName, string jsonPayload)
    {
        ThrowIfNotInitialized();
        var envelope = JsonSerializer.Serialize(
            new BridgeEventMessage("event", eventName, jsonPayload),
            NativeWebView2JsonContext.Default.BridgeEventMessage);

        await RunOnBridgeThreadAsync(() =>
        {
            if (!_documentReady)
            {
                _pendingEventMessages.Enqueue(envelope);
                return;
            }

            _core!.PostWebMessageAsString(PWSTR.From(envelope)).ThrowOnError();
        });
    }

    public void Dispose()
    {
        if (_core is not null && _messageHandler is not null)
        {
            _core.remove_WebMessageReceived(_messageToken);
        }

        _messageHandler = null;
        _core = null;
    }

    internal void SetDocumentReady(bool isReady)
    {
        _documentReady = isReady;

        if (!isReady)
            return;

        while (_pendingEventMessages.Count > 0)
            _core?.PostWebMessageAsString(PWSTR.From(_pendingEventMessages.Dequeue())).ThrowOnError();
    }

    private async void OnWebMessageReceived(
        ICoreWebView2 sender,
        ICoreWebView2WebMessageReceivedEventArgs args)
    {
        string? raw = null;
        try
        {
            args.TryGetWebMessageAsString(out var message).ThrowOnError();
            raw = ToStringAndFree(message);
        }
        catch
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(raw))
            return;

        BridgeInvokeMessage? msg;
        try
        {
            msg = JsonSerializer.Deserialize(
                raw,
                NativeWebView2JsonContext.Default.BridgeInvokeMessage);
        }
        catch { return; }

        if (msg is null || msg.Type != "invoke" || msg.Handler is null || msg.Id is null)
            return;

        if (!_handlers.TryGetValue(msg.Handler, out var handler))
        {
            PostResponse(new BridgeResponseMessage(
                "response",
                msg.Id,
                false,
                Error: $"No JS bridge handler named '{msg.Handler}' is registered."));
            return;
        }

        try
        {
            var result = await handler(msg.Data ?? "null");
            PostResponse(new BridgeResponseMessage("response", msg.Id, true, result));
        }
        catch (Exception ex)
        {
            PostResponse(new BridgeResponseMessage("response", msg.Id, false, Error: ex.Message));
        }
    }

    private void PostResponse(BridgeResponseMessage payload)
    {
        if (_core is null)
            return;

        var response = JsonSerializer.Serialize(
            payload,
            NativeWebView2JsonContext.Default.BridgeResponseMessage);
        _core.PostWebMessageAsString(PWSTR.From(response)).ThrowOnError();
    }

    private Task AddScriptToExecuteOnDocumentCreatedAsync(
        string script,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(
            static state => ((TaskCompletionSource)state!).TrySetCanceled(),
            tcs);

        var hr = _core!.AddScriptToExecuteOnDocumentCreated(
            PWSTR.From(script),
            new CoreWebView2AddScriptToExecuteOnDocumentCreatedCompletedHandler((result, scriptId) =>
            {
                // WebView2 负责管理返回的脚本 ID 内存，这里不能手动释放，否则 WebView2Aot 路径会原生崩溃。
                if (result.IsError)
                {
                    tcs.TrySetException(Marshal.GetExceptionForHR(result)!);
                    return;
                }

                tcs.TrySetResult();
            }));

        if (hr.IsError)
            tcs.TrySetException(Marshal.GetExceptionForHR(hr)!);

        return tcs.Task;
    }

    private Task<string?> ExecuteScriptOnBridgeThreadAsync(
        string script,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(
            static state => ((TaskCompletionSource<string?>)state!).TrySetCanceled(),
            tcs);

        var hr = _core!.ExecuteScript(
            PWSTR.From(script),
            new CoreWebView2ExecuteScriptCompletedHandler((result, json) =>
            {
                if (result.IsError)
                {
                    tcs.TrySetException(Marshal.GetExceptionForHR(result)!);
                    return;
                }

                tcs.TrySetResult(ToStringAndFree(json));
            }));

        if (hr.IsError)
            tcs.TrySetException(Marshal.GetExceptionForHR(hr)!);

        return tcs.Task;
    }

    private void ThrowIfNotInitialized()
    {
        if (_core is null)
            throw new InvalidOperationException(
                "JsBridge is not initialized. Ensure the adapter has been initialized first.");
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

    private static string? ToStringAndFree(PWSTR value)
    {
        if (value.Value == 0)
            return null;

        try
        {
            return value.ToString();
        }
        finally
        {
            Marshal.FreeCoTaskMem(value.Value);
        }
    }
}
