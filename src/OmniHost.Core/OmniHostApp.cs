namespace OmniHost.Core;

/// <summary>
/// Internal default implementation of <see cref="IOmniHostApp"/>.
/// </summary>
internal sealed class OmniHostApp : IOmniHostApp
{
    private readonly OmniHostOptions _options;
    private readonly IReadOnlyList<OmniWindowDefinition> _additionalWindows;
    private readonly IWebViewAdapterFactory _adapterFactory;
    private readonly IDesktopRuntime _runtime;
    private readonly IDesktopApp? _desktopApp;

    internal OmniHostApp(
        OmniHostOptions options,
        IReadOnlyList<OmniWindowDefinition> additionalWindows,
        IWebViewAdapterFactory adapterFactory,
        IDesktopRuntime runtime,
        IDesktopApp? desktopApp)
    {
        _options = options;
        _additionalWindows = additionalWindows;
        _adapterFactory = adapterFactory;
        _runtime = runtime;
        _desktopApp = desktopApp;
    }

    public Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (_additionalWindows.Count == 0)
        {
            _runtime.Run(_options, _adapterFactory, _desktopApp);
            return Task.CompletedTask;
        }

        if (_runtime is not IMultiWindowDesktopRuntime multiWindowRuntime)
        {
            throw new NotSupportedException(
                $"Runtime '{_runtime.GetType().Name}' does not support multi-window startup. " +
                $"Use a runtime that implements {nameof(IMultiWindowDesktopRuntime)}.");
        }

        multiWindowRuntime.Run(_options, _additionalWindows, _adapterFactory, _desktopApp);
        return Task.CompletedTask;
    }
}
