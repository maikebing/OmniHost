using System.Collections.Concurrent;

namespace OmniWebHost.Core;

/// <summary>
/// Coordinates browser-adapter creation and host-window startup for the current application.
/// The first implementation is intentionally single-window and will be expanded later.
/// </summary>
public sealed class HostWindowCoordinator
{
    private const string MainWindowKey = "main";

    private readonly ConcurrentDictionary<string, TrackedHostWindow> _windows = new(StringComparer.Ordinal);
    private string? _mainWindowId;

    /// <summary>
    /// Gets the number of windows currently tracked as open.
    /// </summary>
    public int OpenWindowCount => _windows.Count;

    /// <summary>
    /// Gets the identifier of the current main window when one is running.
    /// </summary>
    public string? MainWindowId => _mainWindowId;

    /// <summary>
    /// Returns a snapshot of currently tracked open-window identifiers.
    /// </summary>
    public IReadOnlyCollection<string> GetOpenWindowIds()
        => _windows.Keys.OrderBy(static id => id, StringComparer.Ordinal).ToArray();

    /// <summary>
    /// Creates the main adapter and host window, then runs the window until it closes.
    /// </summary>
    public void RunMainWindow(
        OmniWebHostOptions options,
        IWebViewAdapterFactory adapterFactory,
        IHostWindowFactory windowFactory,
        IDesktopApp? desktopApp)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(adapterFactory);
        ArgumentNullException.ThrowIfNull(windowFactory);

        var adapter = adapterFactory.Create();
        var window = windowFactory.Create(options, adapter, desktopApp);
        var trackedWindow = new TrackedHostWindow(MainWindowKey, adapter.AdapterId, IsMainWindow: true, window);

        if (!_windows.TryAdd(MainWindowKey, trackedWindow))
            throw new InvalidOperationException("The main host window is already running.");

        _mainWindowId = MainWindowKey;

        try
        {
            window.Run();
        }
        finally
        {
            _windows.TryRemove(MainWindowKey, out _);

            if (string.Equals(_mainWindowId, MainWindowKey, StringComparison.Ordinal))
                _mainWindowId = null;
        }
    }

    private sealed record TrackedHostWindow(
        string WindowId,
        string AdapterId,
        bool IsMainWindow,
        IHostWindow Window);
}
