using System.Text.Json;
using OmniHost;
using OmniHost.WinForms.Win32;

namespace OmniHost.WinForms;

internal sealed class WinFormsHostWindow : IHostWindow
{
    private readonly OmniWindowContext _windowContext;
    private readonly OmniHostOptions _options;
    private readonly IWebViewAdapter _adapter;
    private readonly IDesktopApp? _desktopApp;
    private readonly CancellationTokenSource _windowLifetime = new();

    private HostForm? _form;
    private Exception? _deferredError;
    private int _surfaceWidth;
    private int _surfaceHeight;
    private string _lastWindowState = "unknown";
    private int _closeRequested;
    private bool _closeApproved;

    public WinFormsHostWindow(OmniWindowContext windowContext, IDesktopApp? desktopApp)
    {
        _windowContext = windowContext ?? throw new ArgumentNullException(nameof(windowContext));
        _options = windowContext.Options;
        _adapter = windowContext.Adapter;
        _desktopApp = desktopApp;
        _surfaceWidth = _options.Width;
        _surfaceHeight = _options.Height;
    }

    public HostSurfaceDescriptor Surface
        => new(HostSurfaceKind.Hwnd, _form?.Handle ?? IntPtr.Zero, _surfaceWidth, _surfaceHeight);

    public void Run()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var form = new HostForm(this);
        _form = form;
        Application.Run(form);
        _form = null;

