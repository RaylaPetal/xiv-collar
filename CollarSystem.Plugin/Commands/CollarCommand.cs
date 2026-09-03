using System;
using CollarSystem.Plugin.Config;
using CollarSystem.Plugin.Ipc;
using CollarSystem.Plugin.Safety;
using Glamourer.Api.Enums;

namespace CollarSystem.Plugin.Commands;

/// collar/collaring: the Sub's configured Neck-slot collar, applied and locked automatically at pairing
/// acceptance (see PairingCommand.AcceptPeer). No `ForceApply(name)` taking Owner input the way Outfit/
/// Title do - there's only ever one configured collar item, so the Owner's `collar lock` override needs no
/// argument, it just (re)applies whatever the Sub already configured. Same force-lock shape as
/// OutfitCommand otherwise: a freshly generated key on a fresh lock, an unconditional panic release, and a
/// dedicated non-panic release path (`collar unlock`).
public sealed class CollarCommand
{
    private readonly PluginConfig config;
    private readonly GlamourerIpc glamourer;
    private readonly SubRuntimeState runtimeState;

    public CollarCommand(PluginConfig config, GlamourerIpc glamourer, SubRuntimeState runtimeState)
    {
        this.config = config;
        this.glamourer = glamourer;
        this.runtimeState = runtimeState;
    }

    /// Captures whatever the Sub currently has equipped in their Neck slot as their configured collar -
    /// collar/collaring's "Sub configures their own collar item." Refuses while a collar lock is active,
    /// so the configured item can't be swapped out from under an already-applied lock.
    public bool CaptureCurrentAsCollar()
    {
        if (runtimeState.CollarForceLocked)
            return false;

        var current = glamourer.GetCurrentNeckItem();
        if (current is null)
            return false;

        config.Collar.ItemId = current.Value.ItemId;
        config.Collar.Stain = current.Value.Stain;
        config.Collar.Stain2 = current.Value.Stain2;
        config.Save();
        return true;
    }

    public void ClearConfiguredCollar()
    {
        if (runtimeState.CollarForceLocked)
            return;

        config.Collar.ItemId = null;
        config.Collar.Stain = 0;
        config.Collar.Stain2 = 0;
        config.Save();
    }

    /// Applies and locks the Sub's configured collar item - called automatically from pairing acceptance
    /// (collar/pairing's "Accepting a pairing request applies a configured collar"), and also directly via
    /// the Owner's `collar lock` override (e.g. to re-attach it after `collar unlock`, or to apply it for
    /// the first time if it wasn't configured/enabled yet when pairing was accepted). Reuses the existing
    /// lock key if already locked (Glamourer needs the correct current key to modify an already-locked
    /// slot - a brand new random key would just be rejected), otherwise generates a fresh one the Owner
    /// never sees, same precedent as OutfitCommand.ForceApply.
    public bool ForceApply()
    {
        if (!config.Collar.IsConfigured)
            return false;

        var stains = new byte[] { config.Collar.Stain, config.Collar.Stain2 };
        var key = runtimeState.CollarForceLocked ? runtimeState.CollarLockKey ?? 0 : (uint)Random.Shared.Next(1, int.MaxValue);
        var ec = glamourer.SetItem(ApiEquipSlot.Neck, config.Collar.ItemId!.Value, stains, key, locked: true);
        if (ec != GlamourerApiEc.Success)
            return false;

        runtimeState.CollarLockKey = key;
        runtimeState.CollarForceLocked = true;
        return true;
    }

    /// The Owner's `collar unlock` override - the only way to release a locked collar besides panic.
    public bool ForceUnlock()
    {
        var ec = glamourer.Unlock(runtimeState.CollarLockKey ?? 0);
        if (ec != GlamourerApiEc.Success)
            return false;

        runtimeState.CollarLockKey = null;
        runtimeState.CollarForceLocked = false;
        return true;
    }
}
