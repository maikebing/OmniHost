using System.Text.Json;
using OmniHost;
using OmniHost.Core;
using OmniHost.Gtk;
using OmniHost.WebKitGtk;
#if WINDOWS
using OmniHost.WebView2;
using OmniHost.Windows;
#endif

var builder = OmniApp.CreateBuilder(args)
    .Configure(options =>
    {
        options.Title = "OmniHost Cross-Platform Sample";
        options.CustomScheme = "app";
        options.ContentRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        options.StartUrl = "app://localhost/index.html";
        options.Width = 1100;
        options.Height = 720;
        options.EnableDevTools = true;
        options.WindowStyle = OperatingSystem.IsWindows()
            ? OmniWindowStyle.VsCode
            : OmniWindowStyle.Normal;
        options.ScrollBarMode = OmniScrollBarMode.Auto;
    })
    .AddWindow("secondary", options =>
    {
        options.Title = "OmniHost Secondary Window";
        options.StartUrl = "app://localhost/secondary.html?window=secondary";
        options.Width = 520;
        options.Height = 440;
        options.WindowStyle = OmniWindowStyle.Normal;
    })
    .UseDesktopApp(new SampleApp());

ConfigureCurrentPlatform(builder);

await builder.Build().RunAsync();

static void ConfigureCurrentPlatform(OmniHostBuilder builder)
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
        builder.UseAdapter(new WebView2AdapterFactory())
            .UseRuntime(new Win32Runtime());
        return;
    }
#endif

    throw new PlatformNotSupportedException(
        "This sample currently supports Windows (WebView2) and Linux (GTK + WebKitGTK).");
}

sealed class SampleApp : IWindowAwareDesktopApp
{
    public Task OnStartAsync(IWebViewAdapter adapter, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task OnClosingAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task OnWindowStartAsync(OmniWindowContext window, CancellationToken cancellationToken = default)
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

    public Task OnWindowClosingAsync(OmniWindowContext window, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    private static OmniWindowDefinition CreateInspectorWindow()
    {
        var options = new OmniHostOptions
        {
            Title = "OmniHost Inspector Window",
            CustomScheme = "app",
            ContentRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot"),
            StartUrl = "app://localhost/secondary.html?window=inspector",
            Width = 500,
            Height = 380,
            EnableDevTools = true,
            WindowStyle = OmniWindowStyle.Normal,
            ScrollBarMode = OmniScrollBarMode.Auto,
        };

        return new OmniWindowDefinition("inspector", options);
    }

    private static async Task<string> BroadcastNoticeAsync(OmniWindowContext window, string message)
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
