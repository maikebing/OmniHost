using OmniWebHost;
using OmniWebHost.WebView2;

// ── Configure and build the application ──────────────────────────────────────
var app = OmniApp.CreateBuilder(args)
    .Configure(o =>
    {
        o.Title           = "OmniWebHost Basic Sample";
        o.CustomScheme    = "app";
        o.ContentRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        o.StartUrl        = "app://localhost/index.html";
        o.Width           = 1100;
        o.Height          = 720;
        o.EnableDevTools  = true;
    })
    .UseAdapter(new WebView2AdapterFactory())
    .UseRuntime(new Win32Runtime())
    .UseDesktopApp(new SampleApp())
    .Build();

await app.RunAsync();

// ── Application lifecycle class ───────────────────────────────────────────────
sealed class SampleApp : IDesktopApp
{
    private CancellationTokenSource? _cts;

    public Task OnStartAsync(IWebViewAdapter adapter, CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Register a handler callable from JavaScript via window.omni.invoke('greet', name)
        adapter.JsBridge.RegisterHandler("greet", async payload =>
        {
            // payload is a JSON string; strip quotes for a plain string argument
            var name = payload.Trim('"');
            return $"\"Hello, {name}! Greetings from .NET {Environment.Version} on {Environment.MachineName}.\"";
        });

        // Register a handler that returns system information
        adapter.JsBridge.RegisterHandler("sysinfo", async _ =>
        {
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                os      = Environment.OSVersion.ToString(),
                dotnet  = Environment.Version.ToString(),
                machine = Environment.MachineName,
                time    = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            });
        });

        // Push a "tick" event to the page every 5 seconds
        _ = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                await Task.Delay(5000, _cts.Token).ConfigureAwait(false);
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    time = DateTime.Now.ToString("HH:mm:ss")
                });
                await adapter.JsBridge.PostMessageAsync("tick", payload).ConfigureAwait(false);
            }
        }, _cts.Token);

        return Task.CompletedTask;
    }

    public Task OnClosingAsync(CancellationToken cancellationToken = default)
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }
}

