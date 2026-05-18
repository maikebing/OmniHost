# OmniHost

**A .NET desktop WebView host focused on native OS shells.**

[![NuGet](https://img.shields.io/nuget/v/OmniHost?label=NuGet)](https://www.nuget.org/packages/OmniHost)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Build](https://github.com/maikebing/OmniHost/actions/workflows/build.yml/badge.svg)](https://github.com/maikebing/OmniHost/actions)

OmniHost lets a .NET application host a web UI inside a native desktop window. The project now keeps one native path per operating system instead of carrying multiple UI-framework-specific shells.

## Platform Strategy

| OS | Window runtime | WebView adapter | Status |
|----|----------------|-----------------|--------|
| Windows | `OmniHost.Windows` raw Win32 | `OmniHost.NativeWebView2` | Primary path |
| Linux | `OmniHost.Gtk` GTK 3 | `OmniHost.WebKitGtk` | Experimental |
| macOS | AppKit | WKWebView | Planned |

Removed paths: `OmniHost.WinForms`, `OmniHost.WebView2`, and `OmniHost.Cef`. The Windows path is now raw Win32 plus WebView2Aot-based native WebView2, with no WinForms or WPF dependency.

The packaging goal is that end users do not install extra frameworks manually. Windows uses the OS WebView2 runtime or an app-packaged fixed WebView2 runtime. macOS uses system WebKit. Linux should package the needed GTK/WebKitGTK native libraries with the app image/package.

## Quick Start

```csharp
using OmniHost;
using OmniHost.NativeWebView2;
using OmniHost.Windows;

var app = OmniApp.CreateBuilder(args)
    .Configure(o =>
    {
        o.Title = "My App";
        o.CustomScheme = "app";
        o.ContentRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        o.StartUrl = "app://localhost/index.html";
        o.Width = 1280;
        o.Height = 800;
    })
    .UseAdapter(new NativeWebView2AdapterFactory())
    .UseRuntime(new Win32Runtime())
    .Build();

await app.RunAsync();
```

`OmniHost.NativeWebView2` supports `app://localhost/...` local assets, JavaScript bridge injection, custom window chrome helpers, and built-in title-bar presets.

Linux uses the same app code shape with `GtkRuntime` and `WebKitGtkAdapterFactory`. See `samples/OmniHost.Sample.CrossPlatform` for the platform switch.

## Features

- Native host windows with shared runtime/adapter abstractions
- `omni.invoke(...)` and `omni.on(...)` JavaScript bridge
- `app://localhost/...` local asset loading
- Multi-window startup and dynamic window management
- Splash windows
- Window style presets such as normal, frameless, DWM blur glass, and VS Code-style chrome where the OS runtime supports them
- ASP.NET Core, Blazor, Vue SPA, and static asset hosting examples

## Documentation

| Document | Description |
|----------|-------------|
| [Getting Started](docs/getting-started.md) | Installation and first app |
| [Architecture](docs/architecture.md) | Component design and boundaries |
| [JS Bridge](docs/js-bridge.md) | C# to JavaScript messaging |
| [Adapters](docs/adapters.md) | Browser engine adapters |
| [Migration Guide](MIGRATION.md) | Upgrade notes |
| [Roadmap](docs/roadmap.md) | Release plan |

## License

MIT - see [LICENSE](LICENSE).
