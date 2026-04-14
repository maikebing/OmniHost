---
title: Roadmap
---

# Roadmap

## 0.1.x - Preview (current)

> Goal: architecture baseline + Windows / WebView2 path.

- [x] Repository structure and docs baseline
- [x] `OmniHost.Abstractions` - interfaces and models
- [x] `OmniHost.Core` - builder and runner scaffolding
- [x] `OmniHost.WebView2` - real WebView2 runtime initialization
- [x] JS bridge wired to WebView2 events (`omni` helper injected)
- [x] Custom scheme support (local asset serving via `app://localhost/`)
- [x] `Win32Runtime` + end-to-end runnable sample on Windows
- [x] Window lifecycle bridge (`omni.window.*` controls + `omni.on("window.*")` events)
- [x] Frameless window mode with `omni-drag`

## 0.2.x - Windows Stable

- Multi-window support via `AddWindow(...)` and `IOmniWindowManager`
- Splash screen helper via `UseSplashScreen(...)` and `omni.invoke("splash.close")`
- Runtime / HostWindow refactor:
  - introduced `IHostWindow`, `IHostWindowFactory`, and `HostSurfaceDescriptor`
  - renamed the raw host-window implementation to `Win32HostWindow`
  - moved Win32 hosting out of `OmniHost.WebView2` into `OmniHost.Windows`
  - kept `Win32Runtime` as the simple public entry point during the transition
  - introduced `IWindowFrameStrategy` and split `Normal` / `Frameless` into concrete frame strategies
  - added a dedicated Windows frame-strategy factory so more frame variants can be introduced without changing `Win32HostWindow`
  - introduced a first `HostWindowCoordinator` in `OmniHost.Core` while keeping single-window startup as the default path
  - added internal window definitions and tracked-window snapshots so future auxiliary windows can reuse the same coordinator pipeline
  - cloned `OmniHostOptions` per tracked window so future multi-window instances do not share one mutable options object
  - validates adapter and host-surface compatibility before window startup
- Naming strategy:
  - product short name: `Omni`
  - technical family: `OmniHost.*`
  - the rename has been completed across projects, namespaces, samples, and docs
  - user-facing upgrade notes live in [MIGRATION.md](../MIGRATION.md)

## 0.3.x - macOS

- `OmniHost.WKWebView` adapter

## 0.4.x - Linux

- `OmniHost.Gtk` first-pass runtime/window host package
- `OmniHost.WebKitGtk` experimental adapter

## 0.5.x - CEF (cross-platform)

- `OmniHost.Cef` adapter (CefSharp / Chromely)

See [ROADMAP.md](../ROADMAP.md) in the repository root for the full staged plan, including the refactor sketch and proposed package layout.
