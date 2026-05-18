using Microsoft.AspNetCore.Builder;

namespace NativeWebHost;

/// <summary>
/// NativeWebHost 的 ASP.NET Core 组合宿主入口。
/// </summary>
public static class NativeWebApplication
{
    /// <summary>
    /// 创建包含 ASP.NET Core 默认配置的 NativeWebHost 应用生成器。
    /// </summary>
    public static NativeWebApplicationBuilder CreateBuilder(string[]? args = null)
    {
        var normalizedArgs = args ?? Array.Empty<string>();
        return new NativeWebApplicationBuilder(
            WebApplication.CreateBuilder(normalizedArgs),
            normalizedArgs);
    }

    /// <summary>
    /// 创建更适合 Native AOT 的精简 NativeWebHost 应用生成器。
    /// </summary>
    public static NativeWebApplicationBuilder CreateSlimBuilder(string[]? args = null)
    {
        var normalizedArgs = args ?? Array.Empty<string>();
        return new NativeWebApplicationBuilder(
            WebApplication.CreateSlimBuilder(normalizedArgs),
            normalizedArgs);
    }

    /// <summary>
    /// 创建最小依赖的 NativeWebHost 应用生成器，由调用方显式补齐所需服务。
    /// </summary>
    public static NativeWebApplicationBuilder CreateEmptyBuilder(string[]? args = null)
    {
        var normalizedArgs = args ?? Array.Empty<string>();
        return new NativeWebApplicationBuilder(
            WebApplication.CreateEmptyBuilder(new WebApplicationOptions
            {
                Args = normalizedArgs
            }),
            normalizedArgs);
    }

    /// <summary>
    /// <see cref="CreateEmptyBuilder"/> 的短名称别名。
    /// </summary>
    public static NativeWebApplicationBuilder CreateEmpty(string[]? args = null)
        => CreateEmptyBuilder(args);
}

/// <summary>
/// 兼容旧版 NativeWebHost 入口；新代码优先使用 <see cref="NativeWebApplication"/>。
/// </summary>
public static class NativeWebApp
{
    /// <summary>
    /// 创建只包含桌面壳配置的旧版生成器。
    /// </summary>
    public static NativeWebHostBuilder CreateBuilder(string[]? args = null)
        => new NativeWebHostBuilder().WithArgs(args ?? Array.Empty<string>());
}
