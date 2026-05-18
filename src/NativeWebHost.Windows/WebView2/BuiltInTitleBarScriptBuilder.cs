using System.Text.Json;

namespace NativeWebHost.Windows;

internal static class BuiltInTitleBarScriptBuilder
{
    public static string? Build(NativeWebHostOptions options)
    {
        var preset = options.BuiltInTitleBarStyle.ToCssToken();
        if (string.Equals(preset, "none", StringComparison.Ordinal))
            return null;

        var height = options.BuiltInTitleBarStyle == NativeWebBuiltInTitleBarStyle.Office ? 44 : 36;
        var css = options.BuiltInTitleBarStyle switch
        {
            NativeWebBuiltInTitleBarStyle.VsCode => GetVsCodeCss(height),
            NativeWebBuiltInTitleBarStyle.Office => GetOfficeCss(height),
            _ => string.Empty,
        };

        var html = options.BuiltInTitleBarStyle switch
        {
            NativeWebBuiltInTitleBarStyle.VsCode => """
                <div class="native-web-titlebar__left" native-web-drag>
                  <div class="native-web-titlebar__appbadge" aria-hidden="true"></div>
                  <div class="native-web-titlebar__title" native-web-drag></div>
                </div>
                <div class="native-web-titlebar__right">
                  <button type="button" data-native-web-window-action="minimize" aria-label="Minimize">&minus;</button>
                  <button type="button" data-native-web-window-action="maximize" aria-label="Maximize">&square;</button>
                  <button type="button" data-native-web-window-action="close" class="native-web-titlebar__close" aria-label="Close">&times;</button>
                </div>
                """,
            NativeWebBuiltInTitleBarStyle.Office => """
                <div class="native-web-titlebar__left" native-web-drag>
                  <div class="native-web-titlebar__office-badge">Office</div>
                  <div class="native-web-titlebar__title" native-web-drag></div>
                </div>
                <div class="native-web-titlebar__right">
                  <div class="native-web-titlebar__pill">AutoSave</div>
                  <button type="button" data-native-web-window-action="minimize" aria-label="Minimize">&minus;</button>
                  <button type="button" data-native-web-window-action="maximize" aria-label="Maximize">&square;</button>
                  <button type="button" data-native-web-window-action="close" class="native-web-titlebar__close" aria-label="Close">&times;</button>
                </div>
                """,
            _ => string.Empty,
        };

        var config = JsonSerializer.Serialize(
            new BuiltInTitleBarConfig(preset, options.Title, height, css, html),
            NativeWebView2JsonContext.Default.BuiltInTitleBarConfig);

        return $$"""
            (function () {
                var config = {{config}};

                function applyBuiltInTitleBar() {
                    if (!document.documentElement || !document.body) return;

                    document.documentElement.style.setProperty('--native-web-built-in-titlebar-height', config.height + 'px');
                    document.documentElement.setAttribute('data-native-web-built-in-titlebar', config.preset);

                    if (!document.getElementById('native-web-built-in-titlebar-style')) {
                        var style = document.createElement('style');
                        style.id = 'native-web-built-in-titlebar-style';
                        style.textContent = config.css;
                        (document.head || document.documentElement).appendChild(style);
                    }

                    var host = document.getElementById('native-web-built-in-titlebar');
                    if (!host) {
                        host = document.createElement('div');
                        host.id = 'native-web-built-in-titlebar';
                        host.className = 'native-web-titlebar native-web-titlebar--' + config.preset;
                        host.innerHTML = config.html;
                        document.body.prepend(host);
                    }

                    var titleNode = host.querySelector('.native-web-titlebar__title');
                    if (titleNode) titleNode.textContent = config.title;

                    host.querySelectorAll('[data-native-web-window-action]').forEach(function (button) {
                        if (button.dataset.nativeWebBound === '1') return;
                        button.dataset.nativeWebBound = '1';
                        button.addEventListener('click', function () {
                            var action = button.getAttribute('data-native-web-window-action');
                            if (action === 'minimize') nativeWeb.window.minimize();
                            if (action === 'maximize') nativeWeb.window.maximize();
                            if (action === 'close') nativeWeb.window.close();
                        });
                    });
                }

                if (document.readyState === 'loading') {
                    document.addEventListener('DOMContentLoaded', applyBuiltInTitleBar, { once: true });
                }

                applyBuiltInTitleBar();
            })();
            """;
    }

