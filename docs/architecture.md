---
title: Architecture
---

# Architecture

## Overview

NativeWebHost is split into one shared framework package and one native adapter package per operating system:

- application code configures the host
- `NativeWebHost` exposes `NativeWebApp.CreateBuilder`
- `NativeWebHost` also contains the shared contracts, host coordination, and hosting helpers
- platform runtimes create native windows and run native event loops
- browser adapters embed the platform WebView into the host surface

The project intentionally avoids WinForms, WPF, CefSharp.WinForms, Electron, Tauri, MAUI, Avalonia, or other extra desktop shells as core runtime paths.

## Packages

| Package | Role |
|---------|------|
| `NativeWebHost` | Public contracts, builder, host coordination, ASP.NET Core integration |
| `NativeWebHost.Windows` | Raw Win32 runtime and native WebView2 adapter |
| `NativeWebHost.Linux` | GTK runtime and WebKitGTK adapter |
| `NativeWebHost.Mac` | AppKit runtime and WKWebView adapter placeholder |

The public NuGet surface is intentionally limited to these four packages.

## Platform Choice

| OS | Runtime | Adapter | Notes |
|----|---------|---------|-------|
| Windows | raw Win32 | Native WebView2 | primary, AOT-friendly |
| Linux | GTK 3 | WebKitGTK | package GTK/WebKitGTK with the app when needed |
| macOS | AppKit | WKWebView | planned native path |

## Key Interfaces

`IWebViewAdapter` represents a browser engine instance. It initializes against a typed host surface, navigates, resizes, and exposes the JS bridge.

`IWebViewAdapterFactory` creates adapters and reports whether the native engine is available.

`IHostWindow` represents a concrete native host window and the browser attachment surface it exposes.

`IHostWindowFactory` creates platform-specific host windows while the runtime owns the event loop.

`IDesktopRuntime` owns platform threading and message-loop orchestration.

`IMultiWindowDesktopRuntime` extends a runtime with startup and dynamic multi-window support.

`IWindowAwareDesktopApp`, `NativeWebWindowContext`, and `INativeWebWindowManager` provide per-window lifecycle and dynamic window operations.

## Entry Point Flow

```text
NativeWebApp.CreateBuilder(args)
  -> NativeWebHostBuilder
    .Configure(...)         -> set NativeWebHostOptions
    .UseAdapter(factory)    -> register adapter factory
    .UseRuntime(runtime)    -> select native OS runtime
    .Build()
  -> INativeWebHostApp
    .RunAsync()
      -> HostWindowCoordinator.RunMainWindow(...)
      -> IWebViewAdapterFactory.Create()
      -> validate adapter and host-surface compatibility
      -> IHostWindowFactory.Create(...)
      -> IWebViewAdapter.InitializeAsync(surface, options)
      -> IWebViewAdapter.NavigateAsync(options.StartUrl)
      -> run native message loop
```
