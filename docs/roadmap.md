---
title: Roadmap
---

# Roadmap

## 0.1.x — Preview (current)

> Goal: architecture baseline + Windows / WebView2 path.

- [x] Repository structure and docs baseline
- [x] `OmniWebHost.Abstractions` — interfaces and models
- [x] `OmniWebHost.Core` — builder and runner scaffolding
- [x] `OmniWebHost.WebView2` — real WebView2 runtime initialisation
- [x] JS Bridge wired to WebView2 events (`omni` helper injected)
- [x] Custom scheme support (local asset serving via `app://localhost/`)
- [x] `Win32Runtime` + end-to-end runnable sample on Windows
- [x] Window lifecycle bridge (`omni.window.*`)
- [x] Frameless window mode

## 0.2.x — Windows Stable

- Multi-window support
- Runtime / HostWindow refactor:
  - introduced `IHostWindow`, `IHostWindowFactory`, and `HostSurfaceDescriptor`
  - renamed `OmniHostWindow` to `Win32HostWindow`
  - moved Win32 hosting out of `OmniWebHost.WebView2` into `OmniWebHost.Windows`
  - kept `Win32Runtime` as the simple public entry point during the transition
  - introduced `IWindowFrameStrategy` and split `Normal` / `Frameless` into concrete frame strategies
  - added a dedicated Windows frame-strategy factory so more frame variants can be introduced without changing `Win32HostWindow`
  - introduced a first `HostWindowCoordinator` in `OmniWebHost.Core` while keeping single-window startup as the default path
- Naming strategy:
  - product short name: `Omni`
  - planned technical family after refactor: `OmniHost.*`
  - defer the full rename until the new hosting seams are stable

## 0.3.x — macOS

- `OmniWebHost.WKWebView` adapter

## 0.4.x — Linux

- `OmniWebHost.WebKitGtk` adapter

## 0.5.x — CEF (cross-platform)

- `OmniWebHost.Cef` adapter (CefSharp / Chromely)

See [ROADMAP.md](../ROADMAP.md) in the repository root for the full staged plan,
including the refactor sketch and proposed package layout.
