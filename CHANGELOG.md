# Changelog

All notable changes to this project will be documented in this file.  
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

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
