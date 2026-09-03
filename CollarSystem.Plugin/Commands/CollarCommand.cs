using System.Collections.Generic;
using CollarSystem.Plugin.Config;
using CollarSystem.Plugin.Ipc;
using CollarSystem.Plugin.Safety;
using Glamourer.Api.Enums;

namespace CollarSystem.Plugin.Commands;

/// collar/collaring: the Sub's configured Neck-slot collar, applied and locked automatically at pairing
/// acceptance (see PairingCommand.AcceptPeer). No `ForceApply(name)` taking Owner input the way Outfit/
/// Title do - there's only ever one configured collar item, so the Owner's `collar lock` override needs no
/// argument, it just (re)applies whatever the Sub already configured. Locks only the Neck slot via
/// SlotLockManager (collar/slot-locking) - never Glamourer's own actor-wide lock.
public sealed class CollarCommand
{
    private const string Owner = "Collar";

    private readonly PluginConfig config;
    private readonly GlamourerIpc glamourer;
    private readonly SlotLockManager slotLocks;
    private readonly SubRuntimeState runtimeState;

    public CollarCommand(PluginConfig config, GlamourerIpc glamourer, SlotLockManager slotLocks, SubRuntimeState runtimeState)
    {
        this.config = config;
        this.glamourer = glamourer;
        this.slotLocks = slotLocks;
        this.runtimeState = runtimeState;
    }

    /// Captures whatever the Sub currently has equipped in their Neck slot as their configured collar -
    /// collar/collaring's "Sub configures their own collar item." Refuses while a collar lock is active,
    /// so the configured item can't be swapped out from under an already-applied lock.
    public bool CaptureCurrentAsCollar()
    {
        if (slotLocks.HasLock(Owner))
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
        if (slotLocks.HasLock(Owner))
            return;

        config.Collar.ItemId = null;
        config.Collar.Stain = 0;
        config.Collar.Stain2 = 0;
        config.Save();
    }

    /// Applies and locks the Sub's configured collar item to the Neck slot only - called automatically
    /// from pairing acceptance (collar/pairing's "Accepting a pairing request applies a configured
    /// collar"), and also directly via the Owner's `collar lock` override (e.g. to re-attach it after
    /// `collar unlock`, or to apply it for the first time if it wasn't configured/enabled yet when pairing
    /// was accepted).
    public bool ForceApply()
    {
        if (!config.Collar.IsConfigured)
            return false;

        var value = new SlotLockValue(config.Collar.ItemId!.Value, config.Collar.Stain, config.Collar.Stain2);
        if (!slotLocks.TryLock(Owner, new Dictionary<ApiEquipSlot, SlotLockValue> { [ApiEquipSlot.Neck] = value }))
            return false;

        runtimeState.CollarForceLocked = true;
        return true;
    }

    /// The Owner's `collar unlock` override - the only way to release a locked collar besides panic.
    public bool ForceUnlock()
    {
        if (!slotLocks.HasLock(Owner))
            return false;

        slotLocks.Release(Owner);
        runtimeState.CollarForceLocked = false;
        return true;
    }
}
