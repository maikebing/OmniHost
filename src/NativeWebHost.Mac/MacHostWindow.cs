using System.Text.Json;
using System.Runtime.ExceptionServices;
using AppKit;
using CoreGraphics;
using Foundation;

namespace NativeWebHost.Mac;

internal sealed class MacHostWindow : IHostWindow
{
    private const double DefaultCascadeX = 40;
    private const double DefaultCascadeY = 40;

    private readonly NativeWebWindowContext _windowContext;
    private readonly NativeWebHostOptions _options;
    private readonly IWebViewAdapter _adapter;
    private readonly IDesktopApp? _desktopApp;
    private readonly CancellationTokenSource _windowLifetime = new();

    private NSWindow? _window;
    private NSView? _contentView;
    private WindowDelegate? _windowDelegate;
    private MacStatusItemController? _statusItemController;
    private Exception? _deferredError;
    private int _surfaceWidth;
    private int _surfaceHeight;
    private string _lastWindowState = "unknown";
    private bool _windowShown;
    private bool _forceCloseRequested;
    private bool _closeApproved;
    private bool _closed;

    internal MacHostWindow(NativeWebWindowContext windowContext, IDesktopApp? desktopApp)
    {
        _windowContext = windowContext ?? throw new ArgumentNullException(nameof(windowContext));
        _options = windowContext.Options;
        _adapter = windowContext.Adapter;
        _desktopApp = desktopApp;
        _surfaceWidth = _options.Width;
        _surfaceHeight = _options.Height;
    }

    public HostSurfaceDescriptor Surface
        => new(HostSurfaceKind.NsView, _contentView?.Handle ?? IntPtr.Zero, _surfaceWidth, _surfaceHeight);

    public void Run()
    {
        EnsureMainThread();

        _window = CreateWindow();
        _contentView = CreateContentView(_window.ContentView?.Bounds ?? new CGRect(0, 0, _options.Width, _options.Height));
        _window.ContentView = _contentView;
        _windowDelegate = new WindowDelegate(this);
        _window.Delegate = _windowDelegate;
        _statusItemController = MacStatusItemController.Create(_windowContext, this);

        _ = InitializeAsync();
        RunWindowLoop();
    }

    public void RequestClose()
        => PostToMainThread(() => _window?.PerformClose(null));

    public void RequestActivate()
        => PostToMainThread(() => ShowMainWindow("activate"));

    private NSWindow CreateWindow()
    {
        var frame = CreateInitialFrame(_options.Width, _options.Height);
        var style = _options.WindowStyle == NativeWebWindowStyle.Frameless
            ? NSWindowStyle.Borderless | NSWindowStyle.Resizable
            : NSWindowStyle.Titled | NSWindowStyle.Closable | NSWindowStyle.Miniaturizable | NSWindowStyle.Resizable;

        var window = new NSWindow(frame, style, NSBackingStore.Buffered, false)
        {
            Title = _options.Title,
            MinSize = new CGSize(360, 240)
        };
        window.ReleaseWhenClosed(false);

        if (_options.WindowStyle == NativeWebWindowStyle.Frameless)
        {
            window.TitleVisibility = NSWindowTitleVisibility.Hidden;
            window.TitlebarAppearsTransparent = true;
        }

        window.Center();
        window.CascadeTopLeftFromPoint(new CGPoint(DefaultCascadeX, DefaultCascadeY));
        return window;
    }

    private void RunWindowLoop()
    {
        var app = NSApplication.SharedApplication;
        while (!_closed)
        {
            using var nextEvent = app.NextEvent(
                NSEventMask.AnyEvent,
                NSDate.DistantFuture,
                NSRunLoopMode.Default,
                true);

            if (nextEvent is not null)
                app.SendEvent(nextEvent);

            app.UpdateWindows();
        }

        if (_deferredError is not null)
            ExceptionDispatchInfo.Capture(_deferredError).Throw();
    }

    private static CGRect CreateInitialFrame(int width, int height)
    {
        var screenFrame = NSScreen.MainScreen?.Frame ?? new CGRect(0, 0, width, height);
        var x = screenFrame.X + Math.Max(0, (screenFrame.Width - width) / 2);
        var y = screenFrame.Y + Math.Max(0, (screenFrame.Height - height) / 2);
        return new CGRect(x, y, width, height);
    }

