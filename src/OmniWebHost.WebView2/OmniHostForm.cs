namespace OmniWebHost.WebView2;

/// <summary>
/// Internal WinForms <see cref="Form"/> that hosts a <see cref="WebView2Adapter"/>.
/// </summary>
internal sealed class OmniHostForm : Form
{
    private readonly OmniWebHostOptions _options;
    private readonly IWebViewAdapter _adapter;
    private readonly IDesktopApp? _desktopApp;

    internal OmniHostForm(
        OmniWebHostOptions options,
        IWebViewAdapter adapter,
        IDesktopApp? desktopApp)
    {
        _options = options;
        _adapter = adapter;
        _desktopApp = desktopApp;

        Text = options.Title;
        ClientSize = new System.Drawing.Size(options.Width, options.Height);

        if (options.StartMaximized)
            WindowState = FormWindowState.Maximized;
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        try
        {
            await _adapter.InitializeAsync(Handle, _options);

            // Size the WebView to fill the client area.
            if (_adapter is WebView2Adapter wv2)
                wv2.Resize(ClientSize.Width, ClientSize.Height);

            // Let the application register bridge handlers before first navigation.
            if (_desktopApp is not null)
                await _desktopApp.OnStartAsync(_adapter);

            await _adapter.NavigateAsync(_options.StartUrl);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"OmniWebHost failed to initialise WebView2:\n\n{ex.Message}\n\n" +
                "Make sure the Microsoft Edge WebView2 Runtime is installed.\n" +
                "Download: https://developer.microsoft.com/microsoft-edge/webview2/",
                "OmniWebHost – Initialisation Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Close();
        }
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        try
        {
            if (_desktopApp is not null)
                await _desktopApp.OnClosingAsync();
        }
        catch { /* ignore errors during shutdown */ }
    }

    protected override void OnClientSizeChanged(EventArgs e)
    {
        base.OnClientSizeChanged(e);
        if (_adapter is WebView2Adapter wv2)
            wv2.Resize(ClientSize.Width, ClientSize.Height);
    }
}
