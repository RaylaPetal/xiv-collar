using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Oathbound.Plugin.Config;

namespace Oathbound.Plugin.Commands;

/// One entry in the Aliases export section: the bare alias word plus a human-readable summary of what it
/// does - see AliasBook's doc comment for why the Owner is deliberately shown this, unlike the live wire
/// tell (which only ever carries the bare alias word). A plain mutable class, matching GestureExportEntry's
/// own shape, rather than a record - plays safest with System.Text.Json's default (de)serialization.
public class AliasExportEntry
{
    public string Alias { get; set; } = "";
    public string Description { get; set; } = "";

    /// collar/catalog-sync "Import skips commands that duplicate an existing quick command": the design
    /// id, gesture id, or Moodles status name this alias applies - only ever set for a single-action
    /// Outfit/Gesture/Moodle alias (never Title, Restraint, or a multi-action bundle), so import-time
    /// dedup can recognize "this alias and that plain scanned entry are the same target" without parsing
    /// the human-readable Description. Null on an older export predating this field.
    public string? Target { get; set; }

    public AliasExportEntry() { }
    public AliasExportEntry(string alias, string description, string? target = null)
    {
        Alias = alias;
        Description = description;
        Target = target;
    }
}

/// The per-category added-command counts from a single ParseImport call, plus an overall error when the
/// file wasn't recognizable as a Collar export at all (as opposed to a recognized-but-partially-empty one,
/// which still returns zero-valued counts with Error null). Wardrobe/Gesture/Moodles/Restraints each fold
/// together that category's scanned-name adds and its single-action-alias adds, since both now land in the
/// same Owner quick-command list (collar/catalog-sync's "Owner imports alias names as one-off quick
/// commands", reworked to route by category). Bundles covers only genuinely multi-action Custom Triggers
/// (plus a single-action Chat trigger, which has no matching category list of its own).
public readonly record struct CatalogImportResult(int Title, int Wardrobe, int Gesture, int Moodles, int Restraints, int Bundles, int Duplicates, string? Error)
{
    public int TotalAdded => Title + Wardrobe + Gesture + Moodles + Restraints + Bundles;
}

/// collar/catalog-sync: composes each category's existing export output into one sectioned text file, and
/// splits an imported file back into each category's quick-command list, using the same matching/dedup
/// behavior each category's own individual import already had (moved here from CollarWindow so the
/// unified flow has one implementation instead of three copies). Does not replace or change any category's
/// own scan/apply logic - only the scan-trigger/export/import UX is unified (design.md's Non-Goals).
public sealed class CatalogSyncService
{
    private const string TitleAliasesHeader = "## TITLE_ALIASES";
    private const string WardrobeHeader = "## WARDROBE";
    private const string WardrobeAliasesHeader = "## WARDROBE_ALIASES";
    private const string GestureHeader = "## GESTURE";
    private const string GestureAliasesHeader = "## GESTURE_ALIASES";
    private const string MoodlesHeader = "## MOODLES";
    private const string MoodlesAliasesHeader = "## MOODLES_ALIASES";
    private const string RestraintsHeader = "## RESTRAINTS";
    private const string RestraintsAliasesHeader = "## RESTRAINTS_ALIASES";

    /// Kept under its original header name for backward compatibility - an export from before this change
    /// mixed every single- and multi-action alias/trigger here, and an old file's "## ALIASES" section
    /// still parses today, landing entirely in the Custom Trigger Bundle list (design.md's "Risks" and
    /// tasks.md 2.5). Going forward this section only ever receives genuinely multi-action Custom Triggers
    /// (plus a single-action Chat trigger - see ExportBundleEntries).
    private const string BundlesHeader = "## ALIASES";

    private const string AliasExportPrefix = "COLLAR-ALIAS-V1|";

