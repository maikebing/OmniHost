using System.Collections.Concurrent;

namespace OmniHost.Core;

/// <summary>
/// Coordinates browser-adapter creation and host-window startup for the current application.
/// Tracks the current main/auxiliary window set and runs each window through the selected host factory.
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
    /// Returns a snapshot of currently tracked open windows.
    /// </summary>
    public IReadOnlyCollection<HostWindowSnapshot> GetOpenWindows()
        => _windows.Values
            .OrderBy(static window => window.WindowId, StringComparer.Ordinal)
            .Select(static window => new HostWindowSnapshot(
                window.WindowId,
                window.AdapterId,
                window.IsMainWindow,
                window.Options.Clone()))
            .ToArray();

    /// <summary>
    /// Creates the main adapter and host window, then runs the window until it closes.
    /// </summary>
    public void RunMainWindow(
        OmniHostOptions options,
        IOmniWindowManager windowManager,
        IWebViewAdapterFactory adapterFactory,
        IHostWindowFactory windowFactory,
        IDesktopApp? desktopApp)
        => RunWindow(
            new HostWindowDefinition(MainWindowKey, options, IsMainWindow: true),
            windowManager,
            adapterFactory,
            windowFactory,
            desktopApp);

    /// <summary>
    /// Creates and runs an additional non-main host window.
    /// </summary>
    internal void RunAdditionalWindow(
        string windowId,
        OmniHostOptions options,
        IOmniWindowManager windowManager,
        IWebViewAdapterFactory adapterFactory,
        IHostWindowFactory windowFactory,
        IDesktopApp? desktopApp)
        => RunWindow(
            new HostWindowDefinition(windowId, options, IsMainWindow: false),
            windowManager,
            adapterFactory,
            windowFactory,
            desktopApp);

    private void RunWindow(
        HostWindowDefinition definition,
        IOmniWindowManager windowManager,
        IWebViewAdapterFactory adapterFactory,
        IHostWindowFactory windowFactory,
        IDesktopApp? desktopApp)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(definition.Options);
        ArgumentNullException.ThrowIfNull(windowManager);
        ArgumentNullException.ThrowIfNull(adapterFactory);
        ArgumentNullException.ThrowIfNull(windowFactory);

        var windowOptions = definition.Options.Clone();
        var adapter = adapterFactory.Create();
        EnsureSurfaceCompatibility(adapter, windowFactory);
        var windowContext = new OmniWindowContext(
            definition.WindowId,
            definition.IsMainWindow,
            windowOptions,
            adapter,
            windowManager);
        var window = windowFactory.Create(windowContext, desktopApp);
        var trackedWindow = new TrackedHostWindow(
            definition.WindowId,
            adapter.AdapterId,
            definition.IsMainWindow,
            windowOptions,
            window);

        if (!_windows.TryAdd(definition.WindowId, trackedWindow))
            throw new InvalidOperationException(
                $"A host window with id '{definition.WindowId}' is already running.");

        if (definition.IsMainWindow)
        {
            if (_mainWindowId is not null && !string.Equals(_mainWindowId, definition.WindowId, StringComparison.Ordinal))
            {
                _windows.TryRemove(definition.WindowId, out _);
                throw new InvalidOperationException("The main host window is already running.");
            }

            _mainWindowId = definition.WindowId;
        }

        try
        {
            window.Run();
        }
        finally
        {
            _windows.TryRemove(definition.WindowId, out _);

            if (definition.IsMainWindow
                && string.Equals(_mainWindowId, definition.WindowId, StringComparison.Ordinal))
                _mainWindowId = null;
        }
    }

    /// <summary>
    /// Requests that a tracked window close.
    /// </summary>
    public bool TryRequestClose(string windowId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(windowId);

        if (!_windows.TryGetValue(windowId, out var trackedWindow))
            return false;

        trackedWindow.Window.RequestClose();
        return true;
    }

    private sealed record TrackedHostWindow(
        string WindowId,
        string AdapterId,
        bool IsMainWindow,
        OmniHostOptions Options,
        IHostWindow Window);

    private static void EnsureSurfaceCompatibility(IWebViewAdapter adapter, IHostWindowFactory windowFactory)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(windowFactory);

        var supportedSurfaces = adapter.Capabilities.SupportedHostSurfaces;
        if (supportedSurfaces.Count == 0)
            return;

        if (supportedSurfaces.Contains(windowFactory.SurfaceKind))
            return;

        var supportedList = string.Join(", ", supportedSurfaces);
        throw new NotSupportedException(
            $"Adapter '{adapter.AdapterId}' does not support host surface '{windowFactory.SurfaceKind}'. " +
            $"Supported surfaces: {supportedList}.");
    }
}
