using System;
using CollarSystem.Plugin.Commands;
using CollarSystem.Plugin.Ipc;

namespace CollarSystem.Plugin.Safety;

/// The Sub's always-available panic/safeword (collar/pairing). Every step uses only local state and
/// local IPC calls - EndPairingLocally only closes the local socket, never waits on the relay - so this
/// completes correctly even with no network connection at all, satisfying the "relay down" scenario.
/// Each step is isolated in its own try/catch so one failure (an IPC call throwing, say) never stops the
/// rest of the sequence from running.
public sealed class PanicHandler
{
    private readonly PairingCommand pairing;
    private readonly GlamourerIpc glamourer;
    private readonly HonorificIpc honorific;
    private readonly MovementLockService movementLock;
    private readonly SubRuntimeState runtimeState;

    public PanicHandler(PairingCommand pairing, GlamourerIpc glamourer, HonorificIpc honorific, MovementLockService movementLock, SubRuntimeState runtimeState)
    {
        this.pairing = pairing;
        this.glamourer = glamourer;
        this.honorific = honorific;
        this.movementLock = movementLock;
        this.runtimeState = runtimeState;
    }

    public void Panic()
    {
        RunStep("unpair", () => pairing.EndPairingLocally());

        // Glamourer only trusts the key that locked a state - pass back whatever this client itself
        // last used to apply the lock (see GlamourerIpc.Revert and SubRuntimeState's remarks).
        RunStep("revert outfit", () => glamourer.Revert(runtimeState.OutfitLockKey ?? 0));

        RunStep("clear title", () =>
        {
            if (runtimeState.TitleApplied)
                honorific.ClearTitle();
        });

        RunStep("release movement lock", () => movementLock.Release());

        runtimeState.Reset();
        Plugin.Log.Information("Panic triggered: unpaired, outfit reverted, title cleared, movement lock released.");
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
