namespace OmniHost;

/// <summary>
/// 描述一个原生托盘菜单项。
/// </summary>
public sealed record OmniTrayMenuItem(
    string Id,
    string Text,
    bool Enabled = true,
    bool Checked = false,
    bool Separator = false)
{
    public static OmniTrayMenuItem CreateSeparator()
        => new(string.Empty, string.Empty, Enabled: false, Separator: true);
}
