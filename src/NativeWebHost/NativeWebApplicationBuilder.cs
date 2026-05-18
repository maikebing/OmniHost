using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NativeWebHost;

/// <summary>
/// 同时配置本地 ASP.NET Core 服务和 NativeWebHost 桌面壳的组合生成器。
/// </summary>
public sealed class NativeWebApplicationBuilder
{
    private readonly NativeWebHostBuilder _desktopBuilder;
    private readonly List<Action<NativeWebHostOptions, WebApplication>> _deferredDesktopConfigurations = new();
    private Func<WebApplication, IDesktopApp?>? _desktopAppFactory;

    internal NativeWebApplicationBuilder(WebApplicationBuilder webBuilder, string[] args)
    {
        Web = webBuilder ?? throw new ArgumentNullException(nameof(webBuilder));
        _desktopBuilder = new NativeWebHostBuilder().WithArgs(args);
    }

    /// <summary>底层 ASP.NET Core 生成器，供调用方配置 HTTP、DI、配置源和日志。</summary>
    public WebApplicationBuilder Web { get; }

    public IServiceCollection Services => Web.Services;

    public ConfigurationManager Configuration => Web.Configuration;

    public ConfigureHostBuilder Host => Web.Host;

    public ConfigureWebHostBuilder WebHost => Web.WebHost;

    public IWebHostEnvironment Environment => Web.Environment;

    /// <summary>配置桌面窗口选项。</summary>
    public NativeWebApplicationBuilder ConfigureDesktop(Action<NativeWebHostOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _deferredDesktopConfigurations.Add((options, _) => configure(options));
        return this;
    }

    /// <summary>配置桌面窗口选项，可读取已构建并启动后的 ASP.NET Core 应用状态。</summary>
    public NativeWebApplicationBuilder ConfigureDesktop(Action<NativeWebHostOptions, WebApplication> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _deferredDesktopConfigurations.Add(configure);
        return this;
    }

    /// <summary>注册浏览器适配器工厂。</summary>
    public NativeWebApplicationBuilder UseAdapter(IWebViewAdapterFactory factory)
    {
        _desktopBuilder.UseAdapter(factory);
        return this;
    }

    /// <summary>注册桌面运行时。</summary>
    public NativeWebApplicationBuilder UseRuntime(IDesktopRuntime runtime)
    {
        _desktopBuilder.UseRuntime(runtime);
        return this;
    }

    /// <summary>注册桌面应用生命周期回调。</summary>
    public NativeWebApplicationBuilder UseDesktopApp(IDesktopApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        _desktopAppFactory = _ => app;
        return this;
    }

    /// <summary>注册桌面应用生命周期回调，可基于已启动的本地服务生成。</summary>
    public NativeWebApplicationBuilder UseDesktopApp(Func<WebApplication, IDesktopApp?> factory)
    {
        _desktopAppFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        return this;
    }

    /// <summary>添加启动阶段的闪屏窗口。</summary>
    public NativeWebApplicationBuilder UseSplashScreen(Action<NativeWebHostOptions> configure)
    {
        _desktopBuilder.UseSplashScreen(configure);
        return this;
    }

    /// <summary>添加启动阶段的闪屏窗口。</summary>
    public NativeWebApplicationBuilder UseSplashScreen(string windowId, Action<NativeWebHostOptions> configure)
    {
        _desktopBuilder.UseSplashScreen(windowId, configure);
        return this;
    }

    /// <summary>添加启动时一并创建的额外窗口。</summary>
    public NativeWebApplicationBuilder AddWindow(string windowId, Action<NativeWebHostOptions> configure)
    {
        _desktopBuilder.AddWindow(windowId, configure);
        return this;
    }

    /// <summary>生成组合应用；调用方可继续在返回的 Web 应用上声明中间件和端点。</summary>
    public NativeWebApplicationHost Build()
        => new(
            Web.Build(),
            _desktopBuilder,
            _deferredDesktopConfigurations.ToArray(),
            _desktopAppFactory);
}
