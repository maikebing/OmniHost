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

## Runtime / HostWindow Refactor Track

> Goal: split "UI runtime", "native host window", and "browser adapter" into
> separate layers so one OS can support multiple host-window implementations and
> one browser adapter can target different host surfaces over time.

### Design goals

- Keep the first working Windows path intact while we refactor around it.
- Move platform window hosting out of `OmniWebHost.WebView2`; WebView2 should own
  browser integration, not Win32 window creation.
- Support multiple host-window implementations per platform/toolkit.
- Prepare for future adapters that need more than a raw handle.
- Prefer "have it first, optimize later": introduce seams first, then move code,
  then generalize.

### Target conceptual layers

- `Runtime`
  Owns the UI thread model, dispatcher, and main event loop.
- `HostWindow`
  Owns native window creation, sizing, state changes, close flow, and the host
  surface exposed to a browser adapter.
- `BrowserAdapter`
  Owns browser-engine initialization, navigation, JS bridge, and browser-specific
  capabilities.
- `FrameStrategy`
  Owns how window chrome is implemented for a given host window, such as system
  frame, DWM custom frame, or fully custom borderless handling.

### Target abstractions (draft)

- `IDesktopRuntime`
  Keep the public concept for now; narrow it to thread model and event loop
  orchestration.
- `IHostWindow`
  Represents a concrete native window instance.
- `IHostWindowFactory`
  Creates a platform/toolkit-specific `IHostWindow` for a given runtime.
- `HostSurfaceDescriptor`
  Replaces the current "just pass an `nint`" model with a typed host-surface
  description such as `Hwnd`, `NSView`, `GtkWidget`, or `Offscreen`.
- `IWindowFrameStrategy`
  Optional strategy abstraction for `System`, `DwmCustomFrame`, `Borderless`,
  and future variants.

### Proposed package / directory shape

```text
src/
  OmniWebHost/
    OmniApp.cs

  OmniWebHost.Abstractions/
    Hosting/
      IDesktopRuntime.cs
      IHostWindow.cs
      IHostWindowFactory.cs
      HostSurfaceDescriptor.cs
      HostSurfaceKind.cs
      IWindowFrameStrategy.cs
    Browser/
      IWebViewAdapter.cs
      IWebViewAdapterFactory.cs
      BrowserCapabilities.cs
      IJsBridge.cs
    App/
      IDesktopApp.cs
    Options/
      OmniWebHostOptions.cs
      OmniWindowStyle.cs
      OmniScrollBarMode.cs

  OmniWebHost.Core/
    Hosting/
      OmniWebHostApp.cs
      OmniWebHostBuilder.cs
      HostWindowCoordinator.cs
    Browser/
      NullJsBridge.cs

  OmniWebHost.Windows/
    Runtime/
      Win32Runtime.cs
    Hosting/
      Win32HostWindow.cs
      Win32HostWindowFactory.cs
      Frames/
        SystemFrameStrategy.cs
        DwmCustomFrameStrategy.cs
    Win32/
      NativeMethods.cs
      Win32SynchronizationContext.cs

  OmniWebHost.WebView2/
    WebView2Adapter.cs
    WebView2AdapterFactory.cs
    WebView2JsBridge.cs

  OmniWebHost.AppKit/           (future)
  OmniWebHost.WKWebView/        (future)
  OmniWebHost.Gtk/              (future)
  OmniWebHost.WebKitGtk/        (future)
  OmniWebHost.Cef/              (future)
```

### Implementation phases

#### Phase 1 — Stabilize the seams without changing behaviour

- [x] Introduce `IHostWindow`, `IHostWindowFactory`, and `HostSurfaceDescriptor`.
- [x] Rename `OmniHostWindow` to `Win32HostWindow`.
- [x] Keep `Win32Runtime` as the public runtime entry point.
- [x] Keep the current sample running with WebView2 + Win32 only.
- [ ] Do not split projects yet if that slows the first pass too much.

#### Phase 2 — Move Windows hosting out of the WebView2 package

- [x] Create `OmniWebHost.Windows`.
- [x] Move `Win32Runtime`, `Win32HostWindow`, Win32 interop, and DWM logic there.
- [x] Leave `OmniWebHost.WebView2` focused on browser hosting and JS bridge only.
- [x] Keep the public sample API simple: `.UseRuntime(new Win32Runtime())`.

#### Phase 3 — Make window-frame implementations pluggable

- [x] Introduce `IWindowFrameStrategy`.
- [x] Map current `OmniWindowStyle.Normal` and `Frameless` onto concrete frame strategies.
- [x] Add room for Windows-specific variants beyond the current two modes.
- [x] Keep current defaults intact while the strategy layer is introduced.

#### Phase 4 — Prepare multi-window and surface-aware adapters

- [x] Move adapter initialization from raw `nint hostHandle` to `HostSurfaceDescriptor`.
- [ ] Add capability matching between adapters and host-surface kinds.
- [x] Add a host-window coordinator for multi-window lifecycle management.
- [x] Keep single-window app startup as the default path.
- [x] Add internal window definitions and tracked-window snapshots so future main/auxiliary windows can share one coordination pipeline.

#### Phase 5 — Bring the pattern to other platforms

- [ ] Add AppKit-based runtime/window hosting for macOS.
- [ ] Add GTK-based runtime/window hosting for Linux.
- [ ] Add `WKWebView` and `WebKitGtk` adapters against the new host-surface model.
- [ ] Explore SDL only after a browser adapter proves a clean embedding path.

### First-pass rules

- Preserve the current sample and current public entry points where possible.
- Prefer additive changes before breaking changes.
- Land one vertical slice at a time and keep the solution building after each step.
- Update docs and samples in the same PR as each structural step.

## Naming Strategy

> Decision: use `Omni` as the short product brand and `OmniHost` as the planned
> technical package / namespace family after the hosting refactor stabilizes.

### Why this direction

- `Omni` alone is too broad and collision-prone for package and namespace use.
- `OmniHost` is shorter than `OmniWebHost` while still matching the product's
  nature as a native host for web-powered desktop apps.
- Renaming at the same time as the hosting refactor would create too much churn,
  so we will stage the rename after the new seams are in place.

### Planned rename path

- Current state:
  - brand and technical name both use `OmniWebHost`
- Transitional state:
  - docs can refer to the product informally as `Omni`
  - code, packages, assemblies, and namespaces stay on `OmniWebHost.*`
- Target state:
  - `OmniWebHost` -> `OmniHost`
  - `OmniWebHost.Abstractions` -> `OmniHost.Abstractions`
  - `OmniWebHost.Core` -> `OmniHost.Core`
  - `OmniWebHost.Hosting` -> `OmniHost.Hosting`
  - `OmniWebHost.WebView2` -> `OmniHost.WebView2`
  - `OmniWebHost.Windows` -> `OmniHost.Windows`

### Rename milestone

- [ ] Complete Runtime / HostWindow Phase 1 seams first.
- [ ] Reassess package/project rename once `IHostWindow` and `HostSurfaceDescriptor`
      are in place and the Windows host has a stable home.
- [ ] Execute the rename as a single migration pass rather than piecemeal.

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
