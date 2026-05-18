---
title: Adapters
---

# Adapters

OmniHost uses a pluggable adapter model. Each adapter wraps the native WebView engine for one platform.

## Available Adapters

| Package | Engine | Platform | Status |
|---------|--------|----------|--------|
| `OmniHost.NativeWebView2` | WebView2 via WebView2Aot COM bindings | Windows 10/11 | Primary |
| `OmniHost.WebKitGtk` | WebKitGTK | Linux | Experimental |
| `OmniHost.WKWebView` | WKWebView | macOS | Planned |

Removed adapters: `OmniHost.WebView2` and `OmniHost.Cef`. The framework no longer keeps a CefSharp/WinForms path.

## Native Windows Adapter

```csharp
.UseAdapter(new NativeWebView2AdapterFactory())
.UseRuntime(new Win32Runtime())
```

`OmniHost.NativeWebView2` supports:

- HWND host surfaces
- JavaScript bridge
- `app://localhost/...` local asset loading
- DevTools configuration
- window chrome helper injection
- built-in title-bar presets

## Linux Adapter

```csharp
.UseAdapter(new WebKitGtkAdapterFactory())
.UseRuntime(new GtkRuntime())
```

The Linux adapter supports GTK widget host surfaces, WebKitGTK embedding, JavaScript bridge wiring, and native `app://localhost/...` asset loading.

Current Linux limitations:

- window chrome behavior needs validation across more distros and window managers
- `showSystemMenu` is best-effort and depends on the current GTK/GDK event context
- shipping without user-installed packages requires app packaging that includes the needed GTK/WebKitGTK native libraries

## Implementing an Adapter

1. Reference `OmniHost.Abstractions`.
2. Implement `IWebViewAdapterFactory`.
3. Implement `IWebViewAdapter`.
4. Report supported `HostSurfaceKind` values through `BrowserCapabilities.SupportedHostSurfaces`.

Adapters should own browser-engine initialization, navigation, JavaScript bridge wiring, and browser-specific capabilities. Native window creation belongs in a runtime package such as `OmniHost.Windows` or `OmniHost.Gtk`.
