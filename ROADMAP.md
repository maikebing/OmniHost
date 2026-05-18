# Roadmap

## Native Platform Direction

OmniHost keeps one native runtime and one native WebView adapter per operating system.

| OS | Runtime | WebView | Package status |
|----|---------|---------|----------------|
| Windows | raw Win32 | Native WebView2 | implemented |
| Linux | GTK 3 | WebKitGTK | experimental |
| macOS | AppKit | WKWebView | planned |

Removed from the framework surface:

- `OmniHost.WinForms`
- `OmniHost.WebView2`
- `OmniHost.Cef`
- `samples/OmniHost.Sample.Cef`

The goal is a single application model for downstream projects such as Cosmos and IoTCoWork: ASP.NET Core / Blazor / static web assets in the application layer, with a platform-native desktop shell selected at startup or publish time.

## Implemented

- Shared abstractions and host coordinator
- Raw Win32 runtime and host window
- Native WebView2 adapter using WebView2Aot
- GTK runtime and WebKitGTK adapter
- JavaScript bridge
- `app://localhost/...` local asset loading
- Multi-window support
- Splash window helper
- Window manager operations
- Windows style presets and built-in title-bar injection

## Next Milestones

1. Stabilize `OmniHost.NativeWebView2` as the only Windows adapter.
2. Validate Linux packaging with bundled GTK/WebKitGTK native libraries where possible.
3. Add `OmniHost.AppKit`.
4. Add `OmniHost.WKWebView`.
5. Add macOS packaging helpers for `.app` and `.dmg`.

## Packaging Principle

Users should not need to install a separate desktop framework manually.

- Windows: rely on the OS WebView2 runtime or ship a fixed WebView2 runtime with the app.
- macOS: rely on system WebKit through WKWebView.
- Linux: package GTK/WebKitGTK dependencies with the app package where the target distribution does not provide a stable baseline.
