using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OmniHost.Core;

namespace OmniHost.Hosting;

/// <summary>
/// Extension methods for integrating OmniHost with <see cref="IHostBuilder"/>.
/// </summary>
public static class OmniHostHostingExtensions
{
    /// <summary>
    /// Adds OmniHost services and configures the application options.
    /// </summary>
    public static IHostBuilder UseOmniHost(
        this IHostBuilder builder,
        Action<OmniHostOptions>? configure = null)
    {
        builder.ConfigureServices((_, services) =>
        {
            var options = new OmniHostOptions();
            configure?.Invoke(options);
            services.AddSingleton(options);
        });

        return builder;
    }
}
