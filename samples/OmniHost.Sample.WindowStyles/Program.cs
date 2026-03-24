using System.Text.Json;
using OmniHost;
using OmniHost.WebView2;
using OmniHost.Windows;

var app = OmniApp.CreateBuilder(args)
    .Configure(options =>
    {
        options.Title = "OmniHost Blur Glass Demo";
        options.CustomScheme = "app";
        options.ContentRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        options.StartUrl = "app://localhost/blur-glass.html";
        options.Width = 1220;
        options.Height = 820;
        options.EnableDevTools = true;
        options.WindowStyle = OmniWindowStyle.DwmBlurGlass;
    })
    .AddWindow("vscode-startup", options =>
    {
        options.Title = "OmniHost VSCode Demo";
        options.CustomScheme = "app";
        options.ContentRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        options.StartUrl = "app://localhost/vscode.html?window=vscode-startup";
        options.Width = 1220;
        options.Height = 820;
        options.EnableDevTools = true;
        options.WindowStyle = OmniWindowStyle.VsCode;
    })
    .AddWindow("office-startup", options =>
    {
        options.Title = "OmniHost Office Demo";
        options.CustomScheme = "app";
        options.ContentRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        options.StartUrl = "app://localhost/office.html?window=office-startup";
        options.Width = 1320;
        options.Height = 860;
        options.EnableDevTools = true;
        options.WindowStyle = OmniWindowStyle.DwmBlurGlass;
    })
    .UseAdapter(new WebView2AdapterFactory())
    .UseRuntime(new Win32Runtime())
    .UseDesktopApp(new WindowStylesApp())
    .Build();

await app.RunAsync();

sealed class WindowStylesApp : IWindowAwareDesktopApp
{
    private int _blurGlassCount;
    private int _tabbedCount;
    private int _vscodeCount;
    private int _officeCount;

    public Task OnStartAsync(IWebViewAdapter adapter, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task OnClosingAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task OnWindowStartAsync(OmniWindowContext window, CancellationToken cancellationToken = default)
    {
        window.Adapter.JsBridge.RegisterHandler("sample.describe", _ =>
        {
            var payload = JsonSerializer.Serialize(new
            {
                windowId = window.WindowId,
                title = window.Options.Title,
                style = window.Options.WindowStyle.ToCssToken(),
                styleName = window.Options.WindowStyle.ToString(),
                isMainWindow = window.IsMainWindow,
                adapter = window.Adapter.AdapterId,
                os = Environment.OSVersion.ToString(),
                dotnet = Environment.Version.ToString(),
                machine = Environment.MachineName,
                openWindows = window.WindowManager.GetOpenWindows().Select(snapshot => new
                {
                    id = snapshot.WindowId,
                    title = snapshot.Options.Title,
                    style = snapshot.Options.WindowStyle.ToCssToken(),
                }),
                time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            });

            return Task.FromResult(payload);
        });

        window.Adapter.JsBridge.RegisterHandler("sample.windows", _ =>
        {
            var payload = JsonSerializer.Serialize(
                window.WindowManager.GetOpenWindows().Select(snapshot => new
                {
                    id = snapshot.WindowId,
                    title = snapshot.Options.Title,
                    style = snapshot.Options.WindowStyle.ToCssToken(),
                    startUrl = snapshot.Options.StartUrl,
                }));

            return Task.FromResult(payload);
        });

        window.Adapter.JsBridge.RegisterHandler("sample.openBlurGlass", _ =>
            OpenWindow(window, CreateBlurGlassWindow()));

        window.Adapter.JsBridge.RegisterHandler("sample.openTabbed", _ =>
            OpenWindow(window, CreateTabbedWindow()));

        window.Adapter.JsBridge.RegisterHandler("sample.openVsCode", _ =>
            OpenWindow(window, CreateVsCodeWindow()));

        window.Adapter.JsBridge.RegisterHandler("sample.openOffice", _ =>
            OpenWindow(window, CreateOfficeWindow()));

        window.Adapter.JsBridge.RegisterHandler("sample.closeById", payload =>
        {
            var windowId = payload.Trim('"');
            var closed = window.WindowManager.TryCloseWindow(windowId);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                windowId,
                closed,
            }));
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
                        windowId = window.WindowId,
                        style = window.Options.WindowStyle.ToCssToken(),
                        time = DateTime.Now.ToString("HH:mm:ss"),
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

    private Task<string> OpenWindow(OmniWindowContext window, OmniWindowDefinition definition)
    {
        window.WindowManager.OpenWindow(definition);
        return Task.FromResult(JsonSerializer.Serialize(new
        {
            opened = true,
            windowId = definition.WindowId,
            style = definition.Options.WindowStyle.ToCssToken(),
            startUrl = definition.Options.StartUrl,
        }));
    }

    private OmniWindowDefinition CreateBlurGlassWindow()
        => CreateDynamicWindow(
            "blur-glass",
            "Blur Glass",
            "blur-glass.html",
            OmniWindowStyle.DwmBlurGlass,
            Interlocked.Increment(ref _blurGlassCount),
            width: 1220,
            height: 820);

    private OmniWindowDefinition CreateTabbedWindow()
        => CreateDynamicWindow(
            "tabbed",
            "Tabbed",
            "tabbed.html",
            OmniWindowStyle.VsCode,
            Interlocked.Increment(ref _tabbedCount),
            width: 1220,
            height: 820);

    private OmniWindowDefinition CreateVsCodeWindow()
        => CreateDynamicWindow(
            "vscode",
            "VSCode",
            "vscode.html",
            OmniWindowStyle.VsCode,
            Interlocked.Increment(ref _vscodeCount),
            width: 1320,
            height: 860);

    private OmniWindowDefinition CreateOfficeWindow()
        => CreateDynamicWindow(
            "office",
            "Office",
            "office.html",
            OmniWindowStyle.DwmBlurGlass,
            Interlocked.Increment(ref _officeCount),
            width: 1360,
            height: 900);

    private static OmniWindowDefinition CreateDynamicWindow(
        string prefix,
        string titlePrefix,
        string page,
        OmniWindowStyle windowStyle,
        int index,
        int width,
        int height)
    {
        var windowId = $"{prefix}-{index}";
        var options = new OmniHostOptions
        {
            Title = $"OmniHost {titlePrefix} #{index}",
            CustomScheme = "app",
            ContentRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot"),
            StartUrl = $"app://localhost/{page}?window={windowId}",
            Width = width,
            Height = height,
            EnableDevTools = true,
            WindowStyle = windowStyle,
        };

        return new OmniWindowDefinition(windowId, options);
    }
}
