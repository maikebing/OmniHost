using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace OmniHost.Gtk.Gtk;

internal sealed class GtkSynchronizationContext : SynchronizationContext
{
    private static readonly GtkNative.IdleCallback IdleDrain = DrainFromIdle;

    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = new();

    public override void Post(SendOrPostCallback d, object? state)
    {
        _queue.Enqueue((d, state));
        var callback = Marshal.GetFunctionPointerForDelegate(IdleDrain);
        GtkNative.GIdleAdd(callback, GCHandle.ToIntPtr(GCHandle.Alloc(this)));
    }

    public override void Send(SendOrPostCallback d, object? state)
        => d(state);

    private void DrainQueue()
    {
        while (_queue.TryDequeue(out var item))
            item.Callback(item.State);
    }

    private static int DrainFromIdle(IntPtr data)
    {
        var handle = GCHandle.FromIntPtr(data);

        try
        {
            if (handle.Target is GtkSynchronizationContext context)
                context.DrainQueue();
        }
        finally
        {
            handle.Free();
        }

        return 0;
    }
}
