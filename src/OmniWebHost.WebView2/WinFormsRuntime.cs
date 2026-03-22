using System.Runtime.ExceptionServices;

namespace OmniWebHost.WebView2;

/// <summary>
/// <see cref="IDesktopRuntime"/> implementation that hosts OmniWebHost in a WinForms window.
/// </summary>
public sealed class WinFormsRuntime : IDesktopRuntime
{
    /// <inheritdoc/>
    public void Run(
        OmniWebHostOptions options,
        IWebViewAdapterFactory adapterFactory,
        IDesktopApp? desktopApp)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            RunCore(options, adapterFactory, desktopApp);
        }
        else
        {
            // WinForms requires an STA thread; spin one up automatically.
            Exception? capturedException = null;
            var thread = new Thread(() =>
            {
                try { RunCore(options, adapterFactory, desktopApp); }
                catch (Exception ex) { capturedException = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Name = "OmniWebHost-UI";
            thread.Start();
            thread.Join();
            if (capturedException is not null)
                ExceptionDispatchInfo.Capture(capturedException).Throw();
        }
    }

    private static void RunCore(
        OmniWebHostOptions options,
        IWebViewAdapterFactory adapterFactory,
        IDesktopApp? desktopApp)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var adapter = adapterFactory.Create();
        using var form = new OmniHostForm(options, adapter, desktopApp);
        Application.Run(form);

        // Dispose the adapter after the message loop exits.
        adapter.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
