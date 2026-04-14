namespace OmniHost.Cef;

/// <summary>
/// Factory that produces <see cref="CefAdapter"/> instances.
/// </summary>
public sealed class CefAdapterFactory : IWebViewAdapterFactory
{
    public string AdapterId => "cef";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public IWebViewAdapter Create() => new CefAdapter();
}