    private static NSView CreateContentView(CGRect frame)
        => new(frame)
        {
            AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable
        };

    private async Task InitializeAsync()
    {
        try
        {
            if (_contentView is null)
                throw new InvalidOperationException("The macOS host surface has not been created yet.");

            UpdateSurfaceSize();
            await _adapter.InitializeAsync(Surface, _options);
            _adapter.Resize(_surfaceWidth, _surfaceHeight);
            RegisterWindowBridgeHandlers();

            if (_desktopApp is IWindowAwareDesktopApp windowAwareDesktopApp)
            {
                await windowAwareDesktopApp.OnWindowStartAsync(
                    _windowContext,
                    _windowLifetime.Token);
            }
            else if (_desktopApp is not null)
            {
                await _desktopApp.OnStartAsync(_adapter, _windowLifetime.Token);
            }

            await _adapter.NavigateAsync(_options.StartUrl, _windowLifetime.Token);
            ShowMainWindow("first_navigation_completed");
            await PublishWindowStateChangedIfNeededAsync("initialized");
        }
        catch (Exception ex)
        {
            _deferredError = ex;
            Console.Error.WriteLine(ex);
            RequestForceClose();
        }
    }

    private void RegisterWindowBridgeHandlers()
    {
        var bridge = _adapter.JsBridge;

        bridge.RegisterHandler("window.minimize", _payload =>
        {
            PostToMainThread(() =>
            {
                _window?.Miniaturize(null);
                _ = PublishWindowStateChangedIfNeededAsync("bridge_minimize", "minimized");
            });
            return Task.FromResult("null");
        });

        bridge.RegisterHandler("window.maximize", _payload =>
        {
            PostToMainThread(() =>
            {
                _window?.Zoom(null);
                _ = PublishWindowStateChangedIfNeededAsync("bridge_maximize");
            });
            return Task.FromResult("null");
        });

        bridge.RegisterHandler("window.close", _payload =>
        {
            RequestClose();
            return Task.FromResult("null");
        });

        bridge.RegisterHandler("window.exit", _payload =>
        {
            RequestForceClose();
            return Task.FromResult("null");
        });

        bridge.RegisterHandler("window.startDrag", _payload =>
        {
            // AppKit handles normal title-bar dragging. Custom frameless drag can be
            // added here when NativeWebHost exposes a mouse-down event payload.
            return Task.FromResult("null");
        });

        bridge.RegisterHandler("window.showSystemMenu", _payload => Task.FromResult("null"));
    }

    private bool ShouldClose()
    {
        if (_closeApproved)
            return true;

        if (ShouldHideInsteadOfClose())
        {
            HideMainWindowToStatusItem();
            return false;
        }

        _ = CloseAsync("window_should_close");
        return false;
    }

    private async Task CloseAsync(string reason)
    {
        if (_closeApproved)
            return;

        _closeApproved = true;

        try
        {
            _windowLifetime.Cancel();

            await PublishWindowLifecycleEventAsync(
                "window.closing",
                new MacWindowLifecyclePayload(_windowContext.WindowId, GetCurrentWindowState(), reason));

            if (_desktopApp is IWindowAwareDesktopApp windowAwareDesktopApp)
            {
                await windowAwareDesktopApp.OnWindowClosingAsync(
                    _windowContext,
                    _windowLifetime.Token);
            }
            else if (_desktopApp is not null)
            {
                await _desktopApp.OnClosingAsync();
            }
        }
        catch
        {
        }

        PostToMainThread(() => _window?.Close());
    }

    private void OnWindowWillClose()
    {
        try
        {
            PublishWindowLifecycleEventAsync(
                "window.closed",
                new MacWindowLifecyclePayload(_windowContext.WindowId, "closed", "window_will_close"))
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
        }

        _statusItemController?.Dispose();
        _statusItemController = null;

        _windowLifetime.Dispose();
        _adapter.DisposeAsync().AsTask().GetAwaiter().GetResult();

        if (_window is not null)
        {
            _window.Delegate = null;
            _window = null;
        }

        _contentView = null;
        _windowDelegate?.Dispose();
        _windowDelegate = null;

        _closed = true;
    }

    private bool ShouldHideInsteadOfClose()
        => _windowContext.IsMainWindow
           && _options.EnableTrayIcon
           && _options.HideMainWindowOnClose
           && !_forceCloseRequested;

