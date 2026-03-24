using System.Text.Json;
using OmniHost;
using OmniHost.WebView2;
using OmniHost.Windows;

var app = OmniApp.CreateBuilder(args)
    .Configure(options =>
    {
        options.Title = "OmniHost Basic Sample";
        options.CustomScheme = "app";
        options.ContentRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        options.StartUrl = "app://localhost/index.html";
        options.Width = 1100;
        options.Height = 720;
        options.EnableDevTools = true;
        options.WindowStyle = OmniWindowStyle.Frameless;
        options.ScrollBarMode = OmniScrollBarMode.Auto;
    })
    .AddWindow("secondary", options =>
    {
        options.Title = "OmniHost Secondary Window";
        options.StartUrl = "app://localhost/secondary.html";
        options.Width = 520;
        options.Height = 440;
        options.WindowStyle = OmniWindowStyle.Normal;
    })
    .UseAdapter(new WebView2AdapterFactory())
    .UseRuntime(new Win32Runtime())
    .UseDesktopApp(new SampleApp())
    .Build();

await app.RunAsync();

sealed class SampleApp : IDesktopApp
{
    public Task OnStartAsync(IWebViewAdapter adapter, CancellationToken cancellationToken = default)
    {
        adapter.JsBridge.RegisterHandler("greet", payload =>
        {
            var name = payload.Trim('"');
            return Task.FromResult(
                $"\"Hello, {name}! Greetings from .NET {Environment.Version} on {Environment.MachineName}.\"");
        });

        adapter.JsBridge.RegisterHandler("sysinfo", _ =>
        {
            var payload = JsonSerializer.Serialize(new
            {
                os = Environment.OSVersion.ToString(),
                dotnet = Environment.Version.ToString(),
                machine = Environment.MachineName,
                time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            });

            return Task.FromResult(payload);
        });

        _ = Task.Run(async () =>
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(5000, cancellationToken).ConfigureAwait(false);

                    var payload = JsonSerializer.Serialize(new
                    {
                        time = DateTime.Now.ToString("HH:mm:ss")
                    });

                    await adapter.JsBridge.PostMessageAsync("tick", payload).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task OnClosingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
