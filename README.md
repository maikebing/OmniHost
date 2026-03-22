# OmniWebHost

**A cross-platform .NET desktop WebView hosting framework.**

[![NuGet](https://img.shields.io/nuget/v/OmniWebHost?label=NuGet)](https://www.nuget.org/packages/OmniWebHost)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Build](https://github.com/maikebing/OmniWebHost/actions/workflows/build.yml/badge.svg)](https://github.com/maikebing/OmniWebHost/actions)

---

## What is OmniWebHost?

OmniWebHost lets you embed a web front-end inside a native desktop application using whichever browser engine is available on the target platform.  
Think of it as a **thin, pluggable host layer** between your .NET application logic and the browser WebView control.

```
┌───────────────────────────────────┐
│          Your .NET App            │
├───────────────────────────────────┤
│           OmniWebHost             │  ← unified API
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

- `OmniWebHost.Abstractions` — all public interfaces & models
- `OmniWebHost.Core` — builder + app runner
- `OmniWebHost` — top-level `OmniApp` entry point
- `OmniWebHost.Hosting` — `IHostBuilder` extensions
- `OmniWebHost.WebView2` — WebView2 adapter placeholder

CEF, WKWebView (macOS) and WebKitGTK (Linux) adapters are planned for later milestones — see [ROADMAP.md](ROADMAP.md).

---

## Current Status

> **0.1.0-preview.1** — Architecture baseline. Placeholder implementations only; not yet production-ready.

---

## Quick Start

```csharp
using OmniWebHost;
using OmniWebHost.WebView2;

var app = OmniApp.CreateBuilder(args)
    .Configure(o =>
    {
        o.Title           = "My App";
        o.CustomScheme    = "app";
        o.ContentRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        o.StartUrl        = "app://localhost/index.html";
    })
    .UseAdapter(new WebView2AdapterFactory())
    .UseRuntime(new WinFormsRuntime())
    .UseDesktopApp(new MyApp())
    .Build();

await app.RunAsync();
```

In `wwwroot/index.html` (bridge helper is auto-injected, no script tag needed):

```js
// JS → .NET
const result = await window.omni.invoke('greet', 'World');

// .NET → JS push events
window.omni.on('tick', (data) => console.log(data.time));
```

---

## Documentation

| Document | Description |
|----------|-------------|
| [Getting Started](docs/getting-started.md) | Installation and first app |
| [Architecture](docs/architecture.md) | Component design and boundaries |
| [JS Bridge](docs/js-bridge.md) | C# ↔ JavaScript messaging |
| [Adapters](docs/adapters.md) | Browser engine adapters |
| [Roadmap](docs/roadmap.md) | Release plan |

---

## License

MIT — see [LICENSE](LICENSE).
