---
title: JS Bridge
---

# JS Bridge

The JS Bridge (`IJsBridge`) provides **bidirectional** communication between .NET and JavaScript running in the WebView.

## Injected Helper — `nativeWeb`

NativeWebHost automatically injects a lightweight bridge script into every page at document-creation time (before any page script runs). You do **not** need to add a script tag.

```js
// nativeWeb is always available in NativeWebHost pages
nativeWeb.invoke(handler, data) → Promise<any>
nativeWeb.on(eventName, callback)
nativeWeb.window.minimize()
nativeWeb.window.maximize()
nativeWeb.window.close()
nativeWeb.window.startDrag()
nativeWeb.window.showSystemMenu()
nativeWeb.on('window.stateChanged', callback)
nativeWeb.on('window.closing', callback)
nativeWeb.on('window.closed', callback)
```

---

## Calling .NET from JavaScript

Register a named handler on the .NET side before navigation:

```csharp
adapter.JsBridge.RegisterHandler("greet", async payload =>
{
    var name = payload.Trim('"'); // payload is a JSON string
    return $"\"Hello, {name}!\""; // return value must also be valid JSON
});
```

Call it from JavaScript:

```js
const greeting = await nativeWeb.invoke('greet', 'Alice');
console.log(greeting); // "Hello, Alice!"
```

The call returns a `Promise` that resolves with the parsed return value.

For custom window chrome, pages can mark any drag region with the `native-web-drag`
attribute. NativeWebHost automatically wires these regions so:

- Left-drag moves the window
- Double-click toggles maximize / restore
- Right-click opens the native system menu

The current native window style is also exposed as the CSS variable
`--native-web-window-style` and as the `data-native-web-window-style` attribute on
`document.documentElement`.

On Windows, the host also publishes native lifecycle events back into the page:

- `window.stateChanged` with `state`, `isMinimized`, `isMaximized`, `width`, `height`, and `reason`
- `window.closing` when a close request reaches the native host window
- `window.closed` as a best-effort final notification during teardown

---

## Sending Events from .NET to JavaScript (.NET → JS push)

```csharp
await adapter.JsBridge.PostMessageAsync("tick", "{\"time\":\"12:00:00\"}");
```

Subscribe in JavaScript:

```js
nativeWeb.on('tick', (data) => {
    console.log('Tick received:', data.time);
});

nativeWeb.on('window.stateChanged', (data) => {
    console.log('Window state:', data.state, data.width, data.height);
});
```

Outgoing host events are buffered until the current page finishes navigation, so
early `.NET -> JS` pushes are delivered after the document is ready.

---

## Executing Raw JavaScript from .NET

```csharp
var title = await adapter.JsBridge.ExecuteScriptAsync("document.title");
```

---

## Protocol Details

The bridge uses a JSON envelope over the native `window.chrome.webview` WebView2 messaging channel.

| Direction | JSON shape |
|-----------|-----------|
| JS → .NET invoke | `{"type":"invoke","handler":"name","id":"uuid","data":"jsonString"}` |
| .NET → JS response | `{"type":"response","id":"uuid","ok":true,"result":"jsonString"}` |
| .NET → JS error | `{"type":"response","id":"uuid","ok":false,"error":"message"}` |
| .NET → JS event | `{"type":"event","name":"eventName","data":"jsonString"}` |

Invoke calls time out after 30 seconds and reject the Promise with an error.
