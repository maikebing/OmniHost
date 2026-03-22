---
title: Getting Started
---

# Getting Started

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download) or later
- Windows 10/11 with [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) installed (for the MVP path)

## Installation

```bash
dotnet add package OmniWebHost
dotnet add package OmniWebHost.WebView2
```

## Your First App

Create a new console/WinForms/WPF project targeting `net8.0-windows`, then:

```csharp
using OmniWebHost;
using OmniWebHost.WebView2;

var app = OmniApp.CreateBuilder(args)
    .Configure(o =>
    {
        o.Title    = "Hello OmniWebHost";
        o.StartUrl = "https://example.com";
        o.Width    = 1280;
        o.Height   = 800;
    })
    .UseAdapter(new WebView2AdapterFactory())
    .Build();

await app.RunAsync();
```

## With Microsoft.Extensions.Hosting

```csharp
using Microsoft.Extensions.Hosting;
using OmniWebHost.Hosting;
using OmniWebHost.WebView2;

var host = Host.CreateDefaultBuilder(args)
    .UseOmniWebHost(o =>
    {
        o.Title    = "Hello OmniWebHost";
        o.StartUrl = "https://example.com";
    })
    .Build();

await host.RunAsync();
```

## Next Steps

- [Architecture](architecture.md) — understand how the pieces fit together
- [JS Bridge](js-bridge.md) — call C# from JavaScript and vice-versa
- [Adapters](adapters.md) — choose or implement a browser adapter
