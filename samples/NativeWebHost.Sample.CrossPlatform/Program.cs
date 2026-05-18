using System.Text.Json;
using NativeWebHost;
using NativeWebHost.Linux;
#if WINDOWS
using NativeWebHost.Windows;
#endif

var builder = NativeWebApp.CreateBuilder(args)
    .Configure(options =>
    {
        options.Title = "NativeWebHost Cross-Platform Sample";
        options.CustomScheme = "app";
        options.ContentRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        options.StartUrl = "app://localhost/index.html";
        options.Width = 1100;
        options.Height = 720;
        options.EnableDevTools = true;
        options.WindowStyle = OperatingSystem.IsWindows()
            ? NativeWebWindowStyle.VsCode
            : NativeWebWindowStyle.Normal;
        options.ScrollBarMode = NativeWebScrollBarMode.Auto;
    })
    .AddWindow("secondary", options =>
    {
        options.Title = "NativeWebHost Secondary Window";
        options.StartUrl = "app://localhost/secondary.html?window=secondary";
        options.Width = 520;
        options.Height = 440;
        options.WindowStyle = NativeWebWindowStyle.Normal;
    })
    .UseDesktopApp(new SampleApp());

ConfigureCurrentPlatform(builder);

await builder.Build().RunAsync();

static void ConfigureCurrentPlatform(NativeWebHostBuilder builder)
{
    if (OperatingSystem.IsLinux())
    {
        builder.UseAdapter(new WebKitGtkAdapterFactory())
            .UseRuntime(new GtkRuntime());
        return;
    }

#if WINDOWS
    if (OperatingSystem.IsWindows())
    {
        builder.UseAdapter(new NativeWebView2AdapterFactory())
            .UseRuntime(new Win32Runtime());
        return;
    }
#endif

    throw new PlatformNotSupportedException(
        "This sample currently supports Windows (Native WebView2) and Linux (GTK + WebKitGTK).");
}

sealed class SampleApp : IWindowAwareDesktopApp
{
    public Task OnStartAsync(IWebViewAdapter adapter, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task OnClosingAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task OnWindowStartAsync(NativeWebWindowContext window, CancellationToken cancellationToken = default)
    {
        window.Adapter.JsBridge.RegisterHandler("greet", payload =>
        {
            var name = payload.Trim('"');
            return Task.FromResult(
                $"\"Hello, {name}! This is window '{window.WindowId}' on {Environment.MachineName} using {window.Adapter.AdapterId}.\"");
        });

        window.Adapter.JsBridge.RegisterHandler("sysinfo", _ =>
        {
            var payload = JsonSerializer.Serialize(new
            {
                windowId = window.WindowId,
                isMain = window.IsMainWindow,
                os = Environment.OSVersion.ToString(),
                dotnet = Environment.Version.ToString(),
                machine = Environment.MachineName,
                adapter = window.Adapter.AdapterId,
                openWindows = window.WindowManager.GetOpenWindowIds(),
                time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            });

            return Task.FromResult(payload);
        });

        window.Adapter.JsBridge.RegisterHandler("windows.list", _ =>
        {
            var payload = JsonSerializer.Serialize(
                window.WindowManager.GetOpenWindows().Select(snapshot => new
                {
                    id = snapshot.WindowId,
                    isMain = snapshot.IsMainWindow,
                    title = snapshot.Options.Title,
                    startUrl = snapshot.Options.StartUrl,
                }));

            return Task.FromResult(payload);
        });

        window.Adapter.JsBridge.RegisterHandler("windows.contextById", payload =>
        {
            var windowId = payload.Trim('"');
            var context = window.WindowManager.GetWindowContext(windowId);

            var response = JsonSerializer.Serialize(context is null
                ? null
                : new
                {
                    id = context.WindowId,
                    isMain = context.IsMainWindow,
                    title = context.Options.Title,
                    startUrl = context.Options.StartUrl,
                    adapterId = context.Adapter.AdapterId,
                    openWindowCount = context.WindowManager.OpenWindowCount,
                });

            return Task.FromResult(response);
        });

        if (window.IsMainWindow)
        {
            window.Adapter.JsBridge.RegisterHandler("window.openToolWindow", _ =>
            {
                if (window.WindowManager.GetOpenWindowIds().Contains("inspector"))
                {
                    return Task.FromResult(JsonSerializer.Serialize(new
                    {
                        opened = false,
                        message = "Inspector window is already open."
                    }));
                }

                window.WindowManager.OpenWindow(CreateInspectorWindow());

                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    opened = true,
                    windowId = "inspector"
                }));
            });

            window.Adapter.JsBridge.RegisterHandler("window.closeById", payload =>
            {
                var windowId = payload.Trim('"');
                var closed = window.WindowManager.TryCloseWindow(windowId);

                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    windowId,
                    closed
                }));
            });

            window.Adapter.JsBridge.RegisterHandler("window.activateById", payload =>
            {
                var windowId = payload.Trim('"');
                var activated = window.WindowManager.TryActivateWindow(windowId);

                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    windowId,
                    activated
                }));
            });

            window.Adapter.JsBridge.RegisterHandler("windows.broadcastNotice", payload =>
            {
                var message = payload.Trim('"');
                return BroadcastNoticeAsync(window, message);
            });
        }

        _ = Task.Run(async () =>
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(5000, cancellationToken).ConfigureAwait(false);

                    var payload = JsonSerializer.Serialize(new
                    {
                        windowId = window.WindowId,
                        time = DateTime.Now.ToString("HH:mm:ss")
                    });

                    await window.Adapter.JsBridge.PostMessageAsync("tick", payload).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task OnWindowClosingAsync(NativeWebWindowContext window, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    private static NativeWebWindowDefinition CreateInspectorWindow()
    {
        var options = new NativeWebHostOptions
        {
            Title = "NativeWebHost Inspector Window",
            CustomScheme = "app",
            ContentRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot"),
            StartUrl = "app://localhost/secondary.html?window=inspector",
            Width = 500,
            Height = 380,
            EnableDevTools = true,
            WindowStyle = NativeWebWindowStyle.Normal,
            ScrollBarMode = NativeWebScrollBarMode.Auto,
        };

        return new NativeWebWindowDefinition("inspector", options);
    }

    private static async Task<string> BroadcastNoticeAsync(NativeWebWindowContext window, string message)
    {
        var payload = JsonSerializer.Serialize(new
        {
            message,
            from = window.WindowId,
            time = DateTime.Now.ToString("HH:mm:ss")
        });

        var recipients = await window.WindowManager.BroadcastEventAsync("host.notice", payload);

        return JsonSerializer.Serialize(new
        {
            recipients,
            message
        });
    }
}
