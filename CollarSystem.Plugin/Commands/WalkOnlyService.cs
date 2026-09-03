using CollarSystem.Plugin.Safety;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace CollarSystem.Plugin.Commands;

/// collar/restraints: the walk-only restriction rule. Unlike MovementLockService (which hooks the game's
/// input-polling functions), forcing walk-only needs no signature hook at all - `Control.Instance()->
/// IsWalking` is a plain, directly-writable FFXIVClientStructs field (the same one GagSpeak's own
/// MovementController.ForceWalking sets), so this is just a per-frame poll-and-correct, run from
/// Plugin.OnFrameworkUpdate alongside GestureCommand's own per-frame work. Directional movement input
/// itself is untouched - only the walk/run state is forced, so the Sub still moves, just never runs.
public sealed class WalkOnlyService : IRestrictionEnforcer
{
    private bool active;

    public void Engage() => active = true;
    public void Release() => active = false;

    public unsafe void OnFrameworkUpdate()
    {
        if (!active)
            return;

        var control = Control.Instance();
        if (control == null)
            return;
        if (control->IsWalking && control->IsWalkingDuringAutorun)
            return;

        control->IsWalking = true;
        control->IsWalkingDuringAutorun = true;
    }
}
