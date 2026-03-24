# OmniHost

**A cross-platform .NET desktop WebView hosting framework.**

[![NuGet](https://img.shields.io/nuget/v/OmniHost?label=NuGet)](https://www.nuget.org/packages/OmniHost)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Build](https://github.com/maikebing/OmniHost/actions/workflows/build.yml/badge.svg)](https://github.com/maikebing/OmniHost/actions)

---

## What is OmniHost?

OmniHost lets you embed a web front-end inside a native desktop application using whichever browser engine is available on the target platform.  
Think of it as a **thin, pluggable host layer** between your .NET application logic and the browser WebView control.

```
┌───────────────────────────────────┐
│          Your .NET App            │
├───────────────────────────────────┤
│            OmniHost            │  ← unified API
├───────────────────────────────────┤
│  WebView2  │  CEF  │  WKWebView  │  ← pluggable adapters
└───────────────────────────────────┘
```

---

## Core Ideas

| Concept | Description |
|---------|-------------|
| **Pluggable Adapters** | Swap browser engines without touching application code |
| **JS Bridge** | Bidirectional messaging between C# and JavaScript |
| **`OmniApp.CreateBuilder(args)`** | Familiar, minimal-API entry point |
| **Hosting integration** | Works with `Microsoft.Extensions.Hosting` |

---

## MVP Scope (0.1.x preview)

The first preview ships the **Windows / WebView2** path:

- `OmniHost.Abstractions` — all public interfaces & models
- `OmniHost.Core` — builder + app runner
- `OmniHost` — top-level `OmniApp` entry point
- `OmniHost.Hosting` — `IHostBuilder` extensions
- `OmniHost.Windows` — Windows runtime + raw Win32 host window
- `OmniHost.WebView2` — WebView2 adapter placeholder
- `OmniHost.Gtk` — first-pass Linux GTK runtime + host window package

CEF, WKWebView (macOS) and WebKitGTK (Linux) adapters are planned for later milestones — see [ROADMAP.md](ROADMAP.md).

---

## Current Status

> **0.1.0-preview.3** — Windows/WebView2 is the functional end-to-end path today. `OmniHost.Gtk` now provides an experimental first-pass Linux host runtime, pending a Linux browser adapter.

---

## Quick Start

```csharp
using OmniHost;
using OmniHost.Windows;
using OmniHost.WebView2;

var app = OmniApp.CreateBuilder(args)
    .Configure(o =>
    {
        o.Title           = "My App";
        o.CustomScheme    = "app";
        o.ContentRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        o.StartUrl        = "app://localhost/index.html";
    })
    .UseAdapter(new WebView2AdapterFactory())
    .UseRuntime(new Win32Runtime())   // AOT-compatible — no WinForms/WPF
    .UseDesktopApp(new MyApp())
    .Build();

await app.RunAsync();
```

`OmniHost.WebView2` registers the custom `app://` scheme during WebView2 environment creation, so `StartUrl = "app://localhost/index.html"` works without any extra WebView2 setup in your app code.
For window chrome and overflow control, you can also set `WindowStyle`, `ScrollBarMode`, and `ScrollBarCustomCss` on `OmniHostOptions`.
You can also declare additional startup windows with `AddWindow(...)` when the selected runtime supports `IMultiWindowDesktopRuntime`.
For dynamic window operations during runtime, use `IWindowAwareDesktopApp` together with `IOmniWindowManager`.
That manager can now open windows, close windows, activate windows, look up live contexts by id, and post or broadcast host events.

In `wwwroot/index.html` (bridge helper is auto-injected, no script tag needed):

```js
// JS → .NET
const result = await omni.invoke('greet', 'World');

// .NET → JS push events
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

---

## Documentation

| Document | Description |
|----------|-------------|
| [Getting Started](docs/getting-started.md) | Installation and first app |
| [Architecture](docs/architecture.md) | Component design and boundaries |
| [JS Bridge](docs/js-bridge.md) | C# ↔ JavaScript messaging |
| [Adapters](docs/adapters.md) | Browser engine adapters |
| [Migration Guide](MIGRATION.md) | Upgrade notes from `OmniWebHost*` to `OmniHost*` |
| [Roadmap](docs/roadmap.md) | Release plan |

---

## License

MIT — see [LICENSE](LICENSE).