    private static readonly string[] KnownHeaders =
    [
        TitleAliasesHeader, WardrobeHeader, WardrobeAliasesHeader, GestureHeader, GestureAliasesHeader,
        MoodlesHeader, MoodlesAliasesHeader, RestraintsHeader, RestraintsAliasesHeader, BundlesHeader,
    ];

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
        AppendSection(sb, TitleAliasesHeader, ExportCategoryAliasEntries(CustomTriggerActionKind.Title, config.Aliases.Titles.Select(a => new AliasExportEntry(a.Alias, DescribeTitleAlias(a)))).Select(EncodeAliasEntry));
        AppendSection(sb, WardrobeHeader, outfit.ExportNames());
        AppendSection(sb, WardrobeAliasesHeader, ExportCategoryAliasEntries(CustomTriggerActionKind.Outfit, config.Aliases.Outfits.Select(a => new AliasExportEntry(a.Alias, DescribeOutfitAlias(a), a.DesignName))).Select(EncodeAliasEntry));
        AppendSection(sb, GestureHeader, gesture.ExportCatalog().Split('\n', StringSplitOptions.RemoveEmptyEntries));
        AppendSection(sb, GestureAliasesHeader, ExportCategoryAliasEntries(CustomTriggerActionKind.Gesture, config.Aliases.Gestures.Select(a => new AliasExportEntry(a.Alias, DescribeGestureAlias(a), a.GestureId))).Select(EncodeAliasEntry));
        AppendSection(sb, MoodlesHeader, moodles.ExportNames());
        AppendSection(sb, MoodlesAliasesHeader, ExportCategoryAliasEntries(CustomTriggerActionKind.Moodle, config.Aliases.Moodles.Select(a => new AliasExportEntry(a.Alias, DescribeMoodleAlias(a), MoodlesTextFormat.StripMarkup(a.StatusName)))).Select(EncodeAliasEntry));
        AppendSection(sb, RestraintsHeader, restraints.ExportNames());
        AppendSection(sb, RestraintsAliasesHeader, ExportCategoryAliasEntries(CustomTriggerActionKind.Restraint, config.Aliases.Restraints.Select(a => new AliasExportEntry(a.Alias, DescribeRestraintAlias(a)))).Select(EncodeAliasEntry));
        AppendSection(sb, BundlesHeader, ExportBundleEntries().Select(EncodeAliasEntry));
        return sb.ToString();
    }

    private static void AppendSection(StringBuilder sb, string header, IEnumerable<string> lines)
    {
        sb.Append(header).Append('\n');
        foreach (var line in lines)
            sb.Append(line).Append('\n');
    }

    /// collar/catalog-sync "Exporting every catalog to one file": a category's own alias definitions plus
    /// any Custom Trigger that bundles exactly one action of that same category, deduplicated by alias
    /// word - each carries a human-readable summary of what it does alongside the bare word, so an Owner
    /// importing this file knows what they're actually sending (see AliasBook's doc comment for why this
    /// is a deliberate choice, not an oversight - the live wire tell during real commanding still only
    /// ever carries the bare alias word, unaffected by this).
    private IReadOnlyList<AliasExportEntry> ExportCategoryAliasEntries(CustomTriggerActionKind kind, IEnumerable<AliasExportEntry> categoryDefinitions) =>
        DedupSort(categoryDefinitions.Concat(SingleActionTriggerEntries(kind)));

    private IEnumerable<AliasExportEntry> SingleActionTriggerEntries(CustomTriggerActionKind kind) =>
        config.Aliases.CustomTriggers
            .Where(t => t.Actions.Count == 1 && t.Actions[0].Kind == kind)
            .Select(t => new AliasExportEntry(t.Alias, DescribeCustomTrigger(t), TargetForSingleAction(t.Actions[0])));

    /// collar/catalog-sync "Import skips commands that duplicate an existing quick command": only Outfit/
    /// Gesture/Moodle carry a target identity the Owner's import can match on - Title (free text) and
    /// Restraint (Sub-captured, not scan-derived) fall through to null, same as their own alias
    /// definitions never populate a `Target` on export.
    private static string? TargetForSingleAction(CustomTriggerAction action) => action.Kind switch
    {
        CustomTriggerActionKind.Outfit => action.OutfitDesignName,
        CustomTriggerActionKind.Gesture => action.GestureId,
        CustomTriggerActionKind.Moodle => MoodlesTextFormat.StripMarkup(action.MoodleStatusName),
        _ => null,
    };

    /// Follow's fixed engage/release words and the singleton Clear-title/Unlock-outfit/Clear-moodle
    /// aliases are deliberately excluded from every section above - the Owner already has dedicated fixed
    /// quick-command rows for all of those, so exporting them would be redundant. Custom Triggers that
    /// bundle two or more actions have no single matching category, so they - along with a single-action
    /// Chat trigger, since Chat has no Owner-side category list of its own - are the only entries left in
    /// the Custom Trigger Bundle section.
    private IReadOnlyList<AliasExportEntry> ExportBundleEntries() =>
        DedupSort(config.Aliases.CustomTriggers
            .Where(t => t.Actions.Count >= 2 || (t.Actions.Count == 1 && t.Actions[0].Kind == CustomTriggerActionKind.Chat))
            .Select(t => new AliasExportEntry(t.Alias, DescribeCustomTrigger(t))));

    private static IReadOnlyList<AliasExportEntry> DedupSort(IEnumerable<AliasExportEntry> entries) =>
        entries.Where(e => e.Alias.Length > 0)
            .GroupBy(e => e.Alias, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(e => e.Alias, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string DescribeTitleAlias(TitleAliasDefinition a) => $"Title: \"{a.Text}\" ({(a.IsPrefix ? "prefix" : "suffix")})";
    private static string DescribeOutfitAlias(OutfitAliasDefinition a) => $"Outfit: {a.DesignName}{(a.Locked ? " (locks its slots)" : "")}";
    private static string DescribeGestureAlias(GestureAliasDefinition a) => $"Gesture: {(a.AnimationName.Length > 0 ? a.AnimationName : a.EmoteName)}";
    private static string DescribeRestraintAlias(RestraintAliasDefinition a) => $"Restraint: {a.DeviceName} (toggles)";
    private static string DescribeMoodleAlias(MoodlesAliasDefinition a) => $"Moodle: {MoodlesTextFormat.StripMarkup(a.StatusName)}";
    private static string DescribeCustomTrigger(CustomTriggerDefinition a) => $"Custom Trigger: {string.Join(", ", a.Actions.Select(CustomTriggerCommand.Summarize))}";

    private static string EncodeAliasEntry(AliasExportEntry entry) =>
        AliasExportPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry)));

    /// Fails closed (returns false) on anything that isn't a well-formed encoded alias line - an older/
    /// hand-edited export's bare-word Aliases lines (this format's predecessor) no longer parse, so they're
    /// silently skipped rather than imported with a fabricated description, matching `ImportGestureLines`'s
    /// own "unparseable line is skipped, not fatal" behavior.
    private static bool TryParseAliasEntry(string line, out AliasExportEntry entry)
    {
        entry = new AliasExportEntry();
        if (!line.StartsWith(AliasExportPrefix, StringComparison.Ordinal))
            return false;

        try
        {
            var decoded = JsonSerializer.Deserialize<AliasExportEntry>(Encoding.UTF8.GetString(Convert.FromBase64String(line[AliasExportPrefix.Length..])));
            if (decoded is null || decoded.Alias.Length == 0)
                return false;

            entry = decoded;
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return false;
        }
    }

    /// Populates every category's quick-command list from its corresponding section. A section header
    /// present with zero body lines leaves that category's list untouched (nothing to add). A section
    /// entirely absent from the file (not a well-formed Collar export, or an older/hand-edited one) is
    /// likewise skipped rather than erroring the whole import - only a file with none of the ten
    /// recognized headers at all is rejected outright.
    public CatalogImportResult ParseImport(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new CatalogImportResult(0, 0, 0, 0, 0, 0, 0, "File is empty - nothing to import.");

        var sections = SplitSections(text);
        if (sections.Count == 0)
            return new CatalogImportResult(0, 0, 0, 0, 0, 0, 0, "File doesn't look like a Collar export (no recognized sections) - nothing imported.");

        var quick = config.QuickCommands;

        // collar/catalog-sync "Import skips commands that duplicate an existing quick command": seeded
        // once, before any category import runs, from every command already saved anywhere - not just the
        // category currently being populated - so a shared alias word is caught regardless of import
        // order, and each import call below adds to it as it goes so a duplicate introduced earlier in
        // this same file is caught too.
        var usedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cmd in quick.Titles.Concat(quick.Outfits).Concat(quick.Gestures).Concat(quick.Moodles).Concat(quick.Restraints).Concat(quick.Aliases))
            usedCommands.Add(cmd.Command);

        var duplicates = 0;

        var titleAdded = sections.TryGetValue(TitleAliasesHeader, out var ta)
            ? ImportAliasLines(ta, quick.Titles, usedCommands, ref duplicates)
            : 0;

        var wardrobeAdded = sections.TryGetValue(WardrobeHeader, out var w)
            ? ImportPlainNames(w, quick.Outfits, name => $"outfit lock {name}", usedCommands, name => name, ref duplicates)
            : 0;
        wardrobeAdded += sections.TryGetValue(WardrobeAliasesHeader, out var wa)
            ? ImportAliasLines(wa, quick.Outfits, usedCommands, ref duplicates)
            : 0;

        var gestureAdded = sections.TryGetValue(GestureHeader, out var g)
            ? ImportGestureLines(g, quick.Gestures, usedCommands, ref duplicates)
            : 0;
        gestureAdded += sections.TryGetValue(GestureAliasesHeader, out var ga)
            ? ImportAliasLines(ga, quick.Gestures, usedCommands, ref duplicates)
            : 0;

        var moodlesAdded = sections.TryGetValue(MoodlesHeader, out var m)
            ? ImportPlainNames(m, quick.Moodles, name => $"moodle apply {name}", usedCommands, MoodlesTextFormat.StripMarkup, ref duplicates)
            : 0;
        moodlesAdded += sections.TryGetValue(MoodlesAliasesHeader, out var ma)
            ? ImportAliasLines(ma, quick.Moodles, usedCommands, ref duplicates)
            : 0;

        var restraintsAdded = sections.TryGetValue(RestraintsHeader, out var r)
            ? ImportPlainNames(r, quick.Restraints, name => $"restraint lock {name}", usedCommands, targetSelector: null, ref duplicates)
            : 0;
        restraintsAdded += sections.TryGetValue(RestraintsAliasesHeader, out var ra)
            ? ImportAliasLines(ra, quick.Restraints, usedCommands, ref duplicates)
            : 0;

        // Also the landing spot for an older export's flat "## ALIASES" section, which mixed single- and
        // multi-action entries together - those all land here unchanged, never split out retroactively
        // into a single category's list (tasks.md 2.5's backward-compatibility requirement).
        var bundlesAdded = sections.TryGetValue(BundlesHeader, out var b)
            ? ImportAliasLines(b, quick.Aliases, usedCommands, ref duplicates)
            : 0;

        if (titleAdded + wardrobeAdded + gestureAdded + moodlesAdded + restraintsAdded + bundlesAdded > 0)
            config.Save();

        return new CatalogImportResult(titleAdded, wardrobeAdded, gestureAdded, moodlesAdded, restraintsAdded, bundlesAdded, duplicates, null);
    }

    /// Splits on the ten known "## " headers; a line before the first recognized header, or under an
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
    /// `usedCommands` is the shared, whole-import command set (collar/catalog-sync's cross-category
    /// duplicate check); `targetSelector`, when non-null, normalizes the scanned name into the same
    /// identity space a same-category alias's exported `Target` uses (e.g. markup-stripped for Moodles),
    /// enabling the same-target check - passing null (Restraints) opts a category out of that check
    /// entirely, matching "Import skips commands that duplicate an existing quick command"'s exclusions.
    private int ImportPlainNames(IEnumerable<string> lines, List<QuickCommand> target, Func<string, string> toCommand, HashSet<string> usedCommands, Func<string, string>? targetSelector, ref int duplicates)
    {
        var added = 0;
        foreach (var line in lines)
        {
            if (line.Length > 80 || line.IndexOfAny(['{', '}', ';', '<', '>', '\t']) >= 0 ||
                line.Contains("http://", StringComparison.OrdinalIgnoreCase) || line.Contains("https://", StringComparison.OrdinalIgnoreCase))
                continue;

            var command = toCommand(line);
            var targetValue = targetSelector?.Invoke(line);
            var isDuplicateTarget = targetValue is not null && target.Any(existing => existing.Target is not null && string.Equals(existing.Target, targetValue, StringComparison.OrdinalIgnoreCase));
            if (isDuplicateTarget || usedCommands.Contains(command))
            {
                duplicates++;
                continue;
            }

            target.Add(new QuickCommand { Label = line, Command = command, Source = ImportSource.Imported, Target = targetValue });
            usedCommands.Add(command);
            added++;
        }

        if (added > 0)
            target.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));
        return added;
    }

    /// collar/catalog-sync: unlike the other categories' `ImportPlainNames`, `Label` and `Command` diverge
    /// here - `Command` stays the bare alias word (what's actually sent, trigger-phrase-prefixed, in the
    /// wire tell), while `Label` carries the alias word plus its description, so the Owner sees what
    /// they're about to send without changing what actually gets sent. Category-agnostic on purpose - an
    /// alias only ever resolves against the Sub's own dictionary by its bare word, regardless of which
    /// category list the Owner's copy of it lives in, so this same helper backs Title/Outfit/Gesture/
    /// Restraint/Moodle single-action aliases and the Custom Trigger Bundle list alike.
    /// `entry.Target` is only ever non-null for a single-action Outfit/Gesture/Moodle alias (see
    /// `TargetForSingleAction`/the per-category export calls in `BuildExport`) - for every other category
    /// this always compiles to "no target to compare," so the same-target check below is inert there
    /// without needing an explicit per-category opt-out.
    private static int ImportAliasLines(IEnumerable<string> lines, List<QuickCommand> target, HashSet<string> usedCommands, ref int duplicates)
    {
        var added = 0;
        foreach (var line in lines)
        {
            if (!TryParseAliasEntry(line, out var entry))
                continue;

            var isDuplicateTarget = entry.Target is not null && target.Any(existing => existing.Target is not null && string.Equals(existing.Target, entry.Target, StringComparison.OrdinalIgnoreCase));
            if (isDuplicateTarget || usedCommands.Contains(entry.Alias))
            {
                duplicates++;
                continue;
            }

            target.Add(new QuickCommand { Label = $"{entry.Alias} — {entry.Description}", Command = entry.Alias, Source = ImportSource.Imported, Target = entry.Target });
            usedCommands.Add(entry.Alias);
            added++;
        }

        if (added > 0)
            target.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));
        return added;
    }

    private static int ImportGestureLines(IEnumerable<string> lines, List<QuickCommand> target, HashSet<string> usedCommands, ref int duplicates)
    {
        var added = 0;
        foreach (var line in lines)
        {
            if (!GestureCommand.TryParseExport(line, out var entry) || entry is null)
                continue;

            var command = $"gesture {entry.Id}";
            var isDuplicateTarget = target.Any(existing => existing.Target is not null && string.Equals(existing.Target, entry.Id, StringComparison.OrdinalIgnoreCase));
            if (isDuplicateTarget || usedCommands.Contains(command))
            {
                duplicates++;
                continue;
            }

            target.Add(new QuickCommand
            {
                Label = entry.Label,
                Command = command,
                GestureModName = entry.ModName,
                GestureGroupName = entry.GroupName,
                GestureGroupOrder = entry.GroupOrder,
                GestureOptionOrder = entry.OptionOrder,
                Source = ImportSource.Imported,
                Target = entry.Id,
            });
            usedCommands.Add(command);
            added++;
        }

        if (added > 0)
            target.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));
        return added;
    }
}
