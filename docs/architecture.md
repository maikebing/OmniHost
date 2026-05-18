---
title: Architecture
---

# Architecture

## Overview

OmniHost is split into shared host coordination, native OS runtimes, and native WebView adapters:

- application code configures the host
- `OmniHost` exposes `OmniApp.CreateBuilder`
- `OmniHost.Core` coordinates windows and adapters
- `OmniHost.Abstractions` defines shared contracts
- platform runtimes create native windows and run native event loops
- browser adapters embed the platform WebView into the host surface

The project intentionally avoids WinForms, WPF, CefSharp.WinForms, Electron, Tauri, MAUI, Avalonia, or other extra desktop shells as core runtime paths.

## Packages

| Package | Role |
|---------|------|
| `OmniHost.Abstractions` | Public interfaces and model types |
| `OmniHost.Core` | Builder pattern, app runner, and host-window coordination |
| `OmniHost` | Top-level package exposing `OmniApp.CreateBuilder` |
| `OmniHost.Hosting` | Integration with `Microsoft.Extensions.Hosting` |
| `OmniHost.Windows` | Raw Win32 runtime and host-window implementation |
| `OmniHost.NativeWebView2` | Native WebView2 adapter for Windows |
| `OmniHost.Gtk` | GTK runtime and host-window implementation for Linux |
| `OmniHost.WebKitGtk` | WebKitGTK adapter for Linux |

Planned macOS packages are an AppKit runtime and WKWebView adapter.

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

`IWindowAwareDesktopApp`, `OmniWindowContext`, and `IOmniWindowManager` provide per-window lifecycle and dynamic window operations.

## Entry Point Flow

```text
OmniApp.CreateBuilder(args)
  -> OmniHostBuilder
    .Configure(...)         -> set OmniHostOptions
    .UseAdapter(factory)    -> register adapter factory
    .UseRuntime(runtime)    -> select native OS runtime
    .Build()
  -> IOmniHostApp
    .RunAsync()
      -> HostWindowCoordinator.RunMainWindow(...)
      -> IWebViewAdapterFactory.Create()
      -> validate adapter and host-surface compatibility
      -> IHostWindowFactory.Create(...)
      -> IWebViewAdapter.InitializeAsync(surface, options)
      -> IWebViewAdapter.NavigateAsync(options.StartUrl)
      -> run native message loop
```
