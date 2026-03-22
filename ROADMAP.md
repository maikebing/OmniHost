# Roadmap

## 0.1.x — Preview (current)

> Goal: establish a clean, pluggable architecture; ship the Windows/WebView2 path.

- [x] Repository structure & docs baseline
- [x] `OmniWebHost.Abstractions` — interfaces & models
- [x] `OmniWebHost.Core` — builder & runner scaffolding
- [x] `OmniWebHost` — `OmniApp.CreateBuilder` entry point
- [x] `OmniWebHost.Hosting` — `IHostBuilder` extensions
- [x] `OmniWebHost.WebView2` — WebView2 adapter placeholder
- [x] Real WebView2 runtime initialisation (`CoreWebView2Environment`)
- [x] JS Bridge wired to `CoreWebView2.ExecuteScriptAsync` / `WebMessageReceived`
- [x] Custom URI scheme support (serve local assets without HTTP server)
- [x] Basic sample running end-to-end on Windows
- [x] **AOT-compatible Win32 window host** — raw Win32 P/Invoke, no WinForms/WPF
- [x] `OmniWindowStyle.Frameless` + window-control JS bridge (`minimize`, `maximize`, `close`, `startDrag`)
- [ ] Window lifecycle management (min/max/close events via OS hook)
- [ ] Frameless drag-region HTML attribute (`omni-drag`)
- [ ] Multi-window support
- [ ] Splash screen helper

## 0.3.x — macOS (WKWebView)

- [ ] `OmniWebHost.WKWebView` adapter
- [ ] macOS native window integration
- [ ] JS Bridge parity with WebView2 adapter

## 0.4.x — Linux (WebKitGTK)

- [ ] `OmniWebHost.WebKitGtk` adapter
- [ ] GTK window integration

## 0.5.x — CEF (cross-platform alternative)

- [ ] `OmniWebHost.Cef` adapter using CefSharp or Chromely
- [ ] Consistent JS Bridge across all adapters

## Future

- Enhanced DI integration
- Hot-reload support for web assets
- Packaged distribution helpers (MSIX, AppImage, DMG)
