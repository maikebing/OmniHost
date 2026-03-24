---
title: Architecture
---

# Architecture

## Overview

OmniHost is structured as a set of layered packages with clear boundaries:

```
┌──────────────────────────────────────────────┐
│           Application Code                   │
├──────────────────────────────────────────────┤
│  OmniHost  (OmniApp entry point)          │
├───────────────┬──────────────────────────────┤
│ OmniHost   │  OmniHost.Hosting         │
│ .Core         │  (IHostBuilder extensions)   │
├───────────────┴──────────────────────────────┤
│          OmniHost.Abstractions            │
│  IWebViewAdapter  IWebViewAdapterFactory     │
│  IHostWindow      IHostWindowFactory         │
│  IWindowFrameStrategy                        │
│  IJsBridge        IDesktopApp                │
│  BrowserCapabilities  OmniHostOptions     │
├──────────────┬───────────────┬──────────────┤
│ WebView2     │  CEF (future) │  WKWebView   │
│ Adapter      │               │  (future)    │
└──────────────┴───────────────┴──────────────┘
```

## Packages

| Package | Role |
|---------|------|
| `OmniHost.Abstractions` | Public interfaces and model types. No implementation code. |
| `OmniHost.Core` | Builder pattern, `OmniHostApp` runner, and host-window coordination. |
| `OmniHost` | Top-level package exposing `OmniApp.CreateBuilder`. |
| `OmniHost.Hosting` | Integration with `Microsoft.Extensions.Hosting`. |
| `OmniHost.Windows` | Windows runtime and raw Win32 host-window implementation. |
| `OmniHost.WebView2` | Microsoft WebView2 adapter (Windows). |

## Key Interfaces

### `IWebViewAdapter`
Represents a browser engine instance.  
Responsibilities: initialise the WebView control against a typed host surface,
navigate, expose the JS bridge.

### `IWebViewAdapterFactory`
Creates `IWebViewAdapter` instances and reports availability.  
Register one per supported engine via DI or pass to the builder directly.

### `IHostWindow`
Represents a concrete native host window and the browser attachment surface it exposes.

### `IHostWindowFactory`
Creates platform-specific host windows so a runtime can keep its thread/event-loop
responsibility separate from the actual native window implementation.

### `IWindowFrameStrategy`
Represents how a native host window presents and manages its frame, such as a
standard system frame or a custom DWM-backed frameless implementation.

### `IJsBridge`
Bidirectional channel between .NET and JavaScript running in the WebView.

### `IDesktopApp`
Optional lifecycle callbacks (`OnStartAsync`, `OnClosingAsync`) for the host application.

### `BrowserCapabilities`
Describes what a given adapter can do (DevTools, custom schemes, JS bridge, host-surface support, …).

### `HostWindowCoordinator`
Current single-window coordinator in `OmniHost.Core` that creates the adapter,
creates the host window, tracks the current open-window set, and runs that window
through the selected runtime. It now also owns internal window definitions and
window snapshots so future auxiliary windows can reuse the same coordination path.
Each tracked window now keeps its own cloned `OmniHostOptions` instance.

## Entry Point Flow

```
OmniApp.CreateBuilder(args)
  → OmniHostBuilder
    .Configure(...)         ← set OmniHostOptions
    .UseAdapter(factory)    ← register adapter factory
    .Build()
  → IOmniHostApp
    .RunAsync()
      → HostWindowCoordinator.RunMainWindow(...)
      → IWebViewAdapterFactory.Create()
      → validate adapter ↔ host-surface compatibility
      → IHostWindowFactory.Create(...)
      → IWebViewAdapter.InitializeAsync(surface, options)
      → IWebViewAdapter.NavigateAsync(options.StartUrl)
      → run message loop
```
