using System;
using System.Collections.Generic;
using System.Linq;
using Oathbound.Plugin.Config;
using Oathbound.Plugin.Ipc;

namespace Oathbound.Plugin.Commands;

/// collar/moodles: the Owner's direct override for applying/clearing a Moodle. No alias dictionary
/// involved at all (unlike title/outfit/gesture, which have both an alias path and a force-override path)
/// - Moodles only ever exist as this one reserved-keyword command, matched by name against the Sub's own
/// scanned status catalog. Applies/clears immediately, no confirmation queue - see design.md's "immediate,
/// no confirmation gate" decision.
public sealed class MoodlesCommand
{
    private readonly PluginConfig config;
    private readonly MoodlesIpc moodles;

    /// How many statuses the last scan found - so the UI can say "found N" even before anything is picked.
    public int? LastScanTotalStatuses { get; private set; }
    public MoodlesScanStatus? LastScanStatus { get; private set; }
    public string? LastScanError { get; private set; }

    public MoodlesCommand(PluginConfig config, MoodlesIpc moodles)
    {
        this.config = config;
        this.moodles = moodles;
    }

    /// Sub-side: rescan the Sub's own registered Moodles statuses (buffs/debuffs), not bundled presets -
    /// collar/moodles wants the Owner commanding an individual status. No folder allowlist - Moodles
    /// statuses have no folder-organization concept, every registered status is eligible (design.md's
    /// "mirrors GestureMapping's shape" decision, minus the allowlist).
    public void Rescan()
    {
        var result = moodles.GetOwnStatuses();
        LastScanStatus = result.Status;
        LastScanError = result.Error;
        if (result.Status != MoodlesScanStatus.Success)
            return;

        LastScanTotalStatuses = result.Statuses.Count;
        config.MoodlesMapping.LocalCatalog = result.Statuses
            .Select(s => new MoodlesStatusEntry { StatusId = s.Id.ToString(), Name = s.Name })
            .ToDictionary(e => e.StatusId);
        config.Save();
    }

    /// The Owner's direct override: matches `statusName` against the Sub's own scanned catalog
    /// (case-insensitive) - the Owner never sees status GUIDs, only whatever name the Sub told them out of
    /// band, same pattern as OutfitCommand.ForceApply/GestureCommand.ForceApply.
    public bool ForceApply(string statusName)
    {
        if (CommandSelector.TryRead(statusName, out var selector, out var tail) && tail.Length == 0) statusName = selector;
        var entry = CommandSelector.ResolveMoodle(config.MoodlesMapping.LocalCatalog.Values, statusName);
        if (entry is null || !Guid.TryParse(entry.StatusId, out var statusId))
            return false;

        return moodles.ApplyStatus(statusId);
    }

    public bool ForceClear() => moodles.ClearStatus();

    /// collar/moodles "Sub can self-apply or self-clear a Moodle via alias": looks up by `StatusId` first
    /// (LocalCatalog is already keyed by it), falling back to a name match the same way `ForceApply` does,
    /// so an alias created before a rescan renamed/removed its target status still has a chance to resolve.
    /// Thin wrapper - the underlying apply logic is entirely `ForceApply`'s, never duplicated.
    public bool Apply(MoodlesAliasDefinition alias)
    {
        if (!string.IsNullOrEmpty(alias.StatusId) && config.MoodlesMapping.LocalCatalog.TryGetValue(alias.StatusId, out var exact))
            return Guid.TryParse(exact.StatusId, out var statusId) && moodles.ApplyStatus(statusId);

        return ForceApply(alias.StatusName);
    }

    /// collar/moodles "Sub can self-apply or self-clear a Moodle via alias": thin wrapper over `ForceClear`.
    public bool Clear() => ForceClear();

    /// collar/catalog-sync: every scanned status's display name, deduplicated - the same plain-name shape
    /// Settings' former "Copy names" button produced.
    public IReadOnlyList<string> ExportNames() =>
        config.MoodlesMapping.LocalCatalog.Values.Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
}
