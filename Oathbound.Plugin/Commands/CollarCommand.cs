using System;
using System.Collections.Generic;
using Oathbound.Plugin.Config;
using Oathbound.Plugin.Safety;
using Glamourer.Api.Enums;

namespace Oathbound.Plugin.Commands;

/// collar/collaring: the Sub's configured Neck-slot collar, applied and locked automatically at pairing
/// acceptance (see PairingCommand.AcceptPeer). No `ForceApply(name)` taking Owner input the way Outfit/
/// Title do - there's only ever one configured collar item, so the Owner's `collar lock` override needs no
/// argument, it just (re)applies whatever the Sub already configured. Locks only the Neck slot via
/// SlotLockManager (collar/slot-locking) - never Glamourer's own actor-wide lock.
public sealed class CollarCommand
{
    private const string Owner = "Collar";

    /// How often the assigned collar Moodle is re-applied while the collar is locked (design.md's
    /// timer-based reassertion, not a Moodles change-notification event - see design.md's Decisions for
    /// why). `Environment.TickCount64`-based, matching GestureCommand.OnFrameworkUpdate's own timing style.
    private const long MoodleReassertIntervalMs = 10_000;

    private readonly PluginConfig config;
    private readonly SlotLockManager slotLocks;
    private readonly SubRuntimeState runtimeState;
    private readonly MoodlesCommand moodles;

    private long nextMoodleReassertTicks;

    public CollarCommand(PluginConfig config, SlotLockManager slotLocks, SubRuntimeState runtimeState, MoodlesCommand moodles)
    {
        this.config = config;
        this.slotLocks = slotLocks;
        this.runtimeState = runtimeState;
        this.moodles = moodles;
    }

    /// Advanced from Plugin.OnFrameworkUpdate - re-applies the collar's assigned Moodle on an interval for
    /// as long as the Sub is paired with "Collar" permission enabled, independent of whether the collar's
    /// Neck-slot lock itself is active, so removing it through Moodles' own UI (or unlocking the collar)
    /// doesn't stick (collar/collaring "Manually removing the assigned Moodle does not stick while
    /// unlocked but still paired"). A no-op whenever unpaired, the permission is off, or no Moodle is
    /// assigned.
    public void OnFrameworkUpdate()
    {
        if (!config.Pairing.IsPaired || !config.Permissions.Collar || !config.Collar.HasMoodleAssigned)
            return;

        var now = Environment.TickCount64;
        if (now < nextMoodleReassertTicks)
            return;

        ApplyAssignedMoodle();
    }

    private void ApplyAssignedMoodle()
    {
        moodles.Apply(new MoodlesAliasDefinition { StatusId = config.Collar.MoodleStatusId!, StatusName = config.Collar.MoodleStatusName! });
        nextMoodleReassertTicks = Environment.TickCount64 + MoodleReassertIntervalMs;
    }

    /// Saves an item picked from the Neck-locked `ItemPickerWindow` as the Sub's configured collar -
    /// collar/collaring's "Sub configures their own collar item." Mirrors `RestraintCommand.
    /// CaptureDeviceFromItem`'s shape - no Glamourer read, undyed (stain 0/0). Refuses while a collar lock
    /// is active, so the configured item can't be swapped out from under an already-applied lock.
    public bool ConfigureFromItem(ulong itemId)
    {
        if (slotLocks.HasLock(Owner))
            return false;

        config.Collar.ItemId = itemId;
        config.Collar.Stain = 0;
        config.Collar.Stain2 = 0;
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
    /// was accepted). If a Moodle is assigned, it applies alongside the item and its periodic
    /// re-assertion (OnFrameworkUpdate) begins - collar/collaring "Assigned Moodle applies alongside the
    /// collar at acceptance"/"Owner's re-lock also resumes the assigned Moodle".
    public bool ForceApply()
    {
        if (!config.Collar.IsConfigured)
            return false;

        var value = new SlotLockValue(config.Collar.ItemId!.Value, config.Collar.Stain, config.Collar.Stain2);
        if (!slotLocks.TryLock(Owner, new Dictionary<ApiEquipSlot, SlotLockValue> { [ApiEquipSlot.Neck] = value }))
            return false;

        runtimeState.CollarForceLocked = true;
        if (config.Collar.HasMoodleAssigned)
            ApplyAssignedMoodle();

        return true;
    }

    /// The Owner's `collar unlock` override - releases only the Neck-slot lock. The assigned Moodle (if
    /// any) is untouched and keeps being periodically re-asserted, since it now tracks the pairing rather
    /// than the lock - collar/collaring "Owner's release also clears the assigned Moodle" (no longer true;
    /// see that scenario's updated body).
    public bool ForceUnlock()
    {
        if (!slotLocks.HasLock(Owner))
            return false;

        slotLocks.Release(Owner);
        runtimeState.CollarForceLocked = false;

        return true;
    }

    /// Called when a pairing ends for any reason other than panic - the Sub's own release or a verified
    /// peer notice (PairingService.ReleasePeer / EndFromVerifiedPeerNotice) - collar/collaring "Unpairing
    /// releases the collar and clears the assigned Moodle". Releases the Neck-slot lock if one is held, and
    /// clears the assigned Moodle (if any) unconditionally rather than gating on the lock, since the Moodle
    /// can now be actively reasserting while paired but unlocked. A no-op on both fronts when there's
    /// nothing to release.
    public void ReleaseOnUnpair()
    {
        if (slotLocks.HasLock(Owner))
        {
            slotLocks.Release(Owner);
            runtimeState.CollarForceLocked = false;
        }

        if (config.Collar.HasMoodleAssigned)
            moodles.Clear();
    }

    /// Panic's own release path (called from PanicHandler, not from `slotLocks.ReleaseAllForPanic` which
    /// only knows about slots, not Moodles) - clears the assigned Moodle and stops its re-assertion,
    /// unconditionally, the same as the collar's own Neck-slot lock always releases on panic. Gated only on
    /// whether a Moodle is assigned, not on `CollarForceLocked` - the Moodle now reasserts independently of
    /// the Neck-slot lock (while paired), so a paired-but-unlocked Sub can still have it actively applied
    /// and must have it cleared on panic too. A no-op when no Moodle is assigned.
    public void PanicRelease()
    {
        if (config.Collar.HasMoodleAssigned)
            moodles.Clear();
    }
}
