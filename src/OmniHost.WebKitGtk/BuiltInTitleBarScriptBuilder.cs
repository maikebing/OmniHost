namespace OmniHost.WebKitGtk;

internal static class BuiltInTitleBarScriptBuilder
{
    public static string? Build(OmniHostOptions options)
    {
        var preset = options.BuiltInTitleBarStyle.ToCssToken();
        if (string.Equals(preset, "none", StringComparison.Ordinal))
            return null;

        var title = options.Title;
        var height = options.BuiltInTitleBarStyle == OmniBuiltInTitleBarStyle.Office ? 44 : 36;
        var css = options.BuiltInTitleBarStyle switch
        {
            OmniBuiltInTitleBarStyle.VsCode => GetVsCodeCss(height),
            OmniBuiltInTitleBarStyle.Office => GetOfficeCss(height),
            _ => string.Empty,
        };

        var html = options.BuiltInTitleBarStyle switch
        {
            OmniBuiltInTitleBarStyle.VsCode => """
                <div class="omni-titlebar__left" omni-drag>
                  <div class="omni-titlebar__appbadge" aria-hidden="true"></div>
                  <div class="omni-titlebar__title" omni-drag></div>
                </div>
                <div class="omni-titlebar__right">
                  <button type="button" data-omni-window-action="minimize" aria-label="Minimize">&minus;</button>
                  <button type="button" data-omni-window-action="maximize" aria-label="Maximize">&square;</button>
                  <button type="button" data-omni-window-action="close" class="omni-titlebar__close" aria-label="Close">&times;</button>
                </div>
                """,
            OmniBuiltInTitleBarStyle.Office => """
                <div class="omni-titlebar__left" omni-drag>
                  <div class="omni-titlebar__office-badge">Office</div>
                  <div class="omni-titlebar__title" omni-drag></div>
                </div>
                <div class="omni-titlebar__right">
                  <div class="omni-titlebar__pill">AutoSave</div>
                  <button type="button" data-omni-window-action="minimize" aria-label="Minimize">&minus;</button>
                  <button type="button" data-omni-window-action="maximize" aria-label="Maximize">&square;</button>
                  <button type="button" data-omni-window-action="close" class="omni-titlebar__close" aria-label="Close">&times;</button>
                </div>
                """,
            _ => string.Empty,
        };

        return $$"""
            (function () {
                var config = {
                    preset: {{System.Text.Json.JsonSerializer.Serialize(preset)}},
                    title: {{System.Text.Json.JsonSerializer.Serialize(title)}},
                    height: {{height}},
                    css: {{System.Text.Json.JsonSerializer.Serialize(css)}},
                    html: {{System.Text.Json.JsonSerializer.Serialize(html)}}
                };

                function applyBuiltInTitleBar() {
                    if (!document.documentElement || !document.body) return;

                    document.documentElement.style.setProperty('--omni-built-in-titlebar-height', config.height + 'px');
                    document.documentElement.setAttribute('data-omni-built-in-titlebar', config.preset);

                    if (!document.getElementById('omni-built-in-titlebar-style')) {
                        var style = document.createElement('style');
                        style.id = 'omni-built-in-titlebar-style';
                        style.textContent = config.css;
                        (document.head || document.documentElement).appendChild(style);
                    }

                    var host = document.getElementById('omni-built-in-titlebar');
                    if (!host) {
                        host = document.createElement('div');
                        host.id = 'omni-built-in-titlebar';
                        host.className = 'omni-titlebar omni-titlebar--' + config.preset;
                        host.innerHTML = config.html;
                        document.body.prepend(host);
                    }

                    var titleNode = host.querySelector('.omni-titlebar__title');
                    if (titleNode) titleNode.textContent = config.title;

                    host.querySelectorAll('[data-omni-window-action]').forEach(function (button) {
                        if (button.dataset.omniBound === '1') return;
                        button.dataset.omniBound = '1';
                        button.addEventListener('click', function () {
                            var action = button.getAttribute('data-omni-window-action');
                            if (action === 'minimize') omni.window.minimize();
                            if (action === 'maximize') omni.window.maximize();
                            if (action === 'close') omni.window.close();
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
            :root { --omni-built-in-titlebar-height: {{height}}px; }
            html, body { height: 100%; }
            body { padding-top: var(--omni-built-in-titlebar-height) !important; }
            #omni-built-in-titlebar { position: fixed; top: 0; left: 0; right: 0; height: var(--omni-built-in-titlebar-height); z-index: 2147483640; }
            .omni-titlebar { display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: center; background: rgba(37,37,38,0.92); color: #d4d4d4; border-bottom: 1px solid rgba(255,255,255,0.05); backdrop-filter: blur(18px); user-select: none; }
            .omni-titlebar__left { display: flex; align-items: center; gap: 10px; min-width: 0; padding-left: 12px; }
            .omni-titlebar__appbadge { width: 16px; height: 16px; border-radius: 4px; background: linear-gradient(135deg, #3794ff, #0e639c); box-shadow: 0 0 0 1px rgba(255,255,255,0.12); flex: 0 0 auto; }
            .omni-titlebar__title { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; font-size: .82rem; color: #cccccc; }
            .omni-titlebar__right { display: flex; align-items: stretch; height: 100%; }
            .omni-titlebar__right button { width: 46px; border: 0; background: transparent; color: #cccccc; cursor: pointer; font: inherit; }
            .omni-titlebar__right button:hover { background: rgba(255,255,255,0.08); }
            .omni-titlebar__right .omni-titlebar__close:hover { background: #c42b1c; color: white; }
            """;

    private static string GetOfficeCss(int height)
        => $$"""
            :root { --omni-built-in-titlebar-height: {{height}}px; }
            html, body { height: 100%; }
            body { padding-top: var(--omni-built-in-titlebar-height) !important; }
            #omni-built-in-titlebar { position: fixed; top: 0; left: 0; right: 0; height: var(--omni-built-in-titlebar-height); z-index: 2147483640; }
            .omni-titlebar { display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: center; background: rgba(255,255,255,0.88); color: #1f2937; border-bottom: 1px solid rgba(15,23,42,0.08); backdrop-filter: blur(16px); user-select: none; }
            .omni-titlebar__left, .omni-titlebar__right { display: flex; align-items: center; gap: 10px; min-width: 0; height: 100%; }
            .omni-titlebar__left { padding-left: 14px; }
            .omni-titlebar__office-badge { padding: 6px 10px; border-radius: 10px; background: linear-gradient(135deg, #b7472a, #d35400); color: white; font-size: .82rem; font-weight: 800; letter-spacing: .04em; flex: 0 0 auto; }
            .omni-titlebar__title { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; font-size: .92rem; font-weight: 700; }
            .omni-titlebar__right { padding-right: 6px; }
            .omni-titlebar__pill { padding: 6px 10px; border-radius: 999px; background: rgba(15,23,42,0.05); border: 1px solid rgba(15,23,42,0.08); color: #667085; font-size: .8rem; font-weight: 700; }
            .omni-titlebar__right button { width: 42px; border: 0; background: transparent; color: #475467; cursor: pointer; font: inherit; }
            .omni-titlebar__right button:hover { background: rgba(15,23,42,0.06); }
            .omni-titlebar__right .omni-titlebar__close:hover { background: #c42b1c; color: white; }
            """;
}
