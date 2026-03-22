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
- [x] JS Bridge wired to WebView2 events (`window.omni` helper injected)
- [x] Custom scheme support (local asset serving via `app://localhost/`)
- [x] `WinFormsRuntime` + end-to-end runnable sample on Windows
- [ ] Window lifecycle (min/max/close events)
- [ ] Frameless window mode

## 0.2.x — Windows Stable

- Window lifecycle (min/max/close events)
- Frameless window mode
- Multi-window support

## 0.3.x — macOS

- `OmniWebHost.WKWebView` adapter

## 0.4.x — Linux

- `OmniWebHost.WebKitGtk` adapter

## 0.5.x — CEF (cross-platform)

- `OmniWebHost.Cef` adapter (CefSharp / Chromely)

See [ROADMAP.md](../ROADMAP.md) in the repository root for the full plan.
