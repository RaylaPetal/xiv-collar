using System;
using System.Linq;
using CollarSystem.Plugin.Config;
using CollarSystem.Plugin.Ipc;
using CollarSystem.Plugin.Safety;
using Glamourer.Api.Enums;

namespace CollarSystem.Plugin.Commands;

/// collar/outfit: alias-triggered wardrobe changes applied via Glamourer, including the lock/key model,
/// plus the Owner's "joker" override (ForceApply/ForceUnlock - see ChatCommandListener's reserved-keyword
/// grammar). A force-applied outfit locks out the Sub's own alias-triggered Apply/Unlock until the
/// matching ForceUnlock (or panic) releases it - the Sub set up their aliases, but a forced outfit always
/// wins over them while it's in effect. Scanning stays local-only under the chat transport (collar/
/// gesture's sibling "Catalog shared with paired Owner" requirement was removed for outfit too, in spirit
/// - see design.md's "Gesture/Wardrobe catalog stays local-scan-only" decision).
public sealed class OutfitCommand
{
    private readonly PluginConfig config;
    private readonly GlamourerIpc glamourer;
    private readonly SubRuntimeState runtimeState;

    /// How many designs the last wardrobe scan found in total, before the allowlist filter.
    public int? LastScanTotalDesigns { get; private set; }

    public OutfitCommand(PluginConfig config, GlamourerIpc glamourer, SubRuntimeState runtimeState)
    {
        this.config = config;
        this.glamourer = glamourer;
        this.runtimeState = runtimeState;
    }

    public bool Apply(OutfitAliasDefinition alias)
    {
        if (runtimeState.OutfitForceLocked)
            return false;

        var ec = glamourer.ApplyDesign(alias.DesignId, alias.Key, alias.Locked);
        if (ec != GlamourerApiEc.Success)
            return false;

        runtimeState.OutfitLockKey = alias.Locked ? alias.Key : null;
        return true;
    }

    /// Unlocks using whatever key this client itself last used to lock - see AliasBook.UnlockOutfitAlias.
    public bool Unlock()
    {
        if (runtimeState.OutfitForceLocked)
            return false;

        var ec = glamourer.Unlock(runtimeState.OutfitLockKey ?? 0);
        if (ec != GlamourerApiEc.Success)
            return false;

        runtimeState.OutfitLockKey = null;
        return true;
    }

    /// The Owner's direct override: matches `designName` against the Sub's own scanned+allowlisted
    /// catalog (case-insensitive) - the Owner never sees design IDs, only whatever name the Sub told them
    /// out of band. Always locks, with a freshly generated key the Sub never has to see or type.
    public bool ForceApply(string designName)
    {
        var design = config.WardrobeMapping.LocalDesigns.Values
            .FirstOrDefault(d => string.Equals(d.Name, designName, StringComparison.OrdinalIgnoreCase));
        if (design is null)
            return false;

        var key = (uint)Random.Shared.Next(1, int.MaxValue);
        var ec = glamourer.ApplyDesign(design.DesignId, key, locked: true);
        if (ec != GlamourerApiEc.Success)
            return false;

        runtimeState.OutfitLockKey = key;
        runtimeState.OutfitForceLocked = true;
        return true;
    }

    /// The only thing that can release a force-applied outfit besides panic.
    public bool ForceUnlock()
    {
        var ec = glamourer.Unlock(runtimeState.OutfitLockKey ?? 0);
        if (ec != GlamourerApiEc.Success)
            return false;

        runtimeState.OutfitLockKey = null;
        runtimeState.OutfitForceLocked = false;
        return true;
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
}
