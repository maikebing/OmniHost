---
title: NativeWebHost
---

# NativeWebHost

> Product short name: `Omni`
> Technical package family: `NativeWebHost`

**A cross-platform .NET desktop WebView hosting framework.**

NativeWebHost lets you build desktop applications powered by a web front-end, using native OS windows and native OS WebViews: WebView2 on Windows, WKWebView on macOS, and WebKitGTK on Linux.

## Navigation

| | |
|--|--|
| [Getting Started](getting-started.md) | Install the package and create your first app |
| [Architecture](architecture.md) | How the components fit together |
| [JS Bridge](js-bridge.md) | Bidirectional C# to JavaScript messaging |
| [Adapters](adapters.md) | Supported browser engines |
| [Migration Guide](../MIGRATION.md) | Upgrade notes for the package consolidation |
| [Roadmap](roadmap.md) | Release plan and milestones |

## Current Status

Version **0.1.0-preview.3**: Windows now uses the raw Win32 runtime with `NativeWebHost.Windows`.
Linux has an experimental `NativeWebHost.Linux` path with GTK host-window support, WebKitGTK embedding, bridge wiring, and native `app://` custom-scheme asset loading.
The macOS target is AppKit + WKWebView.
