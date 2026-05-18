---
title: Roadmap
---

# Roadmap

## Decision

OmniHost keeps one native desktop path per operating system:

| OS | Runtime | WebView |
|----|---------|---------|
| Windows | raw Win32 | Native WebView2 |
| Linux | GTK 3 | WebKitGTK |
| macOS | AppKit | WKWebView |

Removed paths: WinForms, the old managed `OmniHost.WebView2` adapter, and CefSharp/CEF.

## Current

- Windows: `OmniHost.Windows` + `OmniHost.NativeWebView2`
- Linux: `OmniHost.Gtk` + `OmniHost.WebKitGtk`
- Shared: multi-window lifecycle, splash windows, JavaScript bridge, `app://localhost/...` local asset loading

## Next

- Add `OmniHost.AppKit` runtime.
- Add `OmniHost.WKWebView` adapter.
- Bring macOS bridge, custom-scheme loading, and window management to parity with Windows/Linux.
- Add packaging guidance for:
  - Windows fixed WebView2 runtime when Evergreen cannot be assumed
  - macOS `.app` / `.dmg`
  - Linux AppImage or distro packages that bundle GTK/WebKitGTK dependencies where appropriate

## Samples

- `samples/OmniHost.Sample.Basic`
- `samples/OmniHost.Sample.CrossPlatform`
- `samples/OmniHost.Sample.Gtk`
- `samples/OmniHost.Sample.WindowStyles`
- `samples/OmniHost.Sample.WebShowcase`
- `samples/OmniHost.Sample.AotMinimalApi`