        if (_deferredError is not null)
            throw _deferredError;
    }

    public void RequestClose() => PostToForm(form => form.Close());

    public void RequestActivate()
    {
        PostToForm(form =>
        {
            if (form.WindowState == FormWindowState.Minimized)
                form.WindowState = FormWindowState.Normal;

            form.Show();
            form.Activate();
        });
    }

    private void PostToForm(Action<HostForm> action)
    {
        var form = _form;
        if (form is null || form.IsDisposed)
            return;

        if (form.InvokeRequired)
        {
            form.BeginInvoke(() => action(form));
            return;
        }

        action(form);
    }

    private async Task InitializeAsync()
    {
        try
        {
            var form = _form ?? throw new InvalidOperationException("The WinForms host window is not ready.");
            _surfaceWidth = form.ClientSize.Width;
            _surfaceHeight = form.ClientSize.Height;

            await _adapter.InitializeAsync(Surface, _options);
            _adapter.Resize(_surfaceWidth, _surfaceHeight);

            RegisterWindowBridgeHandlers();
            await PublishWindowStateChangedIfNeededAsync("initialized");

            if (_desktopApp is IWindowAwareDesktopApp windowAwareDesktopApp)
            {
                await windowAwareDesktopApp.OnWindowStartAsync(_windowContext, _windowLifetime.Token);
            }
            else if (_desktopApp is not null)
            {
                await _desktopApp.OnStartAsync(_adapter, _windowLifetime.Token);
            }

            await _adapter.NavigateAsync(_options.StartUrl);
        }
        catch (Exception ex)
        {
            _deferredError = ex;

            MessageBox.Show(
                $"OmniHost failed to initialize the browser adapter '{_adapter.AdapterId}':\n\n{ex.Message}",
                "OmniHost - Initialization Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            PostToForm(form =>
            {
                _closeApproved = true;
                form.Close();
            });
        }
    }

    private void RegisterWindowBridgeHandlers()
    {
        var bridge = _adapter.JsBridge;

        bridge.RegisterHandler("window.minimize", _ =>
        {
            PostToForm(form => form.WindowState = FormWindowState.Minimized);
            return Task.FromResult("null");
        });

        bridge.RegisterHandler("window.maximize", _ =>
        {
            PostToForm(form =>
            {
                form.WindowState = form.WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized;
            });
            return Task.FromResult("null");
        });

        bridge.RegisterHandler("window.close", _ =>
        {
            RequestClose();
            return Task.FromResult("null");
        });

        bridge.RegisterHandler("window.startDrag", _ =>
        {
            PostToForm(form =>
            {
                NativeMethods.ReleaseCapture();
                NativeMethods.SendMessageW(
                    form.Handle,
                    NativeMethods.WM_NCLBUTTONDOWN,
                    (IntPtr)NativeMethods.HTCAPTION,
                    IntPtr.Zero);
            });

            return Task.FromResult("null");
        });

        bridge.RegisterHandler("window.showSystemMenu", _ =>
        {
            PostToForm(form => ShowSystemMenu(form.Handle));
            return Task.FromResult("null");
        });
    }

    private static void ShowSystemMenu(IntPtr hwnd)
    {
        var menu = NativeMethods.GetSystemMenu(hwnd, false);
        if (menu == IntPtr.Zero)
            return;

        if (!NativeMethods.GetCursorPos(out var point))
            return;

        NativeMethods.SetForegroundWindow(hwnd);
        var command = NativeMethods.TrackPopupMenuEx(
            menu,
            NativeMethods.TPM_LEFTALIGN | NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_RIGHTBUTTON,
            point.x,
            point.y,
            hwnd,
            IntPtr.Zero);

        if (command != 0)
        {
            NativeMethods.PostMessageW(
                hwnd,
                NativeMethods.WM_SYSCOMMAND,
                (IntPtr)command,
                IntPtr.Zero);
        }

        NativeMethods.PostMessageW(hwnd, NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);
    }

    private void OnClientSizeChanged(HostForm form)
    {
        if (form.WindowState != FormWindowState.Minimized)
        {
            _surfaceWidth = form.ClientSize.Width;
            _surfaceHeight = form.ClientSize.Height;
            _adapter.Resize(_surfaceWidth, _surfaceHeight);
        }

        _ = PublishWindowStateChangedIfNeededAsync("client_size_changed");
    }

    private void BeginClose(HostForm form, FormClosingEventArgs e)
    {
        if (_closeApproved)
            return;

        if (Interlocked.Exchange(ref _closeRequested, 1) != 0)
        {
            e.Cancel = true;
            return;
        }

        e.Cancel = true;
        _ = CompleteCloseAsync(form);
    }

    private async Task CompleteCloseAsync(HostForm form)
    {
        try
        {
            _windowLifetime.Cancel();

            await PublishWindowLifecycleEventAsync("window.closing", new
            {
                state = GetCurrentWindowState(form),
                reason = "form_closing",
            });

            if (_desktopApp is IWindowAwareDesktopApp windowAwareDesktopApp)
            {
                await windowAwareDesktopApp.OnWindowClosingAsync(_windowContext, _windowLifetime.Token);
            }
            else if (_desktopApp is not null)
            {
                await _desktopApp.OnClosingAsync();
            }
        }
        catch
        {
        }
        finally
        {
            if (!form.IsDisposed)
            {
                _closeApproved = true;
                form.BeginInvoke(() => form.Close());
            }
        }
    }

    private void OnFormClosed(HostForm form)
    {
        PublishWindowLifecycleEventAsync("window.closed", new
        {
            state = GetCurrentWindowState(form),
            reason = "form_closed",
        }).GetAwaiter().GetResult();

        _adapter.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _windowLifetime.Dispose();
    }

    private string GetCurrentWindowState(HostForm? form = null)
    {
        var windowState = (form ?? _form)?.WindowState ?? FormWindowState.Normal;
        return windowState switch
        {
            FormWindowState.Minimized => "minimized",
            FormWindowState.Maximized => "maximized",
            _ => "normal",
        };
    }

    private async Task PublishWindowStateChangedIfNeededAsync(string reason)
    {
        var nextState = GetCurrentWindowState();
        if (string.Equals(_lastWindowState, nextState, StringComparison.Ordinal))
            return;

        _lastWindowState = nextState;
        await PublishWindowLifecycleEventAsync("window.stateChanged", new
        {
            state = nextState,
            isMinimized = string.Equals(nextState, "minimized", StringComparison.Ordinal),
            isMaximized = string.Equals(nextState, "maximized", StringComparison.Ordinal),
            width = _surfaceWidth,
            height = _surfaceHeight,
            reason,
        });
    }

    private async Task PublishWindowLifecycleEventAsync(string eventName, object payload)
    {
        try
        {
            await _adapter.JsBridge.PostMessageAsync(eventName, JsonSerializer.Serialize(payload));
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private sealed class HostForm : Form
    {
        private readonly WinFormsHostWindow _owner;

        public HostForm(WinFormsHostWindow owner)
        {
            _owner = owner;
            Text = owner._options.Title;
            ClientSize = new System.Drawing.Size(owner._options.Width, owner._options.Height);
            StartPosition = FormStartPosition.CenterScreen;

            if (owner._options.WindowStyle is OmniWindowStyle.Frameless or OmniWindowStyle.VsCode)
            {
                FormBorderStyle = FormBorderStyle.None;
            }

            if (owner._options.StartMaximized)
                WindowState = FormWindowState.Maximized;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _ = _owner.InitializeAsync();
        }

        protected override void OnClientSizeChanged(EventArgs e)
        {
            base.OnClientSizeChanged(e);
            _owner.OnClientSizeChanged(this);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _owner.BeginClose(this, e);
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _owner.OnFormClosed(this);
            base.OnFormClosed(e);
        }
    }
}