    private void HideMainWindowToStatusItem()
    {
        _window?.OrderOut(null);
        _lastWindowState = "hidden";
        _ = PublishWindowLifecycleEventAsync(
            "window.stateChanged",
            new MacWindowLifecyclePayload(
                _windowContext.WindowId,
                "hidden",
                "close_to_status_item",
                IsMinimized: false,
                IsMaximized: false,
                Width: _surfaceWidth,
                Height: _surfaceHeight));
    }

    private void ShowMainWindow(string reason)
    {
        if (_window is null)
            return;

        NSRunningApplication.CurrentApplication.Activate(
            NSApplicationActivationOptions.ActivateIgnoringOtherWindows);

        if (_window.IsMiniaturized)
            _window.Deminiaturize(null);

        if (!_windowShown)
        {
            _window.MakeKeyAndOrderFront(null);
            if (_options.StartMaximized && !_window.IsZoomed)
                _window.Zoom(null);
            _windowShown = true;
        }
        else
        {
            _window.MakeKeyAndOrderFront(null);
        }

        _ = PublishWindowStateChangedIfNeededAsync(reason);
    }

    private void RequestForceClose()
    {
        _forceCloseRequested = true;
        _ = CloseAsync("force_close");
    }

    private void OnResize()
    {
        UpdateSurfaceSize();
        _adapter.Resize(_surfaceWidth, _surfaceHeight);
        _ = PublishWindowStateChangedIfNeededAsync("window_resize");
    }

    private void UpdateSurfaceSize()
    {
        var bounds = _contentView?.Bounds ?? _window?.Frame ?? new CGRect(0, 0, _options.Width, _options.Height);
        _surfaceWidth = Math.Max(1, (int)Math.Round(bounds.Width));
        _surfaceHeight = Math.Max(1, (int)Math.Round(bounds.Height));
    }

    private string GetCurrentWindowState()
    {
        if (_window is null)
            return "normal";

        if (!_window.IsVisible)
            return "hidden";

        if (_window.IsMiniaturized)
            return "minimized";

        if (_window.IsZoomed)
            return "maximized";

        return "normal";
    }

    private async Task PublishWindowStateChangedIfNeededAsync(string reason, string? windowState = null)
    {
        var nextState = windowState ?? GetCurrentWindowState();
        if (string.Equals(_lastWindowState, nextState, StringComparison.Ordinal))
            return;

        _lastWindowState = nextState;
        await PublishWindowLifecycleEventAsync(
            "window.stateChanged",
            new MacWindowLifecyclePayload(
                _windowContext.WindowId,
                nextState,
                reason,
                IsMinimized: string.Equals(nextState, "minimized", StringComparison.Ordinal),
                IsMaximized: string.Equals(nextState, "maximized", StringComparison.Ordinal),
                Width: _surfaceWidth,
                Height: _surfaceHeight));
    }

