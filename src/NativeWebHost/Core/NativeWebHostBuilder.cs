namespace NativeWebHost;

/// <summary>
/// Builder for configuring and constructing an <see cref="INativeWebHostApp"/>.
/// Obtain an instance via <see cref="NativeWebApp.CreateBuilder"/>.
/// </summary>
public sealed class NativeWebHostBuilder
{
    private NativeWebHostOptions _options = new();
    private IWebViewAdapterFactory? _adapterFactory;
    private IDesktopRuntime? _runtime;
    private IDesktopApp? _desktopApp;
    private SplashRegistration? _splashRegistration;
    private readonly List<WindowRegistration> _additionalWindowRegistrations = new();
    private string[] _args = Array.Empty<string>();

    public NativeWebHostBuilder() { }

    public NativeWebHostBuilder WithArgs(string[] args)
    {
        _args = args;
        return this;
    }

    /// <summary>Configures the host options.</summary>
    public NativeWebHostBuilder Configure(Action<NativeWebHostOptions> configure)
    {
        configure(_options);
        return this;
    }

    /// <summary>Registers the adapter factory to use for creating the WebView.</summary>
    public NativeWebHostBuilder UseAdapter(IWebViewAdapterFactory factory)
    {
        _adapterFactory = factory;
        return this;
    }

    /// <summary>Registers the desktop runtime (window host + message loop).</summary>
    public NativeWebHostBuilder UseRuntime(IDesktopRuntime runtime)
    {
        _runtime = runtime;
        return this;
    }

    /// <summary>Registers application lifecycle callbacks.</summary>
    public NativeWebHostBuilder UseDesktopApp(IDesktopApp app)
    {
        _desktopApp = app;
        return this;
    }

    /// <summary>
    /// Adds a dedicated splash screen startup window using the default splash window id.
    /// The splash window inherits the current main-window options and then applies the supplied callback.
    /// </summary>
    public NativeWebHostBuilder UseSplashScreen(Action<NativeWebHostOptions> configure)
        => UseSplashScreen(NativeWebSplashScreen.DefaultWindowId, configure);

    /// <summary>
    /// Adds a dedicated splash screen startup window and wires the
    /// <c>nativeWeb.invoke("splash.close")</c> helper for all windows.
    /// </summary>
    public NativeWebHostBuilder UseSplashScreen(string windowId, Action<NativeWebHostOptions> configure)
    {
        if (string.IsNullOrWhiteSpace(windowId))
            throw new ArgumentException("Splash window id cannot be null or whitespace.", nameof(windowId));

        if (string.Equals(windowId, "main", StringComparison.Ordinal))
            throw new ArgumentException("The window id 'main' is reserved for the primary window.", nameof(windowId));

        ArgumentNullException.ThrowIfNull(configure);
        _splashRegistration = new SplashRegistration(windowId, configure);
        return this;
    }

    /// <summary>
    /// Adds an additional startup window. The window inherits the current main-window options
    /// and then applies the supplied configuration callback.
    /// </summary>
    public NativeWebHostBuilder AddWindow(string windowId, Action<NativeWebHostOptions> configure)
    {
        if (string.IsNullOrWhiteSpace(windowId))
            throw new ArgumentException("Window id cannot be null or whitespace.", nameof(windowId));

        ArgumentNullException.ThrowIfNull(configure);
        _additionalWindowRegistrations.Add(new WindowRegistration(windowId, configure));
        return this;
    }

    /// <summary>Builds and returns the configured application.</summary>
    public INativeWebHostApp Build()
    {
        if (_adapterFactory is null)
            throw new InvalidOperationException(
                "No IWebViewAdapterFactory registered. Call UseAdapter() before Build().");

        if (_runtime is null)
            throw new InvalidOperationException(
                "No IDesktopRuntime registered. Call UseRuntime() before Build().");

        return new NativeWebHostApp(
            _options.Clone(),
            BuildAdditionalWindows(),
            _adapterFactory,
            _runtime,
            BuildDesktopApp());
    }

    private IReadOnlyList<NativeWebWindowDefinition> BuildAdditionalWindows()
    {
        if (_additionalWindowRegistrations.Count == 0 && _splashRegistration is null)
            return Array.Empty<NativeWebWindowDefinition>();

        var expectedWindowCount = _additionalWindowRegistrations.Count + (_splashRegistration is null ? 0 : 1);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var windows = new List<NativeWebWindowDefinition>(expectedWindowCount);

        if (_splashRegistration is not null)
        {
            var splashOptions = _options.Clone();
            _splashRegistration.Configure(splashOptions);
            AddWindowDefinition(
                new NativeWebWindowDefinition(_splashRegistration.WindowId, splashOptions),
                windows,
                seenIds);
        }

        foreach (var registration in _additionalWindowRegistrations)
        {
            var options = _options.Clone();
            registration.Configure(options);
            AddWindowDefinition(
                new NativeWebWindowDefinition(registration.WindowId, options),
                windows,
                seenIds);
        }

        return windows;
    }

    private IDesktopApp? BuildDesktopApp()
        => _splashRegistration is null
            ? _desktopApp
            : new SplashScreenDesktopApp(_desktopApp, _splashRegistration.WindowId);

    private static void AddWindowDefinition(
        NativeWebWindowDefinition definition,
        ICollection<NativeWebWindowDefinition> windows,
        ISet<string> seenIds)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(seenIds);

        if (!seenIds.Add(definition.WindowId))
            throw new InvalidOperationException(
                $"A startup window with id '{definition.WindowId}' has already been configured.");

        windows.Add(definition);
    }

    private sealed record WindowRegistration(
        string WindowId,
        Action<NativeWebHostOptions> Configure);

    private sealed record SplashRegistration(
        string WindowId,
        Action<NativeWebHostOptions> Configure);
}
