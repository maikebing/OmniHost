---
title: JS Bridge
---

# JS Bridge

The JS Bridge (`IJsBridge`) provides **bidirectional** communication between .NET and JavaScript running in the WebView.

## Injected Helper — `window.omni`

OmniWebHost automatically injects a lightweight bridge script into every page at document-creation time (before any page script runs). You do **not** need to add a script tag.

```js
// window.omni is always available in OmniWebHost pages
window.omni.invoke(handler, data) → Promise<any>
window.omni.on(eventName, callback)
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
const greeting = await window.omni.invoke('greet', 'Alice');
console.log(greeting); // "Hello, Alice!"
```

The call returns a `Promise` that resolves with the parsed return value.

---

## Sending Events from .NET to JavaScript (.NET → JS push)

```csharp
await adapter.JsBridge.PostMessageAsync("tick", "{\"time\":\"12:00:00\"}");
```

Subscribe in JavaScript:

```js
window.omni.on('tick', (data) => {
    console.log('Tick received:', data.time);
});
```

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
| .NET → JS response | `{"type":"response","id":"uuid","result":"jsonString"}` |
| .NET → JS event | `{"type":"event","name":"eventName","data":"jsonString"}` |

Invoke calls time out after 30 seconds and reject the Promise with an error.

