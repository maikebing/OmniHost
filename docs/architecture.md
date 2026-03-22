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
│  IJsBridge        IDesktopApp               │
│  BrowserCapabilities  OmniWebHostOptions    │
├──────────────┬───────────────┬──────────────┤
│ WebView2     │  CEF (future) │  WKWebView   │
│ Adapter      │               │  (future)    │
└──────────────┴───────────────┴──────────────┘
```

## Packages

| Package | Role |
|---------|------|
| `OmniWebHost.Abstractions` | Public interfaces and model types. No implementation code. |
| `OmniWebHost.Core` | Builder pattern, `OmniWebHostApp` runner, null implementations. |
| `OmniWebHost` | Top-level package exposing `OmniApp.CreateBuilder`. |
| `OmniWebHost.Hosting` | Integration with `Microsoft.Extensions.Hosting`. |
| `OmniWebHost.WebView2` | Microsoft WebView2 adapter (Windows). |

## Key Interfaces

### `IWebViewAdapter`
Represents a browser engine instance.  
Responsibilities: initialise the WebView control, navigate, expose the JS bridge.

### `IWebViewAdapterFactory`
Creates `IWebViewAdapter` instances and reports availability.  
Register one per supported engine via DI or pass to the builder directly.

### `IJsBridge`
Bidirectional channel between .NET and JavaScript running in the WebView.

### `IDesktopApp`
Optional lifecycle callbacks (`OnStartAsync`, `OnClosingAsync`) for the host application.

### `BrowserCapabilities`
Describes what a given adapter can do (DevTools, custom schemes, JS bridge, …).

## Entry Point Flow

```
OmniApp.CreateBuilder(args)
  → OmniWebHostBuilder
    .Configure(...)         ← set OmniWebHostOptions
    .UseAdapter(factory)    ← register adapter factory
    .Build()
  → IOmniWebHostApp
    .RunAsync()
      → create host window
      → IWebViewAdapterFactory.Create()
      → IWebViewAdapter.InitializeAsync(hwnd, options)
      → IWebViewAdapter.NavigateAsync(options.StartUrl)
      → run message loop
```
