namespace OmniWebHost.Core;

/// <summary>
/// Builder for configuring and constructing an <see cref="IOmniWebHostApp"/>.
/// Obtain an instance via <see cref="OmniApp.CreateBuilder"/>.
/// </summary>
public sealed class OmniWebHostBuilder
{
    private OmniWebHostOptions _options = new();
    private IWebViewAdapterFactory? _adapterFactory;
    private IDesktopRuntime? _runtime;
    private IDesktopApp? _desktopApp;
    private string[] _args = Array.Empty<string>();

    public OmniWebHostBuilder() { }

    public OmniWebHostBuilder WithArgs(string[] args)
    {
        _args = args;
        return this;
    }

    /// <summary>Configures the host options.</summary>
    public OmniWebHostBuilder Configure(Action<OmniWebHostOptions> configure)
    {
        configure(_options);
        return this;
    }

    /// <summary>Registers the adapter factory to use for creating the WebView.</summary>
    public OmniWebHostBuilder UseAdapter(IWebViewAdapterFactory factory)
    {
        _adapterFactory = factory;
        return this;
    }

    /// <summary>Registers the desktop runtime (window host + message loop).</summary>
    public OmniWebHostBuilder UseRuntime(IDesktopRuntime runtime)
    {
        _runtime = runtime;
        return this;
    }

    /// <summary>Registers application lifecycle callbacks.</summary>
    public OmniWebHostBuilder UseDesktopApp(IDesktopApp app)
    {
        _desktopApp = app;
        return this;
    }

    /// <summary>Builds and returns the configured application.</summary>
    public IOmniWebHostApp Build()
    {
        if (_adapterFactory is null)
            throw new InvalidOperationException(
                "No IWebViewAdapterFactory registered. Call UseAdapter() before Build().");

        if (_runtime is null)
            throw new InvalidOperationException(
                "No IDesktopRuntime registered. Call UseRuntime() before Build().");

        return new OmniWebHostApp(_options, _adapterFactory, _runtime, _desktopApp);
    }
}

