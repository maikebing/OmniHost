---
title: Adapters
---

# Adapters

OmniHost uses a pluggable adapter model.  
Each adapter wraps a concrete browser engine and exposes a uniform `IWebViewAdapter` API.

## Available Adapters

| Package | Engine | Platform | Status |
|---------|--------|----------|--------|
| `OmniHost.WebView2` | Microsoft WebView2 | Windows 10/11 | ✅ Preview (functional) |
| `OmniHost.Cef` | Chromium Embedded Framework | Windows / macOS / Linux | 📋 Planned |
| `OmniHost.WKWebView` | WKWebView | macOS | 📋 Planned |
| `OmniHost.WebKitGtk` | WebKitGTK | Linux | 📋 Planned |

## Using an Adapter

Pass the adapter factory to the builder:

```csharp
.UseAdapter(new WebView2AdapterFactory())
```

Or register via DI and let the host resolve it automatically (Hosting integration):

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

`HostSurfaceDescriptor` is the forward-looking API because not every engine will
always attach to a plain `HWND`. A legacy raw-handle overload still exists for
simple adapters during the transition.

Adapters should also report their supported `HostSurfaceKind` values through
`BrowserCapabilities.SupportedHostSurfaces` so the host can reject incompatible
runtime/window combinations early.

## Capability Detection

Check `IWebViewAdapterFactory.IsAvailable` before registering:

```csharp
var factory = new WebView2AdapterFactory();
if (!factory.IsAvailable)
    throw new PlatformNotSupportedException("WebView2 runtime is not installed.");
```
