using System.Collections.Concurrent;
using System.Text.Json;
using CefSharp;
using CefSharp.WinForms;

namespace OmniHost.Cef;

internal sealed class CefSharpJsBridge : IJsBridge, IDisposable
{
    private readonly ChromiumWebBrowser _browser;
    private readonly ConcurrentDictionary<string, Func<string, Task<string>>> _handlers = new(StringComparer.Ordinal);
    private readonly object _eventGate = new();
    private readonly Dictionary<string, List<IJavascriptCallback>> _eventCallbacks = new(StringComparer.Ordinal);
    private readonly Queue<PendingEventMessage> _pendingEventMessages = new();
    private readonly TaskCompletionSource<object?> _browserInitialized = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private SynchronizationContext? _dispatchContext;
    private bool _documentReady;
    private bool _disposed;

    public CefSharpJsBridge(ChromiumWebBrowser browser)
    {
        _browser = browser ?? throw new ArgumentNullException(nameof(browser));
        _dispatchContext = SynchronizationContext.Current;

        _browser.IsBrowserInitializedChanged += OnBrowserInitializedChanged;
        _browser.FrameLoadStart += OnFrameLoadStart;
        _browser.FrameLoadEnd += OnFrameLoadEnd;
    }

    public object CreateJavascriptBoundApi() => new BoundOmniApi(this);

    public Task WaitForBrowserInitializedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _browserInitialized.Task.WaitAsync(cancellationToken);
    }

    public async Task<string?> ExecuteScriptAsync(string script, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        cancellationToken.ThrowIfCancellationRequested();

        var response = await RunOnBridgeThreadAsync(() => _browser.EvaluateScriptAsync(script));
        if (!response.Success)
            throw new InvalidOperationException(response.Message);

        return response.Result switch
        {
            null => null,
            string text => text,
            _ => JsonSerializer.Serialize(response.Result),
        };
    }

    public void RegisterHandler(string name, Func<string, Task<string>> handler)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(handler);
        _handlers[name] = handler;
    }

    public Task PostMessageAsync(string eventName, string jsonPayload)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(jsonPayload);

        return RunOnBridgeThreadAsync(async () =>
        {
            if (!_documentReady)
            {
                lock (_eventGate)
                {
                    _pendingEventMessages.Enqueue(new PendingEventMessage(eventName, jsonPayload));
                }

                return;
            }

            await DispatchEventCoreAsync(eventName, jsonPayload);
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _browser.IsBrowserInitializedChanged -= OnBrowserInitializedChanged;
        _browser.FrameLoadStart -= OnFrameLoadStart;
        _browser.FrameLoadEnd -= OnFrameLoadEnd;

        lock (_eventGate)
        {
            _eventCallbacks.Clear();
            _pendingEventMessages.Clear();
        }
    }

    internal async Task<object?> InvokeFromJavascriptAsync(string handlerName, object? data)
    {
        ThrowIfDisposed();

        if (!_handlers.TryGetValue(handlerName, out var handler))
            throw new InvalidOperationException($"No JS bridge handler named '{handlerName}' is registered.");

        var result = await handler(JsonSerializer.Serialize(data));
        return JsonPayloadConverter.FromJson(result);
    }

    internal Task OnFromJavascriptAsync(string eventName, IJavascriptCallback callback)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(callback);

        lock (_eventGate)
        {
            if (!_eventCallbacks.TryGetValue(eventName, out var callbacks))
            {
                callbacks = new List<IJavascriptCallback>();
                _eventCallbacks[eventName] = callbacks;
            }

            callbacks.Add(callback);
        }

        return Task.CompletedTask;
    }

    private void OnBrowserInitializedChanged(object? sender, EventArgs e)
    {
        if (_browser.IsBrowserInitialized)
            _browserInitialized.TrySetResult(null);
    }

    private void OnFrameLoadStart(object? sender, FrameLoadStartEventArgs e)
    {
        if (!e.Frame.IsMain)
            return;

        _documentReady = false;

        lock (_eventGate)
        {
            _eventCallbacks.Clear();
            _pendingEventMessages.Clear();
        }
    }

    private async void OnFrameLoadEnd(object? sender, FrameLoadEndEventArgs e)
    {
        if (!e.Frame.IsMain)
            return;

        _documentReady = true;

        PendingEventMessage[] pending;
        lock (_eventGate)
        {
            pending = _pendingEventMessages.ToArray();
            _pendingEventMessages.Clear();
        }

        foreach (var message in pending)
            await DispatchEventCoreAsync(message.EventName, message.JsonPayload);
    }

    private async Task DispatchEventCoreAsync(string eventName, string jsonPayload)
    {
        List<IJavascriptCallback>? callbacks;
        lock (_eventGate)
        {
            callbacks = _eventCallbacks.TryGetValue(eventName, out var list)
                ? list.ToList()
                : null;
        }

        if (callbacks is null || callbacks.Count == 0)
            return;

        var payload = JsonPayloadConverter.FromJson(jsonPayload);
        var staleCallbacks = new List<IJavascriptCallback>();

        foreach (var callback in callbacks)
        {
            if (!callback.CanExecute)
            {
                staleCallbacks.Add(callback);
                continue;
            }

            try
            {
                await callback.ExecuteAsync(payload);
            }
            catch
            {
                staleCallbacks.Add(callback);
            }
        }

        if (staleCallbacks.Count == 0)
            return;

        lock (_eventGate)
        {
            if (!_eventCallbacks.TryGetValue(eventName, out var liveCallbacks))
                return;

            foreach (var staleCallback in staleCallbacks)
                liveCallbacks.Remove(staleCallback);

            if (liveCallbacks.Count == 0)
                _eventCallbacks.Remove(eventName);
        }
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

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(CefSharpJsBridge));
    }

    private sealed class BoundOmniApi
    {
        private readonly CefSharpJsBridge _owner;

        public BoundOmniApi(CefSharpJsBridge owner)
        {
            _owner = owner;
            Window = new BoundWindowApi(owner);
        }

        public BoundWindowApi Window { get; }

        public Task<object?> Invoke(string handler, object? data = null)
            => _owner.InvokeFromJavascriptAsync(handler, data);

        public Task On(string eventName, IJavascriptCallback callback)
            => _owner.OnFromJavascriptAsync(eventName, callback);
    }

    private sealed class BoundWindowApi
    {
        private readonly CefSharpJsBridge _owner;

        public BoundWindowApi(CefSharpJsBridge owner)
        {
            _owner = owner;
        }

        public Task<object?> Minimize() => _owner.InvokeFromJavascriptAsync("window.minimize", null);

        public Task<object?> Maximize() => _owner.InvokeFromJavascriptAsync("window.maximize", null);

        public Task<object?> Close() => _owner.InvokeFromJavascriptAsync("window.close", null);

        public Task<object?> StartDrag(object? data = null) => _owner.InvokeFromJavascriptAsync("window.startDrag", data);

        public Task<object?> ShowSystemMenu(object? data = null) => _owner.InvokeFromJavascriptAsync("window.showSystemMenu", data);
    }

    private sealed record PendingEventMessage(string EventName, string JsonPayload);
}
