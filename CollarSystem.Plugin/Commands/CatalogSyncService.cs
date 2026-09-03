using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CollarSystem.Plugin.Config;

namespace CollarSystem.Plugin.Commands;

/// The per-category added-command counts from a single ParseImport call, plus an overall error when the
/// file wasn't recognizable as a Collar export at all (as opposed to a recognized-but-partially-empty one,
/// which still returns zero-valued counts with Error null).
public readonly record struct CatalogImportResult(int Wardrobe, int Gesture, int Moodles, int Restraints, string? Error)
{
    public int TotalAdded => Wardrobe + Gesture + Moodles + Restraints;
}

/// collar/catalog-sync: composes each category's existing export output into one sectioned text file, and
/// splits an imported file back into each category's quick-command list, using the same matching/dedup
/// behavior each category's own individual import already had (moved here from CollarWindow so the
/// unified flow has one implementation instead of three copies). Does not replace or change any category's
/// own scan/apply logic - only the scan-trigger/export/import UX is unified (design.md's Non-Goals).
public sealed class CatalogSyncService
{
    private const string WardrobeHeader = "## WARDROBE";
    private const string GestureHeader = "## GESTURE";
    private const string MoodlesHeader = "## MOODLES";
    private const string RestraintsHeader = "## RESTRAINTS";

    private static readonly string[] KnownHeaders = [WardrobeHeader, GestureHeader, MoodlesHeader, RestraintsHeader];

    private readonly PluginConfig config;
    private readonly OutfitCommand outfit;
    private readonly GestureCommand gesture;
    private readonly MoodlesCommand moodles;
    private readonly RestraintCommand restraints;

    public CatalogSyncService(PluginConfig config, OutfitCommand outfit, GestureCommand gesture, MoodlesCommand moodles, RestraintCommand restraints)
    {
        this.config = config;
        this.outfit = outfit;
        this.gesture = gesture;
        this.moodles = moodles;
        this.restraints = restraints;
    }

    /// Every category's header is always emitted, even with zero body lines - an empty category is
    /// explicitly represented rather than omitted (collar/catalog-sync's "empty category still
    /// represented" requirement), so a re-export after clearing a category can't be misread on import as
    /// "section absent, leave existing quick commands alone."
    public string BuildExport()
    {
        var sb = new StringBuilder();
        AppendSection(sb, WardrobeHeader, outfit.ExportNames());
        AppendSection(sb, GestureHeader, gesture.ExportCatalog().Split('\n', StringSplitOptions.RemoveEmptyEntries));
        AppendSection(sb, MoodlesHeader, moodles.ExportNames());
        AppendSection(sb, RestraintsHeader, restraints.ExportNames());
        return sb.ToString();
    }

    private static void AppendSection(StringBuilder sb, string header, IEnumerable<string> lines)
    {
        sb.Append(header).Append('\n');
        foreach (var line in lines)
            sb.Append(line).Append('\n');
    }

    /// Populates every category's quick-command list from its corresponding section. A section header
    /// present with zero body lines leaves that category's list untouched (nothing to add). A section
    /// entirely absent from the file (not a well-formed Collar export, or an older/hand-edited one) is
    /// likewise skipped rather than erroring the whole import - only a file with none of the four
    /// recognized headers at all is rejected outright.
    public CatalogImportResult ParseImport(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new CatalogImportResult(0, 0, 0, 0, "File is empty - nothing to import.");

        var sections = SplitSections(text);
        if (sections.Count == 0)
            return new CatalogImportResult(0, 0, 0, 0, "File doesn't look like a Collar export (no recognized sections) - nothing imported.");

        var wardrobeAdded = sections.TryGetValue(WardrobeHeader, out var w)
            ? ImportPlainNames(w, config.QuickCommands.Outfits, name => $"outfit lock {name}")
            : 0;
        var gestureAdded = sections.TryGetValue(GestureHeader, out var g)
            ? ImportGestureLines(g, config.QuickCommands.Gestures)
            : 0;
        var moodlesAdded = sections.TryGetValue(MoodlesHeader, out var m)
            ? ImportPlainNames(m, config.QuickCommands.Moodles, name => $"moodle apply {name}")
            : 0;
        var restraintsAdded = sections.TryGetValue(RestraintsHeader, out var r)
            ? ImportPlainNames(r, config.QuickCommands.Restraints, name => $"restraint lock {name}")
            : 0;

        if (wardrobeAdded > 0 || gestureAdded > 0 || moodlesAdded > 0 || restraintsAdded > 0)
            config.Save();

        return new CatalogImportResult(wardrobeAdded, gestureAdded, moodlesAdded, restraintsAdded, null);
    }

    /// Splits on the four known "## " headers; a line before the first recognized header, or under an
    /// unrecognized header, is ignored rather than treated as an error - tolerant of a hand-edited or
    /// future-versioned file that still carries the sections this version understands.
    private static Dictionary<string, List<string>> SplitSections(string text)
    {
        var result = new Dictionary<string, List<string>>();
        List<string>? current = null;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (KnownHeaders.Contains(line))
            {
                current = result[line] = new List<string>();
                continue;
            }
            if (current is null)
                continue;
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
                current.Add(trimmed);
        }

        return result;
    }

    /// Same matching/dedup and line-sanity guards `ImportQuickCommands` used to apply per-button - skips
    /// an individual malformed line instead of aborting the whole category (task 2.3's "malformed line is
    /// skipped, not fatal" behavior - a deliberate improvement over the old clipboard importer's
    /// abort-on-first-bad-line, since a file can legitimately mix well-formed entries across categories).
    private int ImportPlainNames(IEnumerable<string> lines, List<QuickCommand> target, Func<string, string> toCommand)
    {
        var added = 0;
        foreach (var line in lines)
        {
            if (line.Length > 80 || line.IndexOfAny(['{', '}', ';', '<', '>', '\t']) >= 0 ||
                line.Contains("http://", StringComparison.OrdinalIgnoreCase) || line.Contains("https://", StringComparison.OrdinalIgnoreCase))
                continue;

            var command = toCommand(line);
            if (target.Any(existing => string.Equals(existing.Command, command, StringComparison.OrdinalIgnoreCase)))
                continue;

            target.Add(new QuickCommand { Label = line, Command = command });
            added++;
        }

        if (added > 0)
            target.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));
        return added;
    }

    private static int ImportGestureLines(IEnumerable<string> lines, List<QuickCommand> target)
    {
        var added = 0;
        foreach (var line in lines)
        {
            if (!GestureCommand.TryParseExport(line, out var entry) || entry is null)
                continue;

            var command = $"gesture {entry.Id}";
            if (target.Any(x => x.Command.Equals(command, StringComparison.OrdinalIgnoreCase)))
                continue;

            target.Add(new QuickCommand { Label = entry.Label, Command = command });
            added++;
        }

        if (added > 0)
            target.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));
        return added;
    }
}
