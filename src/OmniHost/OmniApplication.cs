using Microsoft.AspNetCore.Builder;
using OmniHost.Core;

namespace OmniHost;

/// <summary>
/// OmniHost 的 ASP.NET Core 组合宿主入口。
/// </summary>
public static class OmniApplication
{
    /// <summary>
    /// 创建包含 ASP.NET Core 默认配置的 OmniHost 应用生成器。
    /// </summary>
    public static OmniApplicationBuilder CreateBuilder(string[]? args = null)
    {
        var normalizedArgs = args ?? Array.Empty<string>();
        return new OmniApplicationBuilder(
            WebApplication.CreateBuilder(normalizedArgs),
            normalizedArgs);
    }

    /// <summary>
    /// 创建更适合 Native AOT 的精简 OmniHost 应用生成器。
    /// </summary>
    public static OmniApplicationBuilder CreateSlimBuilder(string[]? args = null)
    {
        var normalizedArgs = args ?? Array.Empty<string>();
        return new OmniApplicationBuilder(
            WebApplication.CreateSlimBuilder(normalizedArgs),
            normalizedArgs);
    }

    /// <summary>
    /// 创建最小依赖的 OmniHost 应用生成器，由调用方显式补齐所需服务。
    /// </summary>
    public static OmniApplicationBuilder CreateEmptyBuilder(string[]? args = null)
    {
        var normalizedArgs = args ?? Array.Empty<string>();
        return new OmniApplicationBuilder(
            WebApplication.CreateEmptyBuilder(new WebApplicationOptions
            {
                Args = normalizedArgs
            }),
            normalizedArgs);
    }

    /// <summary>
    /// <see cref="CreateEmptyBuilder"/> 的短名称别名。
    /// </summary>
    public static OmniApplicationBuilder CreateEmpty(string[]? args = null)
        => CreateEmptyBuilder(args);
}

/// <summary>
/// 兼容旧版 OmniHost 入口；新代码优先使用 <see cref="OmniApplication"/>。
/// </summary>
public static class OmniApp
{
    /// <summary>
    /// 创建只包含桌面壳配置的旧版生成器。
    /// </summary>
    public static OmniHostBuilder CreateBuilder(string[]? args = null)
        => new OmniHostBuilder().WithArgs(args ?? Array.Empty<string>());
}
