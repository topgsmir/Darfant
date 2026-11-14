namespace ServiceLib.Services.CoreConfig;

internal static class WindowsVpnScope
{
    internal static readonly string[] BrowserExecutables =
    [
        "chrome", "msedge", "firefox", "brave", "opera", "opera_gx", "vivaldi", "chromium",
        "waterfox", "librewolf", "floorp", "arc", "zen", "torbrowser", "browser", "coccoc",
        "duckduckgo", "iexplore"
    ];

    internal static string ProcessRegex(TunModeItem config, bool escapeRuleDelimiters = false)
    {
        var matches = config.Scope == ETunScope.Browsers
            ? BrowserExecutables.Select(name => @"[/\\]" + Regex.Escape(name) + @"\.exe$")
            : (config.ApplicationPaths ?? []).Where(path => Path.IsPathFullyQualified(path))
                .Select(path => "^" + Regex.Escape(path) + "$");
        var pattern = string.Join("|", matches);
        if (escapeRuleDelimiters)
        {
            pattern = pattern.Replace(",", @"\x2c").Replace(@"\(", @"\x28").Replace(@"\)", @"\x29");
        }
        return pattern.Length == 0 ? "^$a" : "(?i)" + pattern;
    }
}
