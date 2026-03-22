namespace OmniWebHost;

/// <summary>
/// Factory that creates <see cref="IWebViewAdapter"/> instances.
/// Register one implementation per supported browser engine via DI.
/// </summary>
public interface IWebViewAdapterFactory
{
    /// <summary>Identifier of the adapter this factory produces (e.g. "webview2").</summary>
    string AdapterId { get; }

    /// <summary>
    /// Returns <see langword="true"/> when the adapter can be used in the current environment
    /// (e.g. WebView2 runtime is installed on Windows).
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>Creates a new, uninitialized <see cref="IWebViewAdapter"/> instance.</summary>
    IWebViewAdapter Create();
}
