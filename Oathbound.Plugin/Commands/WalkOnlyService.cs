using Oathbound.Plugin.Safety;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace Oathbound.Plugin.Commands;

/// collar/restraints: the walk-only restriction rule. Unlike MovementLockService (which hooks the game's
/// input-polling functions), forcing walk-only needs no signature hook at all - `Control.Instance()->
/// IsWalking` is a plain, directly-writable FFXIVClientStructs field (the same one GagSpeak's own
/// MovementController.ForceWalking sets), so this is just a per-frame poll-and-correct, run from
/// Plugin.OnFrameworkUpdate alongside GestureCommand's own per-frame work. Directional movement input
/// itself is untouched - only the walk/run state is forced, so the Sub still moves, just never runs.
public sealed class WalkOnlyService : IRestrictionEnforcer
{
    private bool active;
    private bool wasWalking;

    public bool IsAvailable => SprintInterceptorAvailable;
    public bool SprintInterceptorAvailable { get; set; }
    public bool IsActive => active;

    public unsafe void Engage()
    {
        var control = Control.Instance();
        wasWalking = control != null && control->IsWalking;
        active = true;
    }
    public unsafe void Release()
    {
        active = false;
        var control = Control.Instance();
        if (control != null && !wasWalking)
        {
            control->IsWalking = false;
            control->IsWalkingDuringAutorun = false;
        }
    }

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
