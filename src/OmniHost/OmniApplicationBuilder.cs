using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OmniHost.Core;

namespace OmniHost;

/// <summary>
/// 同时配置本地 ASP.NET Core 服务和 OmniHost 桌面壳的组合生成器。
/// </summary>
public sealed class OmniApplicationBuilder
{
    private readonly OmniHostBuilder _desktopBuilder;
    private readonly List<Action<OmniHostOptions, WebApplication>> _deferredDesktopConfigurations = new();
    private Func<WebApplication, IDesktopApp?>? _desktopAppFactory;

    internal OmniApplicationBuilder(WebApplicationBuilder webBuilder, string[] args)
    {
        Web = webBuilder ?? throw new ArgumentNullException(nameof(webBuilder));
        _desktopBuilder = new OmniHostBuilder().WithArgs(args);
    }

    /// <summary>底层 ASP.NET Core 生成器，供调用方配置 HTTP、DI、配置源和日志。</summary>
    public WebApplicationBuilder Web { get; }

    public IServiceCollection Services => Web.Services;

    public ConfigurationManager Configuration => Web.Configuration;

    public ConfigureHostBuilder Host => Web.Host;

    public ConfigureWebHostBuilder WebHost => Web.WebHost;

    public IWebHostEnvironment Environment => Web.Environment;

    /// <summary>配置桌面窗口选项。</summary>
    public OmniApplicationBuilder ConfigureDesktop(Action<OmniHostOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _deferredDesktopConfigurations.Add((options, _) => configure(options));
        return this;
    }

    /// <summary>配置桌面窗口选项，可读取已构建并启动后的 ASP.NET Core 应用状态。</summary>
    public OmniApplicationBuilder ConfigureDesktop(Action<OmniHostOptions, WebApplication> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _deferredDesktopConfigurations.Add(configure);
        return this;
    }

    /// <summary>注册浏览器适配器工厂。</summary>
    public OmniApplicationBuilder UseAdapter(IWebViewAdapterFactory factory)
    {
        _desktopBuilder.UseAdapter(factory);
        return this;
    }

    /// <summary>注册桌面运行时。</summary>
    public OmniApplicationBuilder UseRuntime(IDesktopRuntime runtime)
    {
        _desktopBuilder.UseRuntime(runtime);
        return this;
    }

    /// <summary>注册桌面应用生命周期回调。</summary>
    public OmniApplicationBuilder UseDesktopApp(IDesktopApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        _desktopAppFactory = _ => app;
        return this;
    }

    /// <summary>注册桌面应用生命周期回调，可基于已启动的本地服务生成。</summary>
    public OmniApplicationBuilder UseDesktopApp(Func<WebApplication, IDesktopApp?> factory)
    {
        _desktopAppFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        return this;
    }

    /// <summary>添加启动阶段的闪屏窗口。</summary>
    public OmniApplicationBuilder UseSplashScreen(Action<OmniHostOptions> configure)
    {
        _desktopBuilder.UseSplashScreen(configure);
        return this;
    }

    /// <summary>添加启动阶段的闪屏窗口。</summary>
    public OmniApplicationBuilder UseSplashScreen(string windowId, Action<OmniHostOptions> configure)
    {
        _desktopBuilder.UseSplashScreen(windowId, configure);
        return this;
    }

    /// <summary>添加启动时一并创建的额外窗口。</summary>
    public OmniApplicationBuilder AddWindow(string windowId, Action<OmniHostOptions> configure)
    {
        _desktopBuilder.AddWindow(windowId, configure);
        return this;
    }

    /// <summary>生成组合应用；调用方可继续在返回的 Web 应用上声明中间件和端点。</summary>
    public OmniApplicationHost Build()
        => new(
            Web.Build(),
            _desktopBuilder,
            _deferredDesktopConfigurations.ToArray(),
            _desktopAppFactory);
}
