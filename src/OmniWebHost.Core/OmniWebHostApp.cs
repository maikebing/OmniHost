namespace OmniWebHost.Core;

/// <summary>
/// Internal default implementation of <see cref="IOmniWebHostApp"/>.
/// </summary>
internal sealed class OmniWebHostApp : IOmniWebHostApp
{
    private readonly OmniWebHostOptions _options;
    private readonly IWebViewAdapterFactory _adapterFactory;
    private readonly IDesktopRuntime _runtime;
    private readonly IDesktopApp? _desktopApp;

    internal OmniWebHostApp(
        OmniWebHostOptions options,
        IWebViewAdapterFactory adapterFactory,
        IDesktopRuntime runtime,
        IDesktopApp? desktopApp)
    {
        _options = options;
        _adapterFactory = adapterFactory;
        _runtime = runtime;
        _desktopApp = desktopApp;
    }

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        _runtime.Run(_options, _adapterFactory, _desktopApp);
        return Task.CompletedTask;
    }
}

