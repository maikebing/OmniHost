# Migration Guide

This guide covers the rename to `NativeWebHost` and the package consolidation.

## Package Shape

The supported NuGet surface is now four packages:

- `NativeWebHost`
- `NativeWebHost.Windows`
- `NativeWebHost.Linux`
- `NativeWebHost.Mac`

The old split implementation projects were merged and should not be referenced directly:

- `NativeWebHost.Abstractions`
- `NativeWebHost.Core`
- `NativeWebHost.Hosting`
- `NativeWebHost.NativeWebView2`
- `NativeWebHost.Gtk`
- `NativeWebHost.WebKitGtk`

## Windows

Use `NativeWebHost` plus `NativeWebHost.Windows`:

```csharp
using NativeWebHost;
using NativeWebHost.Windows;

var app = NativeWebApp.CreateBuilder(args)
    .Configure(options =>
    {
        options.StartUrl = "app://localhost/index.html";
        options.ContentRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
    })
    .UseAdapter(new NativeWebView2AdapterFactory())
    .UseRuntime(new Win32Runtime())
    .Build();
```

```bash
dotnet add package NativeWebHost
dotnet add package NativeWebHost.Windows
```

## Linux

Use `NativeWebHost` plus `NativeWebHost.Linux`:

```csharp
using NativeWebHost;
using NativeWebHost.Linux;

builder.UseAdapter(new WebKitGtkAdapterFactory())
    .UseRuntime(new GtkRuntime());
```

```bash
dotnet add package NativeWebHost
dotnet add package NativeWebHost.Linux
```

## Removed Paths

The framework no longer carries WinForms, WPF, CefSharp, or the old managed WebView2 shell.

Remote URL:

```bash
git remote set-url origin https://github.com/IoTSharp/NativeWebHost.git
```
