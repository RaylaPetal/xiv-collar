using System;
using System.Collections.Generic;
using System.Linq;
using Oathbound.Plugin.Config;
using Oathbound.Plugin.Ipc;
using Oathbound.Plugin.Safety;

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
    private readonly CatalogStore catalogStore;

    /// collar/attached-moodles: every apply/remove this class (and every attached-moodle source) does goes
    /// through here, so removal is always one status at a time and respects other holders.
    public AttachedMoodleLedger Ledger { get; }

    /// How many statuses the last scan found - so the UI can say "found N" even before anything is picked.
    public int? LastScanTotalStatuses { get; private set; }
    public MoodlesScanStatus? LastScanStatus { get; private set; }
    public string? LastScanError { get; private set; }

    public MoodlesCommand(PluginConfig config, MoodlesIpc moodles, CatalogStore catalogStore, AttachedMoodleLedger ledger)
    {
        this.config = config;
        this.moodles = moodles;
        this.catalogStore = catalogStore;
        Ledger = ledger;
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
        catalogStore.Save(config);
    }

    /// The Owner's direct override: matches `statusName` against the Sub's own scanned catalog
    /// (case-insensitive) - the Owner never sees status GUIDs, only whatever name the Sub told them out of
    /// band, same pattern as OutfitCommand.ForceApply/GestureCommand.ForceApply.
    public bool ForceApply(string statusName) =>
        TryResolveStatusId(statusName, out var statusId) && Ledger.Hold(AttachedMoodleLedger.ManualSource(statusId), statusId);

    /// Resolves a status name (or `"name" #hash` selector) against the Sub's own scanned catalog.
    public bool TryResolveStatusId(string statusName, out Guid statusId)
    {
        statusId = Guid.Empty;
        if (CommandSelector.TryRead(statusName, out var selector, out var tail) && tail.Length == 0) statusName = selector;
        var entry = CommandSelector.ResolveMoodle(config.MoodlesMapping.LocalCatalog.Values, statusName);
        return entry is not null && Guid.TryParse(entry.StatusId, out statusId);
    }

    /// collar/moodles clear, reworked by collar/attached-moodles "Owner's moodle clear leaves held attached
    /// moodles in place": clears the Sub's moodles except those an active outfit/restraint/leash/collar holds.
    public bool ForceClear() => Ledger.ClearUnheld();

    /// collar/attached-moodles "Attached moodle is applied with its action": holds, for `source`, the Owner's
    /// `moodle:` override when one was sent, the Sub's Moodles permission allows it, and it names a status in
    /// the Sub's library - otherwise the Sub's own default. With neither, releases whatever `source` held (an
    /// outfit swapped for one with no moodle drops the previous outfit's). The Sub's default needs no Moodles
    /// permission of its own: the Sub picked it, and the action itself was already permission-gated.
    public void HoldAttached(string source, AttachedMoodleRef? subDefault, string? ownerOverride)
    {
        Guid? statusId = null;
        if (!string.IsNullOrWhiteSpace(ownerOverride))
        {
            if (!config.Permissions.Moodles)
                Plugin.Log.Information($"Attached moodle override \"{ownerOverride}\" ignored for {source}: Moodles permission is off.");
            else if (TryResolveStatusId(ownerOverride, out var overrideId))
                statusId = overrideId;
            else
                Plugin.Log.Warning($"Attached moodle override \"{ownerOverride}\" for {source} matched no Moodles status - using the default.");
        }

        if (statusId is null && subDefault is { } fallback && fallback.StatusId != Guid.Empty)
            statusId = fallback.StatusId;

        if (statusId is { } id)
            Ledger.Hold(source, id);
        else
            Ledger.Release(source);
    }

    /// collar/moodles "Sub can self-apply or self-clear a Moodle via alias": looks up by `StatusId` first
    /// (LocalCatalog is already keyed by it), falling back to a name match the same way `ForceApply` does,
    /// so an alias created before a rescan renamed/removed its target status still has a chance to resolve.
    /// Thin wrapper - the underlying apply logic is entirely `ForceApply`'s, never duplicated.
    public bool Apply(MoodlesAliasDefinition alias)
    {
        if (!string.IsNullOrEmpty(alias.StatusId) && config.MoodlesMapping.LocalCatalog.TryGetValue(alias.StatusId, out var exact))
            return Guid.TryParse(exact.StatusId, out var statusId) && Ledger.Hold(AttachedMoodleLedger.ManualSource(statusId), statusId);

        return ForceApply(alias.StatusName);
    }

    /// collar/moodles "Sub can self-apply or self-clear a Moodle via alias": thin wrapper over `ForceClear`.
    public bool Clear() => ForceClear();

    /// collar/catalog-sync: every scanned status's display name, deduplicated - the same plain-name shape
    /// Settings' former "Copy names" button produced.
    public IReadOnlyList<string> ExportNames() =>
        config.MoodlesMapping.LocalCatalog.Values.Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
}
