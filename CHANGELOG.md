# Changelog

All notable changes to this project will be documented in this file.  
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [0.1.0-preview.3] — 2026-03-22

### Changed — breaking

- `OmniWebHost.WebView2` **no longer depends on Windows Forms**.  
  `<UseWindowsForms>` has been removed from the project.  
  This unblocks AOT (Ahead-of-Time) compilation.
- `WinFormsRuntime` and `OmniHostForm` removed; replaced by `Win32Runtime` and `OmniHostWindow`.

### Added

- `OmniWindowStyle` enum (`Normal`, `Frameless`) in `OmniWebHost.Abstractions`
- `OmniWebHostOptions.WindowStyle` — select Normal (OS chrome) or Frameless (custom HTML/CSS chrome)
- `Win32Runtime` — new AOT-compatible `IDesktopRuntime` backed by raw Win32 P/Invoke
- `OmniHostWindow` — raw Win32 HWND host:
  - `RegisterClassExW` / `CreateWindowExW` / Win32 message loop
  - `Win32SynchronizationContext` — posts async continuations to the Win32 message queue via `WM_APP + 1` so `await` works correctly on the UI thread
  - `WM_NCHITTEST` override for resizable frameless windows
- Window-control JS bridge handlers (registered automatically in `Frameless` mode):
  - `window.omni.invoke("window.minimize")`
  - `window.omni.invoke("window.maximize")` — toggles maximize/restore
  - `window.omni.invoke("window.close")`
  - `window.omni.invoke("window.startDrag")` — initiates window drag from custom title bar
- `NativeMethods` (internal) — all P/Invoke declarations with `ExactSpelling = true` + `CharSet.Unicode`
- Sample updated to use `Win32Runtime`; `wwwroot/index.html` adds window-control demo buttons

---

## [0.1.0-preview.2] — 2026-03-22

### Added

- `IDesktopRuntime` interface in `OmniWebHost.Abstractions` — decouples the window/message-loop from the core builder
- `OmniWebHostOptions`: new `CustomScheme`, `ContentRootPath`, `UserDataFolder` properties
- `OmniWebHostBuilder.UseRuntime(IDesktopRuntime)` and `UseDesktopApp(IDesktopApp)` methods
- `WinFormsRuntime` — production `IDesktopRuntime` backed by Windows Forms; auto-creates STA thread
- `OmniHostForm` — internal WinForms `Form` that hosts the WebView2 control, handles resize and graceful shutdown
- `WebView2AdapterFactory.IsAvailable` — real check via `CoreWebView2Environment.GetAvailableBrowserVersionString()`
- `WebView2Adapter` — real `CoreWebView2Environment` + `CoreWebView2Controller` initialisation
- `WebView2Adapter` custom scheme handler: serves local files from `ContentRootPath` via `app://localhost/…` (path-traversal protected)
- `WebView2JsBridge` — fully wired JS bridge:
  - `ExecuteScriptAsync` → `CoreWebView2.ExecuteScriptAsync`
  - `RegisterHandler` + `WebMessageReceived` dispatcher (JSON envelope protocol)
  - `PostMessageAsync` → typed event envelope dispatched via `PostWebMessageAsJson`
  - Injected `omni.js` bridge script: `window.omni.invoke(handler, data)` / `window.omni.on(event, cb)`
- `samples/OmniWebHost.Sample.Basic`: end-to-end sample with `greet` and `sysinfo` bridge handlers, `tick` server-push events
- `samples/OmniWebHost.Sample.Basic/wwwroot/index.html`: interactive demo page

### Changed

- `OmniWebHostApp.RunAsync` now delegates to `IDesktopRuntime.Run` (no longer contains placeholder code)
- `WebView2Adapter.Capabilities.EngineVersion` is now populated from the real WebView2 runtime version string

---

## [0.1.0-preview.1] — 2026-03-22

### Added

- Repository structure: `src/`, `samples/`, `docs/`
- `OmniWebHost.Abstractions` — public contracts:
  - `IWebViewAdapter` / `IWebViewAdapterFactory`
  - `IJsBridge`
  - `IDesktopApp`
  - `BrowserCapabilities`
  - `OmniWebHostOptions`
- `OmniWebHost.Core` — builder & application runner scaffolding:
  - `OmniApp.CreateBuilder(args)` entry point
  - `OmniWebHostBuilder` / `IOmniWebHostApp`
- `OmniWebHost` — top-level package re-exporting the entry point
- `OmniWebHost.Hosting` — `IHostBuilder.UseOmniWebHost()` extension
- `OmniWebHost.WebView2` — Windows/WebView2 adapter placeholder:
  - `WebView2Adapter`
  - `WebView2AdapterFactory`
  - `WebView2JsBridge`
- `samples/OmniWebHost.Sample.Basic` — minimal compilable sample
- Documentation baseline: `README.md`, `ROADMAP.md`, `CHANGELOG.md`, `docs/`

