# Migration Guide

This guide covers the rename from `OmniWebHost*` to `OmniHost*`.

## What changed

- Repository name: `OmniWebHost` -> `OmniHost`
- Solution file: `OmniWebHost.sln` -> `OmniHost.sln`
- Package / assembly / namespace family:
  - `OmniWebHost` -> `OmniHost`
  - `OmniWebHost.Abstractions` -> `OmniHost.Abstractions`
  - `OmniWebHost.Core` -> `OmniHost.Core`
  - `OmniWebHost.Hosting` -> `OmniHost.Hosting`
  - `OmniWebHost.Windows` -> `OmniHost.Windows`
  - `OmniWebHost.WebView2` -> `OmniHost.WebView2`

## Common source changes

Update `using` directives:

```csharp
using OmniHost;
using OmniHost.Windows;
using OmniHost.WebView2;
```

Update renamed types and APIs:

```csharp
// before
var app = OmniApp.CreateBuilder(args);
IHostBuilder builder = ...;
builder.UseOmniWebHost();

// key renamed symbols
// OmniWebHostOptions -> OmniHostOptions
// OmniWebHostBuilder -> OmniHostBuilder
// IOmniWebHostApp -> IOmniHostApp
// UseOmniWebHost() -> UseOmniHost()
```

## Project file changes

Update package or project references:

```xml
<ProjectReference Include="..\\..\\src\\OmniHost\\OmniHost.csproj" />
<ProjectReference Include="..\\..\\src\\OmniHost.Windows\\OmniHost.Windows.csproj" />
<ProjectReference Include="..\\..\\src\\OmniHost.WebView2\\OmniHost.WebView2.csproj" />
```

NuGet package ids now follow the same `OmniHost*` naming:

```bash
dotnet add package OmniHost
dotnet add package OmniHost.Windows
dotnet add package OmniHost.WebView2
```

## Local repo rename

If you cloned the repository before the rename, you can either reclone it or rename
your local working folder from `OmniWebHost` to `OmniHost`.

## Remote URL note

If the Git hosting repository is also renamed, update your local remote after the
server-side rename is complete:

```bash
git remote set-url origin https://github.com/<owner>/OmniHost.git
```

Do this only after the remote repository actually exists at the new URL.
