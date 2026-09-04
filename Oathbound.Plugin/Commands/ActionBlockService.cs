using System;
using Oathbound.Plugin.Safety;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Oathbound.Plugin.Commands;

/// collar/restraints: the action-block restriction rule. Hooks ActionManager's own UseAction entry point
/// directly by its FFXIVClientStructs member-function pointer (`ActionManager.MemberFunctionPointers.
/// UseAction`) rather than a raw signature scan - the same approach GagSpeak's own action-blocking detour
/// uses (StaticDetours.UseAction.cs), and more stable than a signature since FFXIVClientStructs itself
/// tracks this pointer's layout across game patches. Movement input is untouched - only action/skill
/// execution is suppressed.
public sealed class ActionBlockService : IRestrictionEnforcer, IDisposable
{
    private readonly Hook<ActionManager.Delegates.UseAction>? useActionHook;
    private bool active;

    public unsafe ActionBlockService()
    {
        try
        {
            useActionHook = ECommons.DalamudServices.Svc.Hook.HookFromAddress<ActionManager.Delegates.UseAction>(
                (nint)ActionManager.MemberFunctionPointers.UseAction, UseActionDetour);
            IsAvailable = true;
            useActionHook.Enable();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "ActionBlockService: failed to hook ActionManager.UseAction - action-block restriction is disabled for this session.");
            IsAvailable = false;
        }
    }

    /// Fail-closed, same posture as MovementLockService.IsAvailable: if the hook could not be established,
    /// this never claims to be enforcing anything.
    public bool IsAvailable { get; }

    public void Engage()
    {
        if (IsAvailable)
            active = true;
    }

    public void Release() => active = false;

    private unsafe bool UseActionDetour(ActionManager* am, ActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted)
    {
        if (active)
            return false;

        return useActionHook!.Original(am, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
    }

    public void Dispose()
    {
        active = false;
        useActionHook?.Dispose();
    }
}
