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
| `OmniHost.Gtk` | First-pass Linux GTK runtime and host-window implementation. |
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
These callbacks run once per created host window.

### `IWindowAwareDesktopApp`
Optional context-aware desktop-app contract that receives `OmniWindowContext`
for each window and can coordinate dynamic multi-window behaviour.

### `IMultiWindowDesktopRuntime`
Optional runtime extension used when the builder declares additional startup windows.

### `OmniWindowDefinition`
Public startup-window descriptor used by `OmniHostBuilder.AddWindow(...)`.

### `OmniWindowContext`
Per-window context containing the window id, startup options snapshot, browser adapter,
and current window manager.

### `IOmniWindowManager`
Runtime window-management API for enumerating open windows, opening additional windows,
activating or closing a specific window, retrieving live window contexts by id,
and posting or broadcasting host events.

### `BrowserCapabilities`
Describes what a given adapter can do (DevTools, custom schemes, JS bridge, host-surface support, …).

### `HostWindowCoordinator`
Coordinator in `OmniHost.Core` that creates adapters, creates host windows,
tracks the current open-window set, and runs each window through the selected runtime.
It owns internal window definitions and window snapshots so both main and auxiliary
windows can reuse the same coordination path. Each tracked window keeps its own
cloned `OmniHostOptions` instance.

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
