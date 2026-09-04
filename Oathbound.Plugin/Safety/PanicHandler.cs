using System;
using Oathbound.Plugin.Commands;
using Oathbound.Plugin.Config;
using Oathbound.Plugin.Ipc;

namespace Oathbound.Plugin.Safety;

/// The always-available panic/safeword (collar/pairing) - either side can trigger it, not just the Sub.
/// Every step but the first uses only local state and local IPC calls - EndPairingLocally only flips a
/// local config flag, nothing to wait on. Each step is isolated in its own try/catch so one failure (an
/// IPC call throwing, a send failing) never stops the rest of the sequence from running. Unlike a normal
/// single-category slot-lock release (SlotLockManager.Release's snapshot/restore dance), panic is a full
/// teardown by design - one unconditional whole-actor revert, then simply dropping every tracked lock,
/// since nothing needs preserving when everything is being reverted anyway (design.md: "Panic keeps a
/// single, unconditional whole-actor revert"). The one exception to "only local state": the first step
/// sends a single best-effort notification tell to the cached peer (collar/pairing "Panic notifies the
/// peer, best-effort") - the second of this plugin's two narrow exceptions to "no automated sending"
/// (collar/chat-transport), isolated in its own RunStep so a send failure can never affect anything else
/// panic guarantees.
public sealed class PanicHandler
{
    private readonly PairingCommand pairing;
    private readonly PluginConfig config;
    private readonly ChatComposer composer;
    private readonly ChatSender sender;
    private readonly GlamourerIpc glamourer;
    private readonly SlotLockManager slotLocks;
    private readonly HonorificIpc honorific;
    private readonly MovementLockService movementLock;
    private readonly RestrictionRuleManager restrictionRules;
    private readonly RestraintCommand restraints;
    private readonly SubRuntimeState runtimeState;
    private readonly CollarCommand collar;

    public PanicHandler(PairingCommand pairing, PluginConfig config, ChatComposer composer, ChatSender sender, GlamourerIpc glamourer, SlotLockManager slotLocks, HonorificIpc honorific, MovementLockService movementLock, RestrictionRuleManager restrictionRules, RestraintCommand restraints, SubRuntimeState runtimeState, CollarCommand collar)
    {
        this.pairing = pairing;
        this.config = config;
        this.composer = composer;
        this.sender = sender;
        this.glamourer = glamourer;
        this.slotLocks = slotLocks;
        this.honorific = honorific;
        this.movementLock = movementLock;
        this.restrictionRules = restrictionRules;
        this.restraints = restraints;
        this.runtimeState = runtimeState;
        this.collar = collar;
    }

    public void Panic()
    {
        // collar/pairing "Panic notifies the peer, best-effort": captured before any other step runs, so a
        // future change to what EndPairingLocally clears can't silently break this - and isolated in its
        // own RunStep so a send failure (offline peer, network down) never affects any other panic step.
        var peerName = config.Pairing.PeerName;
        var peerWorld = config.Pairing.PeerWorld;
        RunStep("notify peer", () =>
        {
            if (!string.IsNullOrWhiteSpace(peerName) && !string.IsNullOrWhiteSpace(peerWorld))
                sender.Send(composer.ComposeUnpairNotice(peerName, peerWorld));
        });

        RunStep("unpair", () => pairing.EndPairingLocally());

        RunStep("revert outfit/collar", () => glamourer.RevertToAutomationFull());
        RunStep("release slot locks", slotLocks.ReleaseAllForPanic);
        RunStep("clear collar moodle", collar.PanicRelease);

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
