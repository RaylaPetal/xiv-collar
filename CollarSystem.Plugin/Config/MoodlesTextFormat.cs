using System.Text.RegularExpressions;

namespace CollarSystem.Plugin.Config;

/// collar/moodles "Moodles markup is stripped before display": Moodles' own status titles can carry its
/// own inline markup - `[color=N]...[/color]`, `[glow=N]...[/glow]`, `[i]...[/i]` (confirmed via the tag
/// set embedded in the installed Moodles.dll itself) - which this plugin never attempts to reproduce
/// visually (see design.md's "strip, don't render" decision, confirmed with the user rather than assumed).
/// Every opening/closing tag token is stripped independently rather than matched as balanced pairs, so a
/// malformed or unpaired tag can never leave a stray bracket visible.
public static class MoodlesTextFormat
{
    private static readonly Regex MarkupTags = new(
        @"\[(?:color=[^\]]*|/color|glow=[^\]]*|/glow|i|/i)\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// Strips Moodles' own markup tags from a status name for display - never call this on a name before
    /// storing/matching it (`AliasBook`, `MoodlesMapping.LocalCatalog`, export/import), only at the point
    /// it's shown in the UI, so name-based lookup (`MoodlesCommand.ForceApply`, exports) keeps matching
    /// exactly what Moodles itself reports.
    public static string StripMarkup(string text) => MarkupTags.Replace(text, "");
}
