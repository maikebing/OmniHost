using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using OmniHost;
using OmniHost.Sample.WebShowcase.Models;
using OmniHost.Sample.WebShowcase.Services;
using OmniHost.WebView2;
using OmniHost.Windows;

var port = GetFreeTcpPort();
var webBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

webBuilder.WebHost.UseUrls($"http://127.0.0.1:{port}");
webBuilder.Services.AddControllersWithViews();
webBuilder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
webBuilder.Services.AddSingleton<ScriptedApiService>();

var webApp = webBuilder.Build();
webApp.UseStaticFiles();
webApp.UseAntiforgery();
webApp.MapControllers();
webApp.MapGet("/api/blazor/summary", () =>
    Results.Ok(new BlazorSummary(
        Environment.MachineName,
        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        "Blazor front-end with ASP.NET Core JSON backend",
        new[]
        {
            "Interactive server components",
            "Typed JSON endpoint",
            "OmniHost desktop shell over localhost"
        })));
webApp.MapGet("/api/vue/tasks", () =>
    Results.Ok(new[]
    {
        new VueTask("SSR-ready shell", "MVC / Blazor / SPA can share one host", true),
        new VueTask("Local API", "Vue fetches from ASP.NET Core endpoints", true),
        new VueTask("Desktop wrapper", "OmniHost opens the SPA inside a native window", false),
    }));
webApp.MapGet("/api/script/summary", async (ScriptedApiService scripting, CancellationToken cancellationToken) =>
    Results.Ok(await scripting.ExecuteSummaryAsync(cancellationToken)));
webApp.MapGet("/api/showcase/meta", () =>
    Results.Ok(new ShowcaseServerInfo(
        $"http://127.0.0.1:{port}",
        Environment.MachineName,
        Environment.Version.ToString(),
        new[]
        {
            "aspnet-mvc-core",
            "blazor-frontend",
            "spa-vue3",
            "third-party-site",
            "script-api"
        })));
webApp.MapRazorComponents<OmniHost.Sample.WebShowcase.Components.App>()
    .AddInteractiveServerRenderMode();

await webApp.StartAsync();

var serverUrl = webApp.Services
    .GetRequiredService<IServer>()
    .Features
    .Get<IServerAddressesFeature>()?
    .Addresses
    .Single() ?? $"http://127.0.0.1:{port}";

var app = OmniApp.CreateBuilder(args)
    .Configure(options =>
    {
        options.Title = "OmniHost Web Showcase";
        options.CustomScheme = "app";
        options.ContentRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        options.StartUrl = "app://localhost/launcher.html";
        options.Width = 1280;
        options.Height = 860;
        options.EnableDevTools = true;
        options.WindowStyle = OmniWindowStyle.VsCode;
        options.BuiltInTitleBarStyle = OmniBuiltInTitleBarStyle.VsCode;
    })
    .UseAdapter(new WebView2AdapterFactory())
    .UseRuntime(new Win32Runtime())
    .UseDesktopApp(new WebShowcaseDesktopApp(serverUrl))
    .Build();

try
{
    await app.RunAsync();
}
finally
{
    await webApp.StopAsync();
    await webApp.DisposeAsync();
}

static int GetFreeTcpPort()
{
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    return ((IPEndPoint)listener.LocalEndpoint).Port;
}

sealed class WebShowcaseDesktopApp(string serverUrl) : IWindowAwareDesktopApp
{
    private int _windowCount;

    private readonly IReadOnlyDictionary<string, ScenarioDefinition> _scenarios =
        new Dictionary<string, ScenarioDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["aspnet-mvc-core"] = new(
                "aspnet-mvc-core",
                "ASP.NET MVC Core",
                "/mvc",
                "Server-side MVC with Razor View rendering.",
                OmniWindowStyle.Normal),
            ["blazor-frontend"] = new(
                "blazor-frontend",
                "Blazor Frontend + ASP.NET Core API",
                "/blazor",
                "Interactive Blazor page backed by ASP.NET Core JSON endpoints.",
                OmniWindowStyle.Normal),
            ["spa-vue3"] = new(
                "spa-vue3",
                "ASP.NET Core SPA + Vue 3",
                "/spa-vue/index.html",
                "Vue 3 SPA served by ASP.NET Core static files and API endpoints.",
                OmniWindowStyle.Normal),
            ["third-party-site"] = new(
                "third-party-site",
                "Third-Party Website - Microsoft",
                "https://www.microsoft.com/",
                "External site hosted directly in OmniHost.",
                OmniWindowStyle.DwmBlurGlass),
            ["script-api"] = new(
                "script-api",
                "C# Script API",
                "/script-api/index.html",
                "Static client page reading JSON produced from a .csx backend script.",
                OmniWindowStyle.Normal),
        };

    public Task OnStartAsync(IWebViewAdapter adapter, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task OnClosingAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task OnWindowStartAsync(OmniWindowContext window, CancellationToken cancellationToken = default)
    {
        window.Adapter.JsBridge.RegisterHandler("showcase.serverInfo", _ =>
        {
            var payload = JsonSerializer.Serialize(new
            {
                serverUrl,
                machine = Environment.MachineName,
                adapter = window.Adapter.AdapterId,
                scenarios = _scenarios.Values.Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.Description,
                }),
                time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            });

            return Task.FromResult(payload);
        });

        window.Adapter.JsBridge.RegisterHandler("showcase.openScenario", payload =>
        {
            var scenarioId = payload.Trim('"');
            if (!_scenarios.TryGetValue(scenarioId, out var scenario))
            {
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    opened = false,
                    error = $"Unknown scenario '{scenarioId}'."
                }));
            }

            var definition = CreateScenarioWindow(scenario);
            window.WindowManager.OpenWindow(definition);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                opened = true,
                definition.WindowId,
                title = definition.Options.Title,
                definition.Options.StartUrl,
            }));
        });

        window.Adapter.JsBridge.RegisterHandler("showcase.listWindows", _ =>
        {
            var payload = JsonSerializer.Serialize(
                window.WindowManager.GetOpenWindows().Select(snapshot => new
                {
                    snapshot.WindowId,
                    snapshot.Options.Title,
                    snapshot.Options.StartUrl,
                    style = snapshot.Options.WindowStyle.ToCssToken(),
                }));

            return Task.FromResult(payload);
        });

        return Task.CompletedTask;
    }

    public Task OnWindowClosingAsync(OmniWindowContext window, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    private OmniWindowDefinition CreateScenarioWindow(ScenarioDefinition scenario)
    {
        var index = Interlocked.Increment(ref _windowCount);
        var isExternal = scenario.UrlOrPath.StartsWith("http", StringComparison.OrdinalIgnoreCase);
        var startUrl = isExternal ? scenario.UrlOrPath : $"{serverUrl}{scenario.UrlOrPath}";

        var options = new OmniHostOptions
        {
            Title = $"{scenario.Title} #{index}",
            StartUrl = startUrl,
            Width = 1380,
            Height = 920,
            EnableDevTools = true,
            WindowStyle = scenario.WindowStyle,
        };

        return new OmniWindowDefinition($"{scenario.Id}-{index}", options);
    }
}

sealed record ScenarioDefinition(
    string Id,
    string Title,
    string UrlOrPath,
    string Description,
    OmniWindowStyle WindowStyle);
