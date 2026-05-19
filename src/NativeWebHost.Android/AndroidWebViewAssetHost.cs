namespace NativeWebHost.Android;

/// <summary>
/// Shared constants and helpers for serving packaged Android assets to a WebView.
/// </summary>
public static class AndroidWebViewAssetHost
{
    /// <summary>
    /// HTTPS origin used by the Android asset loader.
    /// </summary>
    public const string Origin = "https://appassets.androidplatform.net";

    /// <summary>
    /// APK asset directory containing the web application's published files.
    /// </summary>
    public const string DefaultAssetRoot = "wwwroot";

    /// <summary>
    /// Default application URL used by Android host activities.
    /// </summary>
    public const string RootUrl = Origin + "/";

    internal static string NormalizeStartUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || string.Equals(url, "about:blank", StringComparison.OrdinalIgnoreCase))
            return RootUrl;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return new Uri(new Uri(RootUrl), url.TrimStart('/')).ToString();

        if (!string.Equals(uri.Scheme, "app", StringComparison.OrdinalIgnoreCase))
            return url;

        var builder = new UriBuilder(Origin)
        {
            Path = string.IsNullOrWhiteSpace(uri.AbsolutePath) ? "/" : uri.AbsolutePath,
            Query = uri.Query.TrimStart('#', '?'),
            Fragment = uri.Fragment.TrimStart('#'),
        };
        return builder.Uri.ToString();
    }
}
