using System.Runtime.ExceptionServices;

namespace OmniWebHost.Windows;

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
    private readonly IHostWindowFactory _windowFactory;

    /// <summary>
    /// Creates a Win32 runtime using the default raw Win32 host window implementation.
    /// </summary>
    public Win32Runtime()
        : this(new Win32HostWindowFactory())
    {
    }

    /// <summary>
    /// Creates a Win32 runtime with a custom host-window factory.
    /// </summary>
    public Win32Runtime(IHostWindowFactory windowFactory)
    {
        _windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
    }

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
                var window  = _windowFactory.Create(options, adapter, desktopApp);
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
