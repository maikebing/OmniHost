---
title: Adapters
---

# Adapters

NativeWebHost uses a pluggable adapter model. Each adapter wraps the native WebView engine for one platform.

## Available Adapters

| Package | Engine | Platform | Status |
|---------|--------|----------|--------|
| `NativeWebHost.Windows` | WebView2 via WebView2Aot COM bindings | Windows 10/11 | Primary |
| `NativeWebHost.Linux` | WebKitGTK | Linux | Experimental |
| `NativeWebHost.Mac` | WKWebView | macOS | Experimental |
| `NativeWebHost.Android` | Android system WebView | Android tablets/phones | Experimental |

Removed adapters: `NativeWebHost.WebView2` and `NativeWebHost.Cef`. The framework no longer keeps a CefSharp/WinForms path.

## Native Windows Adapter

```csharp
.UseAdapter(new NativeWebView2AdapterFactory())
.UseRuntime(new Win32Runtime())
```

`NativeWebHost.Windows` supports:

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

## macOS Adapter

```csharp
.UseAdapter(new WKWebViewAdapterFactory())
.UseRuntime(new MacRuntime())
```

The macOS adapter supports AppKit `NSView` host surfaces, WKWebView embedding, JavaScript bridge wiring, native `app://localhost/...` asset loading, status item tray menus, and standard AppKit windows.

Current macOS limitations:

- custom frameless dragging is limited by AppKit event payload support
- DevTools are reported as unavailable for the WKWebView path
- `MacRuntime` must be started from the process main thread, as required by AppKit
- notarization/signing is an application packaging responsibility

## Android Adapter

```csharp
public sealed class MainActivity : NativeWebHostAndroidActivity
{
    protected override void ConfigureNativeWebHostOptions(AndroidNativeWebHostOptions options)
    {
        options.Title = "My App";
        options.StartUrl = AndroidWebViewAssetHost.RootUrl;
        options.AssetRoot = AndroidWebViewAssetHost.DefaultAssetRoot;
    }
}
```

The Android adapter supports system WebView hosting in an Activity, JavaScript bridge wiring, APK/AAB asset loading from `wwwroot`, and same-origin `/api/...` fetch interception for app-provided handlers.

Current Android limitations:

- it does not use the ASP.NET Core `NativeWebApplication` host because `Microsoft.AspNetCore.App` is not available for Android runtime identifiers
- multi-window desktop APIs are not mapped to Android Activity/task behavior yet
- app signing, store metadata, and Play distribution are application packaging responsibilities

## Implementing an Adapter

1. Reference `NativeWebHost`.
2. Implement `IWebViewAdapterFactory`.
3. Implement `IWebViewAdapter`.
4. Report supported `HostSurfaceKind` values through `BrowserCapabilities.SupportedHostSurfaces`.

Adapters should own browser-engine initialization, navigation, JavaScript bridge wiring, and browser-specific capabilities. Native window or Activity creation belongs in a runtime package such as `NativeWebHost.Windows`, `NativeWebHost.Linux`, or `NativeWebHost.Android`.
