# OmniHost

**A cross-platform .NET desktop WebView hosting framework.**

[![NuGet](https://img.shields.io/nuget/v/OmniHost?label=NuGet)](https://www.nuget.org/packages/OmniHost)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Build](https://github.com/maikebing/OmniHost/actions/workflows/build.yml/badge.svg)](https://github.com/maikebing/OmniHost/actions)

---

## What is OmniHost?

OmniHost lets you embed a web front-end inside a native desktop application using whichever browser engine is available on the target platform.
Think of it as a thin, pluggable host layer between your .NET application logic and the browser WebView control.

## Core Ideas

| Concept | Description |
|---------|-------------|
| Pluggable Adapters | Swap browser engines without touching application code |
| JS Bridge | Bidirectional messaging between C# and JavaScript |
| `OmniApp.CreateBuilder(args)` | Familiar, minimal-API entry point |
| Hosting integration | Works with `Microsoft.Extensions.Hosting` |

## MVP Scope (0.1.x preview)

The first preview ships the Windows/WebView2 path, plus an experimental first-pass Linux stack:

- `OmniHost.Abstractions` - all public interfaces and models
- `OmniHost.Core` - builder and app runner
- `OmniHost` - top-level `OmniApp` entry point
- `OmniHost.Hosting` - `IHostBuilder` extensions
- `OmniHost.Windows` - Windows runtime and raw Win32 host window
- `OmniHost.WinForms` - optional Windows Forms runtime and host window
- `OmniHost.WebView2` - WebView2 adapter
- `OmniHost.Cef` - experimental CEF/CefSharp adapter for Windows via WinForms hosting
- `OmniHost.Gtk` - first-pass Linux GTK runtime and host window package
- `OmniHost.WebKitGtk` - experimental WebKitGTK adapter for Linux

CEF and WKWebView (macOS) adapters remain planned for later milestones. See [ROADMAP.md](ROADMAP.md).

## Current Status

> **0.1.0-preview.3** - Windows/WebView2 remains the most complete path today. Linux now also has an experimental `OmniHost.Gtk` + `OmniHost.WebKitGtk` stack with GTK host-window support, WebKitGTK embedding, JS bridge wiring, and native `app://` custom-scheme asset loading.
> There is now also an experimental `OmniHost.Cef` path for Windows when you want a CefSharp-backed browser through `OmniHost.WinForms`.

## Quick Start

```csharp
using OmniHost;
using OmniHost.Windows;
using OmniHost.WebView2;

var app = OmniApp.CreateBuilder(args)
    .Configure(o =>
    {
        o.Title = "My App";
        o.CustomScheme = "app";
        o.ContentRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        o.StartUrl = "app://localhost/index.html";
    })
    .UseAdapter(new WebView2AdapterFactory())
    .UseRuntime(new Win32Runtime())
    .UseDesktopApp(new MyApp())
    .Build();

await app.RunAsync();
```

`OmniHost.WebView2` registers the custom `app://` scheme during WebView2 environment creation, so `StartUrl = "app://localhost/index.html"` works without extra WebView2 setup in your app code.

For window chrome and overflow control, you can also set `WindowStyle`, `BuiltInTitleBarStyle`, `ScrollBarMode`, and `ScrollBarCustomCss` on `OmniHostOptions`.
Windows currently exposes `Normal`, `Frameless`, `DwmBlurGlass`, and `VsCode` style presets through that same option.
`BuiltInTitleBarStyle` lets the host inject a maintained title bar preset such as `VsCode` or `Office`, so individual pages do not need to duplicate caption markup and window buttons.
The `DwmBlurGlass` preset intentionally stays on public DWM APIs, so it is a safe app-local approximation of the external `DWMBlurGlass` project rather than a system-wide hook/injection clone.
If you specifically want a Windows Forms host surface instead of the raw Win32 host, use the optional `OmniHost.WinForms` package and `new WinFormsRuntime()`.
If you want an experimental CEF-backed Windows path, pair `OmniHost.Cef` with `OmniHost.WinForms` and `new WinFormsRuntime()`.
You can declare additional startup windows with `AddWindow(...)` when the selected runtime supports `IMultiWindowDesktopRuntime`.
You can also declare a dedicated splash window with `UseSplashScreen(...)`; the helper registers `omni.invoke("splash.close")` so the main page or desktop app can dismiss it once startup work completes.
For dynamic window operations during runtime, use `IWindowAwareDesktopApp` together with `IOmniWindowManager`.
That manager can open windows, close windows, activate windows, look up live contexts by id, and post or broadcast host events.

On Linux, use `OmniHost.Gtk` with `OmniHost.WebKitGtk` for the experimental path. The current v1 flow targets GTK widget hosting and now serves `app://localhost/...` assets through a native WebKitGTK URI scheme handler.
There is now a matching sample project at `samples/OmniHost.Sample.Gtk`.
There is also `samples/OmniHost.Sample.CrossPlatform`, which auto-selects the Windows or Linux runtime/adapter pair based on the current platform.
For the experimental CEF path, inspect `samples/OmniHost.Sample.Cef`.
For the dedicated Windows style comparison demo, inspect `samples/OmniHost.Sample.WindowStyles`.
For web-stack embedding scenarios, inspect `samples/OmniHost.Sample.WebShowcase`, which covers ASP.NET MVC Core, Blazor, Vue 3 SPA, a third-party website, and a C# scripted API.
For a NativeAOT-friendly backend baseline, inspect `samples/OmniHost.Sample.AotMinimalApi`.

In `wwwroot/index.html` the bridge helper is auto-injected, so no script tag is required:

```js
const result = await omni.invoke('greet', 'World');

omni.on('tick', (data) => console.log(data.time));
omni.on('window.stateChanged', (data) => console.log(data.state));
```

Startup multi-window example:

```csharp
var app = OmniApp.CreateBuilder(args)
    .Configure(o => { o.Title = "Main"; o.StartUrl = "app://localhost/index.html"; })
    .AddWindow("secondary", o =>
    {
        o.Title = "Secondary";
        o.StartUrl = "app://localhost/secondary.html";
        o.Width = 520;
        o.Height = 440;
    })
    .UseAdapter(new WebView2AdapterFactory())
    .UseRuntime(new Win32Runtime())
    .Build();
```

Splash screen helper example:

```csharp
var app = OmniApp.CreateBuilder(args)
    .Configure(o =>
    {
        o.Title = "Main";
        o.CustomScheme = "app";
        o.ContentRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        o.StartUrl = "app://localhost/index.html";
    })
    .UseSplashScreen(o =>
    {
        o.Title = "Loading";
        o.StartUrl = "app://localhost/splash.html";
        o.Width = 560;
        o.Height = 320;
    })
    .UseAdapter(new WebView2AdapterFactory())
    .UseRuntime(new Win32Runtime())
    .Build();
```

From the main page, close the splash window when your first screen is ready:

```js
window.addEventListener('load', async () => {
  await omni.invoke('splash.close');
}, { once: true });
```

Context-aware window management example:

```csharp
sealed class MyApp : IWindowAwareDesktopApp
{
    public Task OnStartAsync(IWebViewAdapter adapter, CancellationToken ct = default) => Task.CompletedTask;
    public Task OnClosingAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task OnWindowStartAsync(OmniWindowContext window, CancellationToken ct = default)
    {
        if (window.IsMainWindow)
        {
            window.WindowManager.OpenWindow(new OmniWindowDefinition("tool", new OmniHostOptions
            {
                Title = "Tool",
                StartUrl = "app://localhost/tool.html",
                CustomScheme = "app",
                ContentRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
            }));
        }

        return Task.CompletedTask;
    }

    public Task OnWindowClosingAsync(OmniWindowContext window, CancellationToken ct = default)
        => Task.CompletedTask;
}
```

## Documentation

| Document | Description |
|----------|-------------|
| [Getting Started](docs/getting-started.md) | Installation and first app |
| [Architecture](docs/architecture.md) | Component design and boundaries |
| [JS Bridge](docs/js-bridge.md) | C# to JavaScript messaging |
| [Adapters](docs/adapters.md) | Browser engine adapters |
| [Migration Guide](MIGRATION.md) | Upgrade notes from `OmniWebHost*` to `OmniHost*` |
| [Roadmap](docs/roadmap.md) | Release plan |

## License

MIT - see [LICENSE](LICENSE).