    private async Task PublishWindowLifecycleEventAsync(
        string eventName,
        MacWindowLifecyclePayload payload)
    {
        try
        {
            await _adapter.JsBridge.PostMessageAsync(
                eventName,
                JsonSerializer.Serialize(payload, MacJsonContext.Default.MacWindowLifecyclePayload));
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void PostToMainThread(Action action)
    {
        if (NSThread.IsMain)
        {
            action();
            return;
        }

        NSApplication.SharedApplication.BeginInvokeOnMainThread(action);
    }

    private static void EnsureMainThread()
    {
        if (!NSThread.IsMain)
            throw new InvalidOperationException("MacHostWindow must be created on the AppKit main thread.");
    }

    private sealed class WindowDelegate : NSWindowDelegate
    {
        private readonly MacHostWindow _owner;

        public WindowDelegate(MacHostWindow owner)
        {
            _owner = owner;
        }

        public override bool WindowShouldClose(NSObject sender)
            => _owner.ShouldClose();

        public override void WillClose(NSNotification notification)
            => _owner.OnWindowWillClose();

        public override void DidResize(NSNotification notification)
            => _owner.OnResize();

        public override void DidEndLiveResize(NSNotification notification)
            => _owner.OnResize();

        public override void DidMiniaturize(NSNotification notification)
            => _ = _owner.PublishWindowStateChangedIfNeededAsync("miniaturized", "minimized");

        public override void DidDeminiaturize(NSNotification notification)
            => _ = _owner.PublishWindowStateChangedIfNeededAsync("deminiaturized");
    }

    private sealed class MacStatusItemController : IDisposable
    {
        private readonly NativeWebWindowContext _windowContext;
        private readonly MacHostWindow _window;
        private readonly NSStatusItem _statusItem;

        private MacStatusItemController(
            NativeWebWindowContext windowContext,
            MacHostWindow window,
            NSStatusItem statusItem)
        {
            _windowContext = windowContext;
            _window = window;
            _statusItem = statusItem;
        }

        public static MacStatusItemController? Create(
            NativeWebWindowContext windowContext,
            MacHostWindow window)
        {
            if (!windowContext.IsMainWindow || !windowContext.Options.EnableTrayIcon)
                return null;

            var statusItem = NSStatusBar.SystemStatusBar.CreateStatusItem(NSStatusItemLength.Variable);
            var controller = new MacStatusItemController(windowContext, window, statusItem);
            controller.ConfigureStatusItem();
            return controller;
        }

        public void Dispose()
        {
            NSStatusBar.SystemStatusBar.RemoveStatusItem(_statusItem);
            _statusItem.Menu = null;
        }

        private void ConfigureStatusItem()
        {
            var button = _statusItem.Button;
            if (button is not null)
            {
                button.Title = _windowContext.Options.TrayToolTip ?? _windowContext.Options.Title;
                button.ToolTip = GetToolTip();

                var icon = LoadIcon();
                if (icon is not null)
                {
                    icon.Size = new CGSize(18, 18);
                    button.Image = icon;
                    button.Title = string.Empty;
                }
            }

            RebuildMenu();
        }

        private void RebuildMenu()
        {
            var menu = new NSMenu(_windowContext.Options.Title);
            AddMenuItem(menu, _windowContext.Options.TrayOpenText, () => _window.ShowMainWindow("status_item_open"));
            menu.AddItem(NSMenuItem.SeparatorItem);

            var customItems = GetCustomMenuItems();
            if (customItems.Count > 0)
            {
                foreach (var item in customItems)
                {
                    if (item.Separator)
                    {
                        menu.AddItem(NSMenuItem.SeparatorItem);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(item.Text))
                        continue;

                    AddMenuItem(
                        menu,
                        item.Text,
                        () => DispatchCustomCommandAsync(item.Id).GetAwaiter().GetResult(),
                        item.Enabled && !string.IsNullOrWhiteSpace(item.Id),
                        item.Checked);
                }

                menu.AddItem(NSMenuItem.SeparatorItem);
            }

            AddMenuItem(menu, _windowContext.Options.TrayExitText, _window.RequestForceClose);
            _statusItem.Menu = menu;
        }

        private void AddMenuItem(
            NSMenu menu,
            string title,
            Action action,
            bool enabled = true,
            bool isChecked = false)
        {
            var item = new NSMenuItem(title, (_, _) => action())
            {
                Enabled = enabled,
                State = isChecked ? NSCellStateValue.On : NSCellStateValue.Off
            };
            menu.AddItem(item);
        }

        private IReadOnlyList<NativeWebTrayMenuItem> GetCustomMenuItems()
        {
            try
            {
                return _windowContext.Options.TrayMenuProvider?.Invoke()
                    ?? Array.Empty<NativeWebTrayMenuItem>();
            }
            catch
            {
                return Array.Empty<NativeWebTrayMenuItem>();
            }
        }

        private async Task DispatchCustomCommandAsync(string commandId)
        {
            var handler = _windowContext.Options.TrayCommandHandler;
            if (handler is null || string.IsNullOrWhiteSpace(commandId))
                return;

            try
            {
                await handler(commandId, _window._windowLifetime.Token);
                RebuildMenu();
            }
            catch
            {
            }
        }

        private string GetToolTip()
        {
            try
            {
                return _windowContext.Options.TrayToolTipProvider?.Invoke()
                    ?? _windowContext.Options.TrayToolTip
                    ?? _windowContext.Options.Title;
            }
            catch
            {
                return _windowContext.Options.TrayToolTip ?? _windowContext.Options.Title;
            }
        }

        private NSImage? LoadIcon()
        {
            var iconPath = _windowContext.Options.IconPath;
            if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
                return new NSImage(iconPath, lazy: false);

            return NSApplication.SharedApplication.ApplicationIconImage;
        }
    }
}
