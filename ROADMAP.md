# Roadmap

## 0.1.x — Preview (current)

> Goal: establish a clean, pluggable architecture; ship the Windows/WebView2 path.

- [x] Repository structure & docs baseline
- [x] `OmniWebHost.Abstractions` — interfaces & models
- [x] `OmniWebHost.Core` — builder & runner scaffolding
- [x] `OmniWebHost` — `OmniApp.CreateBuilder` entry point
- [x] `OmniWebHost.Hosting` — `IHostBuilder` extensions
- [x] `OmniWebHost.WebView2` — WebView2 adapter placeholder
- [ ] Real WebView2 runtime initialisation (`CoreWebView2Environment`)
- [ ] JS Bridge wired to `CoreWebView2.ExecuteScriptAsync` / `WebMessageReceived`
- [ ] Custom URI scheme support (serve local assets without HTTP server)
- [ ] Basic sample running end-to-end on Windows

## 0.2.x — Windows Stable

- [ ] Window lifecycle management (min/max/close events)
- [ ] Frameless / transparent window mode
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
