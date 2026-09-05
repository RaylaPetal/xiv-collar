using System;
using System.Threading;
using Oathbound.Plugin.Commands;
using Oathbound.Plugin.Config;
using Oathbound.Plugin.Ipc;
using Oathbound.Plugin.Relay;

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
    private readonly PairingService pairing;
    private readonly RevocationService revocation;
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

    public PanicHandler(PairingService pairing, RevocationService revocation, PluginConfig config, ChatComposer composer, ChatSender sender, GlamourerIpc glamourer, SlotLockManager slotLocks, HonorificIpc honorific, MovementLockService movementLock, RestrictionRuleManager restrictionRules, RestraintCommand restraints, SubRuntimeState runtimeState, CollarCommand collar)
    {
        this.pairing = pairing;
        this.revocation = revocation;
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

    /// collar/pairing "Unpair and panic publish authenticated revocation": every local, synchronous
    /// teardown step runs and completes *before* either notification is even attempted - a network attempt
    /// (tell send, relay publish) can never delay or skip a local safety step, and both notifications are
    /// independent best-effort additions on top of teardown that has already fully happened.
    public void Panic()
    {
        // Snapshot notification data first: EndPairingLocally only flips Paired (never clears these), but
        // capturing them before touching anything at all keeps this immune to any future change in what
        // local teardown clears.
        var peerName = config.Pairing.PeerName;
        var peerWorld = config.Pairing.PeerWorld;
        var pairIdHash = config.Pairing.PairIdHash;
        var pairEpoch = config.Pairing.PairEpoch;

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

        // Everything above is already done, unconditionally, by this point. Only now are the two
        // best-effort notifications attempted, independently of each other and of everything above.
        RunStep("notify peer (tell)", () =>
        {
            if (!string.IsNullOrWhiteSpace(peerName) && !string.IsNullOrWhiteSpace(peerWorld))
                sender.Send(composer.ComposeUnpairNotice(peerName, peerWorld));
        });

        if (pairIdHash is not null)
            Plugin.FireAndForget(revocation.PublishBestEffortAsync(pairIdHash, pairEpoch, "panic", CancellationToken.None));
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
