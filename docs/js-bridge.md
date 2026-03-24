---
title: JS Bridge
---

# JS Bridge

The JS Bridge (`IJsBridge`) provides **bidirectional** communication between .NET and JavaScript running in the WebView.

## Injected Helper — `omni`

OmniWebHost automatically injects a lightweight bridge script into every page at document-creation time (before any page script runs). You do **not** need to add a script tag.

```js
// omni is always available in OmniWebHost pages
omni.invoke(handler, data) → Promise<any>
omni.on(eventName, callback)
omni.window.minimize()
omni.window.maximize()
omni.window.close()
omni.window.startDrag()
omni.window.showSystemMenu()
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
const greeting = await omni.invoke('greet', 'Alice');
console.log(greeting); // "Hello, Alice!"
```

The call returns a `Promise` that resolves with the parsed return value.

For custom window chrome, pages can mark any drag region with the `omni-drag`
attribute. OmniWebHost automatically wires these regions so:

- Left-drag moves the window
- Double-click toggles maximize / restore
- Right-click opens the native system menu

The current native window style is also exposed as the CSS variable
`--omni-window-style` and as the `data-omni-window-style` attribute on
`document.documentElement`.

---

## Sending Events from .NET to JavaScript (.NET → JS push)

```csharp
await adapter.JsBridge.PostMessageAsync("tick", "{\"time\":\"12:00:00\"}");
```

Subscribe in JavaScript:

```js
omni.on('tick', (data) => {
    console.log('Tick received:', data.time);
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
