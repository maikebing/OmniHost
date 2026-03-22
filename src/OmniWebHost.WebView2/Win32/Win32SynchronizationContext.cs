using System.Collections.Concurrent;
using OmniWebHost.WebView2.Win32;

namespace OmniWebHost.WebView2;

/// <summary>
/// A <see cref="SynchronizationContext"/> that posts async continuations back to the
/// Win32 UI thread via <see cref="NativeMethods.PostMessageW"/>.
/// </summary>
internal sealed class Win32SynchronizationContext : SynchronizationContext
{
    private readonly IntPtr _hwnd;
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = new();

    internal Win32SynchronizationContext(IntPtr hwnd) => _hwnd = hwnd;

    /// <inheritdoc/>
    public override void Post(SendOrPostCallback d, object? state)
    {
        _queue.Enqueue((d, state));
        NativeMethods.PostMessageW(_hwnd, NativeMethods.WM_APP_DELEGATE, IntPtr.Zero, IntPtr.Zero);
    }

    /// <inheritdoc/>
    public override void Send(SendOrPostCallback d, object? state) => d(state);

    /// <inheritdoc/>
    public override SynchronizationContext CreateCopy() => this;

    /// <summary>
    /// Drains all pending continuations. Must be called on the UI thread
    /// each time a <c>WM_APP_DELEGATE</c> message is received.
    /// </summary>
    internal void DrainQueue()
    {
        while (_queue.TryDequeue(out var item))
            item.Callback(item.State);
    }
}
