# NativeWebHost macOS Sample

This sample shows the intended macOS wiring for `NativeWebHost.Mac`:

```csharp
.UseAdapter(new WKWebViewAdapterFactory())
.UseRuntime(new MacRuntime())
```

It uses the same multi-window bridge surface as the Windows and Linux adapter samples, backed by AppKit windows and WKWebView.

Run from macOS:

```bash
dotnet run --project samples/NativeWebHost.Sample.Mac
```
