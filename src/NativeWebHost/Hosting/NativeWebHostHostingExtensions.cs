using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NativeWebHost.Hosting;

/// <summary>
/// Extension methods for integrating NativeWebHost with <see cref="IHostBuilder"/>.
/// </summary>
public static class NativeWebHostHostingExtensions
{
    /// <summary>
    /// Adds NativeWebHost services and configures the application options.
    /// </summary>
    public static IHostBuilder UseNativeWebHost(
        this IHostBuilder builder,
        Action<NativeWebHostOptions>? configure = null)
    {
        builder.ConfigureServices((_, services) =>
        {
            var options = new NativeWebHostOptions();
            configure?.Invoke(options);
            services.AddSingleton(options);
        });

        return builder;
    }
}
