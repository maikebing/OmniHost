---
title: Adapters
---

# Adapters

OmniHost uses a pluggable adapter model.
Each adapter wraps a concrete browser engine and exposes a uniform `IWebViewAdapter` API.

## Available Adapters

| Package | Engine | Platform | Status |
|---------|--------|----------|--------|
| `OmniHost.WebView2` | Microsoft WebView2 | Windows 10/11 | Preview (functional) |
| `OmniHost.WebKitGtk` | WebKitGTK | Linux | Preview (experimental) |
| `OmniHost.WKWebView` | WKWebView | macOS | Planned |
| `OmniHost.Cef` | Chromium Embedded Framework | Windows / macOS / Linux | Planned |

`OmniHost.WebKitGtk` is the first Linux browser adapter and is designed to pair with `OmniHost.Gtk`.

Current experimental scope:

- attach a WebKitGTK web view to a `HostSurfaceKind.GtkWidget`
- enable JavaScript execution and the `omni` JS bridge
- support host-to-page events through the shared bridge contract
- serve `app://localhost/...` assets through a native WebKitGTK URI scheme handler when `ContentRootPath` is configured
- expose basic Linux window bridge actions for minimize, maximize, close, drag start, and system-menu attempts

Current v1 limitations:

- Linux window chrome behavior still needs broader real-world validation across distros and window managers
- `showSystemMenu` remains best-effort and depends on the current GTK/GDK event context

## Using an Adapter

Pass the adapter factory to the builder:

```csharp
.UseAdapter(new WebView2AdapterFactory())
```

Linux example:

```csharp
.UseAdapter(new WebKitGtkAdapterFactory())
```

Or register via DI and let the host resolve it automatically:

```csharp
services.AddSingleton<IWebViewAdapterFactory, WebView2AdapterFactory>();
```

## Implementing a Custom Adapter

1. Reference `OmniHost.Abstractions`.
2. Implement `IWebViewAdapterFactory`:

   ```csharp
   public class MyAdapterFactory : IWebViewAdapterFactory
   {
       public string AdapterId => "my-engine";
       public bool IsAvailable => /* runtime check */;
       public IWebViewAdapter Create() => new MyAdapter();
   }
   ```

3. Implement `IWebViewAdapter`:

   ```csharp
   public class MyAdapter : IWebViewAdapter
   {
       public string AdapterId => "my-engine";
       public BrowserCapabilities Capabilities => new() { EngineName = "MyEngine", ... };
       public IJsBridge JsBridge => _bridge;
       public Task InitializeAsync(HostSurfaceDescriptor surface, OmniHostOptions options, CancellationToken ct = default) { ... }
       public Task NavigateAsync(string url, CancellationToken ct = default) { ... }
       public ValueTask DisposeAsync() { ... }
   }
   ```

`HostSurfaceDescriptor` is the forward-looking API because not every engine will always attach to a plain `HWND`.
A legacy raw-handle overload still exists for simple adapters during the transition.

Adapters should also report their supported `HostSurfaceKind` values through `BrowserCapabilities.SupportedHostSurfaces` so the host can reject incompatible runtime/window combinations early.

## Capability Detection

Check `IWebViewAdapterFactory.IsAvailable` before registering:

```csharp
var factory = new WebView2AdapterFactory();
if (!factory.IsAvailable)
    throw new PlatformNotSupportedException("WebView2 runtime is not installed.");
```

Linux example:

```csharp
var factory = new WebKitGtkAdapterFactory();
if (!factory.IsAvailable)
    throw new PlatformNotSupportedException("WebKitGTK runtime libraries are not available.");
```
