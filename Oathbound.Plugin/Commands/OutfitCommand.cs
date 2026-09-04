using System;
using System.Collections.Generic;
using System.Linq;
using Oathbound.Plugin.Config;
using Oathbound.Plugin.Ipc;
using Oathbound.Plugin.Safety;
using Glamourer.Api.Enums;

namespace Oathbound.Plugin.Commands;

/// collar/outfit: alias-triggered wardrobe changes applied via Glamourer, plus the Owner's "joker"
/// override (ForceApply/ForceUnlock - see ChatCommandListener's reserved-keyword grammar). A
/// force-applied outfit locks out the Sub's own alias-triggered Apply/Unlock until the matching
/// ForceUnlock (or panic) releases it - the Sub set up their aliases, but a forced outfit always wins over
/// them while it's in effect (SubRuntimeState.OutfitForceLocked, independent of slot locking below).
/// Locking a design only locks the equipment slots that design itself changes (collar/slot-locking), via
/// SlotLockManager - never Glamourer's own actor-wide lock. Scanning stays local-only under the chat
/// transport (collar/gesture's sibling "Catalog shared with paired Owner" requirement was removed for
/// outfit too, in spirit - see design.md's "Gesture/Wardrobe catalog stays local-scan-only" decision).
public sealed class OutfitCommand
{
    private const string Owner = "Outfit";

    private readonly PluginConfig config;
    private readonly GlamourerIpc glamourer;
    private readonly SlotLockManager slotLocks;
    private readonly SubRuntimeState runtimeState;

    /// How many designs the last wardrobe scan found in total, before the allowlist filter.
    public int? LastScanTotalDesigns { get; private set; }

    public OutfitCommand(PluginConfig config, GlamourerIpc glamourer, SlotLockManager slotLocks, SubRuntimeState runtimeState)
    {
        this.config = config;
        this.glamourer = glamourer;
        this.slotLocks = slotLocks;
        this.runtimeState = runtimeState;
    }

    public (bool Success, string? Reason) Apply(OutfitAliasDefinition alias)
    {
        if (runtimeState.OutfitForceLocked)
            return (false, "the outfit is currently force-locked by your Owner.");

        return ApplyDesign(alias.DesignId, alias.DesignName, alias.Locked);
    }

    /// Releases whichever slots the currently-locked design claimed - see AliasBook.UnlockOutfitAlias.
    public bool Unlock()
    {
        if (runtimeState.OutfitForceLocked)
            return false;
        if (!slotLocks.HasLock(Owner))
            return false;

        slotLocks.Release(Owner);
        return true;
    }

    /// The Owner's direct override: matches `designName` against the Sub's own scanned+allowlisted
    /// catalog (case-insensitive) - the Owner never sees design IDs, only whatever name the Sub told them
    /// out of band. Always locks.
    public (bool Success, string? Reason) ForceApply(string designName)
    {
        var design = config.WardrobeMapping.LocalDesigns.Values
            .FirstOrDefault(d => string.Equals(d.Name, designName, StringComparison.OrdinalIgnoreCase));
        if (design is null)
            return (false, $"no wardrobe design named \"{designName}\" in your Sub's scanned catalog.");

        var (success, reason) = ApplyDesign(design.DesignId, designName, locked: true);
        if (!success)
            return (false, reason);

        runtimeState.OutfitForceLocked = true;
        return (true, null);
    }

    /// The only thing that can release a force-applied outfit besides panic.
    public bool ForceUnlock()
    {
        if (!slotLocks.HasLock(Owner))
            return false;

        slotLocks.Release(Owner);
        runtimeState.OutfitForceLocked = false;
        return true;
    }

    /// Applies a design's full look via Glamourer, then - if requested - locks exactly the equipment
    /// slots that design itself changes (`Equipment.*.Apply`, see GlamourerIpc.GetDesignEquipSlots), via
    /// SlotLockManager. Any slot the design touches that's already locked by a *different* owner (e.g. the
    /// Sub's collar holding Neck) is skipped rather than refusing the whole apply - immediately restored to
    /// its already-locked value right after Glamourer applies the design, so that slot never visibly
    /// changes and stays under its existing owner's lock, while every other slot the design changes still
    /// applies and locks normally. The design apply itself never locks through Glamourer's own state.
    private (bool Success, string? Reason) ApplyDesign(Guid designId, string designName, bool locked)
    {
        var slots = locked ? glamourer.GetDesignEquipSlots(designId) : new HashSet<ApiEquipSlot>();
        var conflicts = locked ? slotLocks.ConflictingLocks(slots, Owner) : [];

        var ec = glamourer.ApplyDesign(designId);
        if (ec != GlamourerApiEc.Success)
        {
            var reason = $"Glamourer refused the apply: {ec}.";
            Plugin.Log.Warning($"Outfit apply failed for \"{designName}\": {reason}");
            return (false, reason);
        }

        foreach (var (slot, _) in conflicts)
        {
            if (slotLocks.GetLockedValue(slot) is { } existingLock)
                glamourer.SetItemOnce(slot, existingLock.ItemId, [existingLock.Stain, existingLock.Stain2]);
        }

        if (!locked || slots.Count == 0)
            return (true, null);

        var toLock = new Dictionary<ApiEquipSlot, SlotLockValue>();
        foreach (var slot in slots.Except(conflicts.Select(c => c.Slot)))
        {
            if (glamourer.GetEquipSlotValue(slot) is { } value)
                toLock[slot] = new SlotLockValue(value.ItemId, value.Stain, value.Stain2);
        }

        if (toLock.Count > 0 && !slotLocks.TryRegisterAlreadyApplied(Owner, toLock))
            return (false, "the design applied visually, but locking its slots afterward failed unexpectedly.");

        if (conflicts.Count == 0)
            return (true, null);

        var skipped = string.Join(", ", conflicts.Select(c => $"{c.Slot} (kept {c.Owner}'s)"));
        return (true, $"Skipped {skipped} - already locked by something else.");
    }

    /// Sub-side: rescan the Sub's own Glamourer designs; an empty folder scope includes all designs. Purely
    /// local - there is no live channel to push the result anywhere; the Sub picks a design here to name
    /// an alias after in the Wardrobe tab.
    public void Rescan()
    {
        var allDesigns = glamourer.GetDesigns();
        LastScanTotalDesigns = allDesigns.Count;

        var allowlist = config.WardrobeFolderAllowlist;
        var matched = allowlist.Count == 0
            ? allDesigns
            : allDesigns.Where(d => allowlist.Any(folder => IsUnderFolder(d.FullPath, folder))).ToList();

        var entries = matched.Select(d => new WardrobeDesignEntry { DesignId = d.Id, Name = d.DisplayName });
        config.WardrobeMapping.LocalDesigns = entries.ToDictionary(e => e.DesignId);
        config.Save();
    }

    private static bool IsUnderFolder(string fullPath, string folder) =>
        fullPath.StartsWith(folder.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);

    /// collar/catalog-sync: every scanned (allowlist-filtered) design's display name, deduplicated - the
    /// same plain-name shape Settings' former "Copy names" button produced.
    public IReadOnlyList<string> ExportNames() =>
        config.WardrobeMapping.LocalDesigns.Values.Select(d => d.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
}
