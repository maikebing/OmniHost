# OmniHost.Sample.AotMinimalApi

This sample is intentionally backend-first and NativeAOT-friendly:

- ASP.NET Core Minimal API
- static HTML front-end
- source-generated JSON metadata
- no MVC, no Razor Views, no Roslyn scripting

Run locally:

```powershell
dotnet run --project samples/OmniHost.Sample.AotMinimalApi
```

Try a NativeAOT publish:

```powershell
dotnet publish samples/OmniHost.Sample.AotMinimalApi -c Release -r win-x64
```

If you want to wrap it with OmniHost, point a desktop window at `http://127.0.0.1:5078/`.
