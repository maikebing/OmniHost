using CefSharp;
using CefSharp.SchemeHandler;
using CefSharp.WinForms;

namespace OmniHost.Cef;

internal static class CefRuntimeManager
{
    private static readonly object Gate = new();
    private static bool _initialized;
    private static string? _registeredScheme;

    public static void EnsureInitialized(OmniHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("OmniHost.Cef is currently only supported on Windows.");

        lock (Gate)
        {
            if (_initialized)
            {
                ValidateCompatibleScheme(options);
                return;
            }

            var settings = new CefSettings
            {
                CachePath = options.UserDataFolder
                    ?? Path.Combine(Path.GetTempPath(), "OmniHost", "Cef", SanitizeFolderName(options.Title)),
                PersistSessionCookies = true,
            };

            if (options.EnableDevTools)
                settings.RemoteDebuggingPort = 9222;

            if (!string.IsNullOrWhiteSpace(options.ContentRootPath))
            {
                settings.RegisterScheme(new CefCustomScheme
                {
                    SchemeName = options.CustomScheme,
                    DomainName = "localhost",
                    SchemeHandlerFactory = new FolderSchemeHandlerFactory(
                        rootFolder: options.ContentRootPath,
                        hostName: "localhost",
                        defaultPage: "index.html",
                        schemeName: options.CustomScheme)
                });

                _registeredScheme = options.CustomScheme;
            }

            if (!global::CefSharp.Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null))
                throw new InvalidOperationException("CEF failed to initialize for OmniHost.");

            _initialized = true;
        }
    }

    private static void ValidateCompatibleScheme(OmniHostOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ContentRootPath))
            return;

        if (_registeredScheme is null)
            throw new InvalidOperationException(
                "CEF was initialized without custom-scheme support, but this window requests a custom content root.");

        if (!string.Equals(_registeredScheme, options.CustomScheme, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"CEF was already initialized with scheme '{_registeredScheme}', so scheme '{options.CustomScheme}' cannot be added later in the same process.");
        }
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c).ToArray());
    }
}
