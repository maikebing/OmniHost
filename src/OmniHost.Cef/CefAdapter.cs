using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;

namespace OmniHost.Cef;

/// <summary>
/// Experimental <see cref="IWebViewAdapter"/> implementation backed by CefSharp.
/// </summary>
public sealed class CefAdapter : IWebViewAdapter
{
    private BrowserCapabilities _capabilities = new()
    {
        EngineName = "CEF",
        EngineVersion = "unknown",
        SupportsJavaScript = true,
        SupportsJsBridge = true,
        SupportsCustomSchemes = true,
        SupportsDevTools = true,
        SupportedHostSurfaces = new[] { HostSurfaceKind.Hwnd },
    };

    private CefSharpJsBridge? _bridge;
    private ChromiumWebBrowser? _browser;
    private Control? _parentControl;
    private OmniHostOptions? _options;

    public string AdapterId => "cef";

    public BrowserCapabilities Capabilities => _capabilities;

    public IJsBridge JsBridge
        => _bridge ?? throw new InvalidOperationException(
            "CefAdapter has not been initialized yet. Call InitializeAsync first.");

    public Task InitializeAsync(
        HostSurfaceDescriptor surface,
        OmniHostOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        if (surface.Kind != HostSurfaceKind.Hwnd)
            throw new NotSupportedException("OmniHost.Cef currently supports only HWND host surfaces.");

        if (!surface.IsCreated)
            throw new InvalidOperationException("The supplied host surface has not been created yet.");

        CefRuntimeManager.EnsureInitialized(options);

        var parentControl = Control.FromHandle(surface.Handle);
        if (parentControl is null)
        {
            throw new NotSupportedException(
                "OmniHost.Cef currently requires a WinForms host surface. Use OmniHost.WinForms.WinFormsRuntime together with CefAdapterFactory.");
        }

        _parentControl = parentControl;
        _options = options;

        var browser = new ChromiumWebBrowser("about:blank")
        {
            Dock = DockStyle.Fill,
        };

        browser.JavascriptObjectRepository.Settings.LegacyBindingEnabled = true;

        var bridge = new CefSharpJsBridge(browser);
        browser.JavascriptObjectRepository.Register(
            "omni",
            bridge.CreateJavascriptBoundApi(),
            BindingOptions.DefaultBinder);

        parentControl.Controls.Add(browser);
        browser.BringToFront();

        _browser = browser;
        _bridge = bridge;

        var engineVersion = global::CefSharp.Cef.ChromiumVersion;
        if (!string.IsNullOrWhiteSpace(engineVersion))
        {
            _capabilities = new BrowserCapabilities
            {
                EngineName = "CEF",
                EngineVersion = engineVersion,
                SupportsJavaScript = true,
                SupportsJsBridge = true,
                SupportsCustomSchemes = true,
                SupportsDevTools = true,
                SupportedHostSurfaces = new[] { HostSurfaceKind.Hwnd },
            };
        }

        return bridge.WaitForBrowserInitializedAsync(cancellationToken);
    }

    public Task NavigateAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureInitialized();
        _browser!.Load(url);
        return Task.CompletedTask;
    }

    public void Resize(int width, int height)
    {
        if (_browser is null)
            return;

        if (_browser.Parent is not null)
            _browser.Bounds = new System.Drawing.Rectangle(0, 0, width, height);
    }

    public ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            if (_parentControl is not null && !_parentControl.IsDisposed && _parentControl.Controls.Contains(_browser))
                _parentControl.Controls.Remove(_browser);

            _bridge?.Dispose();
            _browser.Dispose();
        }

        _browser = null;
        _bridge = null;
        _parentControl = null;
        _options = null;

        return ValueTask.CompletedTask;
    }

    private void EnsureInitialized()
    {
        if (_browser is null || _bridge is null)
            throw new InvalidOperationException(
                "CefAdapter has not been initialized. Call InitializeAsync first.");
    }
}
