namespace NativeWebHost;

/// <summary>
/// Internal default implementation of <see cref="INativeWebHostApp"/>.
/// </summary>
internal sealed class NativeWebHostApp : INativeWebHostApp
{
    private readonly NativeWebHostOptions _options;
    private readonly IReadOnlyList<NativeWebWindowDefinition> _additionalWindows;
    private readonly IWebViewAdapterFactory _adapterFactory;
    private readonly IDesktopRuntime _runtime;
    private readonly IDesktopApp? _desktopApp;

    internal NativeWebHostApp(
        NativeWebHostOptions options,
        IReadOnlyList<NativeWebWindowDefinition> additionalWindows,
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
