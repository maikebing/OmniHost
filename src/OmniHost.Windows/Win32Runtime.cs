using System.Runtime.ExceptionServices;
using System.Collections.Concurrent;
using OmniHost.Core;

namespace OmniHost.Windows;

/// <summary>
/// AOT-compatible <see cref="IDesktopRuntime"/> that creates a raw Win32 window
/// and runs a native message loop — no WinForms or WPF dependency.
/// </summary>
/// <remarks>
/// The window is always created on a dedicated STA thread so that COM
/// (and therefore WebView2's COM-backed APIs) work correctly regardless of the
/// apartment state of the calling thread.
/// </remarks>
public sealed class Win32Runtime : IMultiWindowDesktopRuntime
{
    private readonly IHostWindowFactory _windowFactory;
    private readonly HostWindowCoordinator _coordinator;

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
        : this(windowFactory, new HostWindowCoordinator())
    {
    }

    internal Win32Runtime(IHostWindowFactory windowFactory, HostWindowCoordinator coordinator)
    {
        _windowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    /// <inheritdoc/>
    public void Run(
        OmniHostOptions options,
        IWebViewAdapterFactory adapterFactory,
        IDesktopApp? desktopApp)
        => Run(options, Array.Empty<OmniWindowDefinition>(), adapterFactory, desktopApp);

    /// <inheritdoc/>
    public void Run(
        OmniHostOptions options,
        IReadOnlyList<OmniWindowDefinition> additionalWindows,
        IWebViewAdapterFactory adapterFactory,
        IDesktopApp? desktopApp)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(additionalWindows);
        ArgumentNullException.ThrowIfNull(adapterFactory);

        var capturedExceptions = new ConcurrentQueue<Exception>();
        var threads = new List<Thread>(additionalWindows.Count + 1)
        {
            CreateWindowThread(
                "main",
                () => _coordinator.RunMainWindow(options, adapterFactory, _windowFactory, desktopApp),
                capturedExceptions)
        };

        foreach (var window in additionalWindows)
        {
            ArgumentNullException.ThrowIfNull(window);

            threads.Add(CreateWindowThread(
                window.WindowId,
                () => _coordinator.RunAdditionalWindow(
                    window.WindowId,
                    window.Options,
                    adapterFactory,
                    _windowFactory,
                    desktopApp),
                capturedExceptions));
        }

        foreach (var thread in threads)
            thread.Start();

        foreach (var thread in threads)
            thread.Join();

        if (capturedExceptions.IsEmpty)
            return;

        var exceptions = capturedExceptions.ToArray();
        if (exceptions.Length == 1)
            ExceptionDispatchInfo.Capture(exceptions[0]).Throw();

        throw new AggregateException("One or more OmniHost windows failed.", exceptions);
    }

    private static Thread CreateWindowThread(
        string windowId,
        Action runWindow,
        ConcurrentQueue<Exception> capturedExceptions)
    {
        var thread = new Thread(() =>
        {
            try
            {
                runWindow();
            }
            catch (Exception ex)
            {
                capturedExceptions.Enqueue(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Name = string.Equals(windowId, "main", StringComparison.Ordinal)
            ? "OmniHost-UI"
            : $"OmniHost-UI-{windowId}";

        return thread;
    }
}
