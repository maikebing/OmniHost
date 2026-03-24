---
title: Getting Started
---

# Getting Started

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download) or later
- Windows 10/11 with [WebView2 Evergreen Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) installed

## Installation

```bash
dotnet add package OmniWebHost
dotnet add package OmniWebHost.Windows
dotnet add package OmniWebHost.WebView2
```

## Your First App

Create a console project targeting `net8.0-windows`:

```csharp
using OmniWebHost;
using OmniWebHost.Windows;
using OmniWebHost.WebView2;

var app = OmniApp.CreateBuilder(args)
    .Configure(o =>
    {
        o.Title           = "Hello OmniWebHost";
        o.CustomScheme    = "app";
        o.ContentRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        o.StartUrl        = "app://localhost/index.html";
        o.Width           = 1280;
        o.Height          = 800;
        o.EnableDevTools  = true;
    })
    .UseAdapter(new WebView2AdapterFactory())
    .UseRuntime(new Win32Runtime())       // AOT-compatible, no WinForms/WPF
    .UseDesktopApp(new MyApp())
    .Build();

await app.RunAsync();

sealed class MyApp : IDesktopApp
{
    public Task OnStartAsync(IWebViewAdapter adapter, CancellationToken ct = default)
    {
        adapter.JsBridge.RegisterHandler("greet", async payload =>
        {
            var name = payload.Trim('"');
            return $"\"Hello, {name} from .NET {Environment.Version}!\"";
        });
        return Task.CompletedTask;
    }

    public Task OnClosingAsync(CancellationToken ct = default) => Task.CompletedTask;
}
```

`OmniWebHost.WebView2` registers the configured custom scheme when creating the underlying `CoreWebView2Environment`, so `app://localhost/index.html` can be used directly as the startup page.

Optional host-level behaviour can be configured through `OmniWebHostOptions`, including:

- `WindowStyle = OmniWindowStyle.Frameless` for custom HTML/CSS chrome
- `ScrollBarMode = OmniScrollBarMode.Auto`, `Hidden`, `VerticalOnly`, or `Custom`
- `ScrollBarCustomCss = "..."`

Add `wwwroot/index.html`:

```html
<!DOCTYPE html>
<html>
  <body>
    <button id="btn">Greet</button>
    <p id="out"></p>
    <script>
      document.getElementById('btn').onclick = async () => {
        document.getElementById('out').textContent =
          await omni.invoke('greet', 'World');
      };
    </script>
  </body>
</html>
```

The `omni` bridge helper is automatically injected by OmniWebHost before each page loads, so no extra script tag is required.

## Win32Runtime and STA

`Win32Runtime` always creates a dedicated STA thread internally, so you can call
`await app.RunAsync()` from a standard async `Main` without any extra threading setup.
It requires no WinForms or WPF dependency, making the application compatible with
.NET AOT (Ahead-of-Time) compilation.

## Next Steps

- [Architecture](architecture.md) — understand how the pieces fit together
- [JS Bridge](js-bridge.md) — full bridge API reference
- [Adapters](adapters.md) — choose or implement a browser adapter
