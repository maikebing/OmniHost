using OmniWebHost;
using OmniWebHost.WebView2;

// Minimal compilable placeholder — full runtime behaviour requires WebView2 runtime on Windows.
var app = OmniApp.CreateBuilder(args)
    .Configure(o =>
    {
        o.Title = "OmniWebHost Basic Sample";
        o.StartUrl = "https://example.com";
        o.Width = 1280;
        o.Height = 800;
        o.EnableDevTools = true;
    })
    .UseAdapter(new WebView2AdapterFactory())
    .Build();

await app.RunAsync();