    private static string GetVsCodeCss(int height)
        => $$"""
            :root { --native-web-built-in-titlebar-height: {{height}}px; }
            html, body { height: 100%; }
            body { padding-top: var(--native-web-built-in-titlebar-height) !important; }
            #native-web-built-in-titlebar { position: fixed; top: 0; left: 0; right: 0; height: var(--native-web-built-in-titlebar-height); z-index: 2147483640; }
            .native-web-titlebar { display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: center; background: rgba(37,37,38,0.92); color: #d4d4d4; border-bottom: 1px solid rgba(255,255,255,0.05); backdrop-filter: blur(18px); user-select: none; }
            .native-web-titlebar__left { display: flex; align-items: center; gap: 10px; min-width: 0; padding-left: 12px; }
            .native-web-titlebar__appbadge { width: 16px; height: 16px; border-radius: 4px; background: linear-gradient(135deg, #3794ff, #0e639c); box-shadow: 0 0 0 1px rgba(255,255,255,0.12); flex: 0 0 auto; }
            .native-web-titlebar__title { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; font-size: .82rem; color: #cccccc; }
            .native-web-titlebar__right { display: flex; align-items: stretch; height: 100%; }
            .native-web-titlebar__right button { width: 46px; border: 0; background: transparent; color: #cccccc; cursor: pointer; font: inherit; }
            .native-web-titlebar__right button:hover { background: rgba(255,255,255,0.08); }
            .native-web-titlebar__right .native-web-titlebar__close:hover { background: #c42b1c; color: white; }
            """;

    private static string GetOfficeCss(int height)
        => $$"""
            :root { --native-web-built-in-titlebar-height: {{height}}px; }
            html, body { height: 100%; }
            body { padding-top: var(--native-web-built-in-titlebar-height) !important; }
            #native-web-built-in-titlebar { position: fixed; top: 0; left: 0; right: 0; height: var(--native-web-built-in-titlebar-height); z-index: 2147483640; }
            .native-web-titlebar { display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: center; background: rgba(255,255,255,0.88); color: #1f2937; border-bottom: 1px solid rgba(15,23,42,0.08); backdrop-filter: blur(16px); user-select: none; }
            .native-web-titlebar__left, .native-web-titlebar__right { display: flex; align-items: center; gap: 10px; min-width: 0; height: 100%; }
            .native-web-titlebar__left { padding-left: 14px; }
            .native-web-titlebar__office-badge { padding: 6px 10px; border-radius: 10px; background: linear-gradient(135deg, #b7472a, #d35400); color: white; font-size: .82rem; font-weight: 800; letter-spacing: .04em; flex: 0 0 auto; }
            .native-web-titlebar__title { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; font-size: .92rem; font-weight: 700; }
            .native-web-titlebar__right { padding-right: 6px; }
            .native-web-titlebar__pill { padding: 6px 10px; border-radius: 999px; background: rgba(15,23,42,0.05); border: 1px solid rgba(15,23,42,0.08); color: #667085; font-size: .8rem; font-weight: 700; }
            .native-web-titlebar__right button { width: 42px; border: 0; background: transparent; color: #475467; cursor: pointer; font: inherit; }
            .native-web-titlebar__right button:hover { background: rgba(15,23,42,0.06); }
            .native-web-titlebar__right .native-web-titlebar__close:hover { background: #c42b1c; color: white; }
            """;
}
