# Changelog

All notable changes to this project will be documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

### Added

- Native window lifecycle events are now pushed back through the JS bridge:
  - `window.stateChanged`
  - `window.closing`
  - `window.closed`
- Public startup multi-window support via:
  - `OmniHostBuilder.AddWindow(...)`
  - `IMultiWindowDesktopRuntime`
  - `OmniWindowDefinition`
- Context-aware window management via:
  - `IWindowAwareDesktopApp`
  - `OmniWindowContext`
  - `IOmniWindowManager`
- The basic sample now shows a live window lifecycle event stream.
- The basic sample now launches a secondary startup window and can open/close an inspector window at runtime.

### Changed - breaking

- Renamed the technical package / namespace family from `OmniWebHost*` to `OmniHost*`.
- Solution, project files, assembly names, namespaces, and samples now use:
  - `OmniHost`
  - `OmniHost.Abstractions`
  - `OmniHost.Core`
  - `OmniHost.Hosting`
  - `OmniHost.Windows`
  - `OmniHost.WebView2`
- `UseOmniWebHost()` was renamed to `UseOmniHost()`.
- `OmniWebHostOptions`, `OmniWebHostBuilder`, and related `OmniWebHost*` type names
  were renamed to their `OmniHost*` equivalents.

---

## [0.1.0-preview.3] - 2026-03-22

### Changed - breaking

- `OmniHost.WebView2` no longer depends on Windows Forms.
  `<UseWindowsForms>` has been removed from the project.
  This unblocks AOT (Ahead-of-Time) compilation.
- `WinFormsRuntime` and `OmniHostForm` were removed and replaced by `Win32Runtime` and `Win32HostWindow`.
- `Frameless` mode now uses a DWM custom-frame path instead of relying only on stripped non-client rendering.

### Added

- `OmniWindowStyle` enum (`Normal`, `Frameless`) in `OmniHost.Abstractions`.
- `OmniScrollBarMode` enum (`Auto`, `Hidden`, `VerticalOnly`, `Custom`) in `OmniHost.Abstractions`.
- `OmniHostOptions.WindowStyle` to select `Normal` (OS chrome) or `Frameless` (custom HTML/CSS chrome).
- `OmniHostOptions.ScrollBarMode` and `ScrollBarCustomCss` for host-level scrollbar control.
- `Win32Runtime`, a new AOT-compatible `IDesktopRuntime` backed by raw Win32 P/Invoke.
- `Win32HostWindow`, a raw Win32 HWND host with:
  - `RegisterClassExW` / `CreateWindowExW` / Win32 message loop
  - `Win32SynchronizationContext` posting async continuations through `WM_APP + 1`
  - DWM custom-frame handling for frameless windows
  - `WM_NCHITTEST` override for resizable frameless windows
- Window-control JS bridge handlers:
  - `omni.invoke("window.minimize")`
  - `omni.invoke("window.maximize")`
  - `omni.invoke("window.close")`
  - `omni.invoke("window.startDrag")`
  - `omni.invoke("window.showSystemMenu")`
- `omni.window.*` convenience helpers injected into every page.
- Automatic `omni-drag` support for frameless pages:
  - drag to move the window
  - double-click to maximize / restore
  - right-click to open the native system menu
- `NativeMethods` (internal) with Win32 and DWM P/Invoke declarations using `ExactSpelling = true` and `CharSet.Unicode`.
- Sample updated to use `Win32Runtime`; `wwwroot/index.html` now demonstrates a frameless title bar, window controls, and host-level scrollbar configuration.

---

## [0.1.0-preview.2] - 2026-03-22

### Added

- `IDesktopRuntime` interface in `OmniHost.Abstractions` to decouple the window/message-loop from the core builder.
- `OmniHostOptions` gained `CustomScheme`, `ContentRootPath`, and `UserDataFolder` properties.
- `OmniHostBuilder.UseRuntime(IDesktopRuntime)` and `UseDesktopApp(IDesktopApp)` methods.
- `WinFormsRuntime`, a production `IDesktopRuntime` backed by Windows Forms with automatic STA-thread creation.
- `OmniHostForm`, an internal WinForms `Form` that hosts WebView2, handles resize, and performs graceful shutdown.
- `WebView2AdapterFactory.IsAvailable` with a real runtime check via `CoreWebView2Environment.GetAvailableBrowserVersionString()`.
- `WebView2Adapter` with real `CoreWebView2Environment` and `CoreWebView2Controller` initialization.
- `WebView2Adapter` custom-scheme handler serving local files from `ContentRootPath` via `app://localhost/...` with path-traversal protection.
- `WebView2JsBridge`, fully wired:
  - `ExecuteScriptAsync` -> `CoreWebView2.ExecuteScriptAsync`
  - `RegisterHandler` + `WebMessageReceived` dispatcher
  - `PostMessageAsync` -> typed event envelope dispatched through WebView2 messaging
  - injected `omni.js` bridge script providing `omni.invoke(handler, data)` and `omni.on(event, cb)`
- `samples/OmniHost.Sample.Basic`, an end-to-end sample with `greet`, `sysinfo`, and `tick` handlers.
- `samples/OmniHost.Sample.Basic/wwwroot/index.html`, an interactive demo page.

### Changed

- `OmniHostApp.RunAsync` now delegates to `IDesktopRuntime.Run`.
- `WebView2Adapter.Capabilities.EngineVersion` is populated from the real WebView2 runtime version string.

---

## [0.1.0-preview.1] - 2026-03-22

### Added

- Repository structure: `src/`, `samples/`, `docs/`
- `OmniHost.Abstractions` public contracts:
  - `IWebViewAdapter` / `IWebViewAdapterFactory`
  - `IJsBridge`
  - `IDesktopApp`
  - `BrowserCapabilities`
  - `OmniHostOptions`
- `OmniHost.Core` builder and application runner scaffolding:
  - `OmniApp.CreateBuilder(args)` entry point
  - `OmniHostBuilder` / `IOmniHostApp`
- `OmniHost`, the top-level package re-exporting the entry point.
- `OmniHost.Hosting` with `IHostBuilder.UseOmniHost()` extension.
- `OmniHost.WebView2`, the Windows/WebView2 adapter placeholder:
  - `WebView2Adapter`
  - `WebView2AdapterFactory`
  - `WebView2JsBridge`
- `samples/OmniHost.Sample.Basic`, a minimal compilable sample.
- Documentation baseline: `README.md`, `ROADMAP.md`, `CHANGELOG.md`, `docs/`.
