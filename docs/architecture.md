---
title: Architecture
---

# Architecture

## Overview

OmniWebHost is structured as a set of layered packages with clear boundaries:

```
┌──────────────────────────────────────────────┐
│           Application Code                   │
├──────────────────────────────────────────────┤
│  OmniWebHost  (OmniApp entry point)          │
├───────────────┬──────────────────────────────┤
│ OmniWebHost   │  OmniWebHost.Hosting         │
│ .Core         │  (IHostBuilder extensions)   │
├───────────────┴──────────────────────────────┤
│          OmniWebHost.Abstractions            │
│  IWebViewAdapter  IWebViewAdapterFactory     │
│  IHostWindow      IHostWindowFactory         │
│  IWindowFrameStrategy                        │
│  IJsBridge        IDesktopApp                │
│  BrowserCapabilities  OmniWebHostOptions     │
├──────────────┬───────────────┬──────────────┤
│ WebView2     │  CEF (future) │  WKWebView   │
│ Adapter      │               │  (future)    │
└──────────────┴───────────────┴──────────────┘
```

## Packages

| Package | Role |
|---------|------|
| `OmniWebHost.Abstractions` | Public interfaces and model types. No implementation code. |
| `OmniWebHost.Core` | Builder pattern, `OmniWebHostApp` runner, and host-window coordination. |
| `OmniWebHost` | Top-level package exposing `OmniApp.CreateBuilder`. |
| `OmniWebHost.Hosting` | Integration with `Microsoft.Extensions.Hosting`. |
| `OmniWebHost.Windows` | Windows runtime and raw Win32 host-window implementation. |
| `OmniWebHost.WebView2` | Microsoft WebView2 adapter (Windows). |

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
Describes what a given adapter can do (DevTools, custom schemes, JS bridge, …).

### `HostWindowCoordinator`
Current single-window coordinator in `OmniWebHost.Core` that creates the adapter,
creates the host window, tracks the current open-window set, and runs that window
through the selected runtime.

## Entry Point Flow

```
OmniApp.CreateBuilder(args)
  → OmniWebHostBuilder
    .Configure(...)         ← set OmniWebHostOptions
    .UseAdapter(factory)    ← register adapter factory
    .Build()
  → IOmniWebHostApp
    .RunAsync()
      → HostWindowCoordinator.RunMainWindow(...)
      → IWebViewAdapterFactory.Create()
      → IHostWindowFactory.Create(...)
      → IWebViewAdapter.InitializeAsync(surface, options)
      → IWebViewAdapter.NavigateAsync(options.StartUrl)
      → run message loop
```
