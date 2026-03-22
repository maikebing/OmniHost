---
title: JS Bridge
---

# JS Bridge

The JS Bridge (`IJsBridge`) provides bidirectional communication between your .NET application code and JavaScript running inside the WebView.

## Executing JavaScript from .NET

```csharp
var result = await adapter.JsBridge.ExecuteScriptAsync("document.title");
Console.WriteLine(result); // e.g. "My Page"
```

## Calling .NET from JavaScript

Register a named handler on the .NET side:

```csharp
adapter.JsBridge.RegisterHandler("greet", async payload =>
{
    // payload is a JSON string sent from JavaScript
    var name = System.Text.Json.JsonSerializer.Deserialize<string>(payload);
    return System.Text.Json.JsonSerializer.Serialize($"Hello, {name}!");
});
```

Invoke it from JavaScript (exact API depends on the adapter):

```js
// WebView2 uses window.chrome.webview.postMessage
window.chrome.webview.postMessage(JSON.stringify({ handler: "greet", data: "World" }));
```

## Sending Messages from .NET to JavaScript

```csharp
await adapter.JsBridge.PostMessageAsync("appReady", "{\"version\":\"0.1.0\"}");
```

Subscribe in JavaScript:

```js
window.addEventListener("message", e => {
    const { event, payload } = JSON.parse(e.data);
    if (event === "appReady") console.log("App version:", payload.version);
});
```

## Notes

- The exact JS-side API varies slightly per adapter to match each engine's native messaging mechanism.
- A unified JavaScript SDK (`omni.js`) that normalises the API across adapters is planned for a future milestone.
