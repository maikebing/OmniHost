using OmniWebHost.Core;

namespace OmniWebHost;

/// <summary>
/// Top-level entry point for OmniWebHost applications.
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
    /// Creates a new <see cref="OmniWebHostBuilder"/> with default configuration.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the application.</param>
    public static OmniWebHostBuilder CreateBuilder(string[]? args = null)
        => new OmniWebHostBuilder().WithArgs(args ?? Array.Empty<string>());
}
