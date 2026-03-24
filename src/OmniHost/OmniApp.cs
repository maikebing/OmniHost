using OmniHost.Core;

namespace OmniHost;

/// <summary>
/// Top-level entry point for OmniHost applications.
/// </summary>
/// <example>
/// <code>
/// var app = OmniApp.CreateBuilder(args)
///     .Configure(o => { o.Title = "My App"; o.StartUrl = "https://example.com"; })
///     .UseAdapter(new MyAdapterFactory())
///     .Build();
///
/// await app.RunAsync();
/// </code>
/// </example>
public static class OmniApp
{
    /// <summary>
    /// Creates a new <see cref="OmniHostBuilder"/> with default configuration.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the application.</param>
    public static OmniHostBuilder CreateBuilder(string[]? args = null)
        => new OmniHostBuilder().WithArgs(args ?? Array.Empty<string>());
}
