namespace OmniWebHost.Core;

/// <summary>
/// Internal default implementation of <see cref="IOmniWebHostApp"/>.
/// </summary>
internal sealed class OmniWebHostApp : IOmniWebHostApp
{
    private readonly OmniWebHostOptions _options;
    private readonly IWebViewAdapterFactory _adapterFactory;

    internal OmniWebHostApp(OmniWebHostOptions options, IWebViewAdapterFactory adapterFactory)
    {
        _options = options;
        _adapterFactory = adapterFactory;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // TODO: create host window, initialise adapter, run message loop.
        var adapter = _adapterFactory.Create();
        await adapter.InitializeAsync(nint.Zero, _options, cancellationToken);
        await adapter.NavigateAsync(_options.StartUrl, cancellationToken);
        await adapter.DisposeAsync();
    }
}
