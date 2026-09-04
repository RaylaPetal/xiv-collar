using System;
using CollarSystem.Plugin.Commands;
using CollarSystem.Plugin.Ipc;

namespace CollarSystem.Plugin.Safety;

/// The Sub's always-available panic/safeword (collar/pairing). Every step uses only local state and
/// local IPC calls - EndPairingLocally only flips a local config flag, nothing to wait on. Each step is
/// isolated in its own try/catch so one failure (an IPC call throwing, say) never stops the rest of the
/// sequence from running. Unlike a normal single-category slot-lock release (SlotLockManager.Release's
/// snapshot/restore dance), panic is a full teardown by design - one unconditional whole-actor revert,
/// then simply dropping every tracked lock, since nothing needs preserving when everything is being
/// reverted anyway (design.md: "Panic keeps a single, unconditional whole-actor revert").
public sealed class PanicHandler
{
    private readonly PairingCommand pairing;
    private readonly GlamourerIpc glamourer;
    private readonly SlotLockManager slotLocks;
    private readonly HonorificIpc honorific;
    private readonly MovementLockService movementLock;
    private readonly RestrictionRuleManager restrictionRules;
    private readonly RestraintCommand restraints;
    private readonly SubRuntimeState runtimeState;

    public PanicHandler(PairingCommand pairing, GlamourerIpc glamourer, SlotLockManager slotLocks, HonorificIpc honorific, MovementLockService movementLock, RestrictionRuleManager restrictionRules, RestraintCommand restraints, SubRuntimeState runtimeState)
    {
        this.pairing = pairing;
        this.glamourer = glamourer;
        this.slotLocks = slotLocks;
        this.honorific = honorific;
        this.movementLock = movementLock;
        this.restrictionRules = restrictionRules;
        this.restraints = restraints;
        this.runtimeState = runtimeState;
    }

    public void Panic()
    {
        RunStep("unpair", () => pairing.EndPairingLocally());

        RunStep("revert outfit/collar", () => glamourer.RevertToAutomationFull());
        RunStep("release slot locks", slotLocks.ReleaseAllForPanic);

        RunStep("clear title", () =>
        {
            if (runtimeState.TitleApplied)
                honorific.ClearTitle();
        });

        RunStep("release movement lock", movementLock.ReleaseAll);
        RunStep("release restriction rules", restrictionRules.ReleaseAllForPanic);
        RunStep("release restraint bound animations", restraints.ReleaseAllBoundAnimationsForPanic);

        runtimeState.Reset();
        Plugin.Log.Information("Panic triggered: unpaired, outfit/collar reverted, title cleared, movement lock released, all slot locks and restriction rules released.");
    }

    private static void RunStep(string name, Action step)
    {
        try
        {
            step();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Panic step '{name}' failed - continuing with remaining steps.");
        }
    }
}
