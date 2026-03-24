using System.Runtime.ExceptionServices;

namespace OmniWebHost.WebView2;

/// <summary>
/// AOT-compatible <see cref="IDesktopRuntime"/> that creates a raw Win32 window
/// and runs a native message loop — no WinForms or WPF dependency.
/// </summary>
/// <remarks>
/// The window is always created on a dedicated STA thread so that COM
/// (and therefore WebView2's COM-backed APIs) work correctly regardless of the
/// apartment state of the calling thread.
/// </remarks>
public sealed class Win32Runtime : IDesktopRuntime
{
    /// <inheritdoc/>
    public void Run(
        OmniWebHostOptions options,
        IWebViewAdapterFactory adapterFactory,
        IDesktopApp? desktopApp)
    {
        Exception? capturedException = null;

        var thread = new Thread(() =>
        {
            try
            {
                var adapter = adapterFactory.Create();
                var window  = new OmniHostWindow(options, adapter, desktopApp);
                window.Run();
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Name = "OmniWebHost-UI";
        thread.Start();
        thread.Join();

        if (capturedException is not null)
            ExceptionDispatchInfo.Capture(capturedException).Throw();
    }
}
