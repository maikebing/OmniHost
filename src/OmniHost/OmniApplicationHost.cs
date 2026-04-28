using Microsoft.AspNetCore.Builder;
using OmniHost.Core;

namespace OmniHost;

/// <summary>
/// 组合运行 ASP.NET Core 本地服务和 OmniHost 桌面消息循环。
/// </summary>
public sealed class OmniApplicationHost : IAsyncDisposable
{
    private readonly OmniHostBuilder _desktopBuilder;
    private readonly IReadOnlyList<Action<OmniHostOptions, WebApplication>> _desktopConfigurations;
    private readonly Func<WebApplication, IDesktopApp?>? _desktopAppFactory;
    private int _hasRun;

    internal OmniApplicationHost(
        WebApplication web,
        OmniHostBuilder desktopBuilder,
        IReadOnlyList<Action<OmniHostOptions, WebApplication>> desktopConfigurations,
        Func<WebApplication, IDesktopApp?>? desktopAppFactory)
    {
        Web = web ?? throw new ArgumentNullException(nameof(web));
        _desktopBuilder = desktopBuilder ?? throw new ArgumentNullException(nameof(desktopBuilder));
        _desktopConfigurations = desktopConfigurations ?? throw new ArgumentNullException(nameof(desktopConfigurations));
        _desktopAppFactory = desktopAppFactory;
    }

    /// <summary>底层 ASP.NET Core 应用，调用方在 RunAsync 前声明中间件和端点。</summary>
    public WebApplication Web { get; }

    /// <summary>
    /// 启动本地 HTTP 服务，然后进入桌面窗口消息循环；窗口退出后停止并释放 HTTP 服务。
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _hasRun, 1) != 0)
        {
            throw new InvalidOperationException("OmniApplication can only be run once.");
        }

        await Web.StartAsync(cancellationToken);

        try
        {
            var desktopApp = BuildDesktopApp();
            await desktopApp.RunAsync(cancellationToken);
        }
        finally
        {
            await Web.StopAsync(CancellationToken.None);
            await Web.DisposeAsync();
        }
    }

    public ValueTask DisposeAsync()
        => Web.DisposeAsync();

    private IOmniHostApp BuildDesktopApp()
    {
        foreach (var configure in _desktopConfigurations)
        {
            _desktopBuilder.Configure(options => configure(options, Web));
        }

        var desktopApp = _desktopAppFactory?.Invoke(Web);
        if (desktopApp is not null)
        {
            _desktopBuilder.UseDesktopApp(desktopApp);
        }

        return _desktopBuilder.Build();
    }
}
