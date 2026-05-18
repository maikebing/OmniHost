# Migration Guide

This guide covers the rename from `OmniWebHost*` to `OmniHost*` and the native-shell cleanup.

## Rename

- Repository name: `OmniWebHost` -> `OmniHost`
- Solution file: `OmniWebHost.sln` -> `OmniHost.sln`
- Package / assembly / namespace family:
  - `OmniWebHost` -> `OmniHost`
  - `OmniWebHost.Abstractions` -> `OmniHost.Abstractions`
  - `OmniWebHost.Core` -> `OmniHost.Core`
  - `OmniWebHost.Hosting` -> `OmniHost.Hosting`
  - `OmniWebHost.Windows` -> `OmniHost.Windows`

## Windows Adapter Change

Use `OmniHost.NativeWebView2` instead of the removed `OmniHost.WebView2` package:

```csharp
using OmniHost;
using OmniHost.NativeWebView2;
using OmniHost.Windows;

var app = OmniApp.CreateBuilder(args)
    .Configure(options =>
    {
        options.StartUrl = "app://localhost/index.html";
        options.ContentRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
    })
    .UseAdapter(new NativeWebView2AdapterFactory())
    .UseRuntime(new Win32Runtime())
    .Build();
```

Update project references:

```xml
<ProjectReference Include="..\\..\\src\\OmniHost\\OmniHost.csproj" />
<ProjectReference Include="..\\..\\src\\OmniHost.Windows\\OmniHost.Windows.csproj" />
<ProjectReference Include="..\\..\\src\\OmniHost.NativeWebView2\\OmniHost.NativeWebView2.csproj" />
```

Or package references:

```bash
dotnet add package OmniHost
dotnet add package OmniHost.Windows
dotnet add package OmniHost.NativeWebView2
```

## Removed Paths

The following packages/projects were removed from the supported framework surface:

- `OmniHost.WinForms`
- `OmniHost.WebView2`
- `OmniHost.Cef`
- `samples/OmniHost.Sample.Cef`

Use the native OS paths instead:

- Windows: `OmniHost.Windows` + `OmniHost.NativeWebView2`
- Linux: `OmniHost.Gtk` + `OmniHost.WebKitGtk`
- macOS: AppKit + WKWebView, planned

## Remote URL Note

If the Git hosting repository is renamed, update your local remote after the server-side rename is complete:

```bash
git remote set-url origin https://github.com/<owner>/OmniHost.git
```
