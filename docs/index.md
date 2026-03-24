---
title: OmniHost
---

# OmniHost

> Product short name: `Omni`
> Technical package family: `OmniHost`

**A cross-platform .NET desktop WebView hosting framework.**

OmniHost lets you build desktop applications powered by a web front-end, using whichever browser engine is available on the host platform: WebView2 on Windows, WKWebView on macOS, WebKitGTK on Linux, or CEF as a future cross-platform option.

## Navigation

| | |
|--|--|
| [Getting Started](getting-started.md) | Install the package and create your first app |
| [Architecture](architecture.md) | How the components fit together |
| [JS Bridge](js-bridge.md) | Bidirectional C# to JavaScript messaging |
| [Adapters](adapters.md) | Supported browser engines |
| [Migration Guide](../MIGRATION.md) | Upgrade notes from `OmniWebHost*` to `OmniHost*` |
| [Roadmap](roadmap.md) | Release plan and milestones |

## Current Status

Version **0.1.0-preview.3**: Windows/WebView2 is still the most complete path and remains the first-class MVP target.
Linux now also has an experimental `OmniHost.Gtk` + `OmniHost.WebKitGtk` path with GTK host-window support, WebKitGTK embedding, bridge wiring, and native `app://` custom-scheme asset loading in place.
