using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OmniWebHost.Core;

namespace OmniWebHost.Hosting;

/// <summary>
/// Extension methods for integrating OmniWebHost with <see cref="IHostBuilder"/>.
/// </summary>
public static class OmniWebHostHostingExtensions
{
    /// <summary>
    /// Adds OmniWebHost services and configures the application options.
    /// </summary>
    public static IHostBuilder UseOmniWebHost(
        this IHostBuilder builder,
        Action<OmniWebHostOptions>? configure = null)
    {
        builder.ConfigureServices((_, services) =>
        {
            var options = new OmniWebHostOptions();
            configure?.Invoke(options);
            services.AddSingleton(options);
        });

        return builder;
    }
}
