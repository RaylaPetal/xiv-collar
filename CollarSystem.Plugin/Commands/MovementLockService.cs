using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.System.Input;

namespace CollarSystem.Plugin.Commands;

#pragma warning disable CS0649 // assigned via reflection by Svc.Hook.InitializeFromAttributes, not by the compiler

/// The actual leash mechanism for collar/follow: suppresses movement input at the same low-level input-
/// polling functions the game itself reads, for both direct movement keys and whatever internal logic
/// decides to cancel an active follow/auto-move. Isolated into its own module per design.md's risk-tier
/// decision - a broken signature after a game patch degrades only this module, and IsAvailable lets the
/// rest of the plugin fail closed (task 7.5) rather than leaving a lock silently unenforced.
///
/// Hook target signatures are the same, actively-maintained ones GagSpeak uses for this exact purpose
/// (Project-GagSpeak/client, ProjectGagSpeak/GameInternals/Signatures.cs) - per design.md's explicit
/// recommendation to build on GagSpeak's proven approach rather than re-deriving new signatures.
public sealed unsafe class MovementLockService : IDisposable
{
    private static readonly InputId[] MovementInputs =
    [
        InputId.MOVE_FORE, InputId.MOVE_BACK, InputId.MOVE_STRIFE_L, InputId.MOVE_STRIFE_R,
        InputId.MOVE_LEFT, InputId.MOVE_RIGHT, InputId.MOVE_AND_STEER,
    ];

    private const string SigIsInputIdPressed = "E8 ?? ?? ?? ?? 84 C0 74 ?? 8D 93";
    private const string SigIsInputIdDown = "E8 ?? ?? ?? ?? 48 8B 75 ?? BB";
    private const string SigIsInputIdHeld = "E8 ?? ?? ?? ?? 84 C0 74 ?? EB ?? BE";

    public unsafe delegate byte IsInputIdDelegate(void* unk, InputId inputId);

    [Signature(SigIsInputIdPressed, DetourName = nameof(IsInputIdPressedDetour), Fallibility = Fallibility.Auto)]
    private readonly Hook<IsInputIdDelegate>? isInputIdPressedHook;

    [Signature(SigIsInputIdDown, DetourName = nameof(IsInputIdDownDetour), Fallibility = Fallibility.Auto)]
    private readonly Hook<IsInputIdDelegate>? isInputIdDownHook;

    [Signature(SigIsInputIdHeld, DetourName = nameof(IsInputIdHeldDetour), Fallibility = Fallibility.Auto)]
    private readonly Hook<IsInputIdDelegate>? isInputIdHeldHook;

    private bool locked;

    public MovementLockService()
    {
        Svc.Hook.InitializeFromAttributes(this);

        // task 7.5: fail closed. If any signature didn't resolve on this game version, never claim the
        // lock works - IsAvailable stays false and FollowCommand must refuse to engage it.
        IsAvailable = isInputIdPressedHook is not null && isInputIdDownHook is not null && isInputIdHeldHook is not null;

        if (IsAvailable)
        {
            isInputIdPressedHook!.Enable();
            isInputIdDownHook!.Enable();
            isInputIdHeldHook!.Enable();
        }
        else
        {
            Plugin.Log.Error("MovementLockService: one or more input hooks failed to resolve - movement lock is disabled for this session.");
        }
    }

    public bool IsAvailable { get; }

    public bool IsLocked => IsAvailable && locked;

    /// collar/follow: "Movement lock releases on panic, unpair, or Owner release" - all three paths call
    /// this, and it is safe to call even if IsAvailable is false (locked just never becomes true).
    public void Engage() => locked = IsAvailable;

    public void Release() => locked = false;

    private byte IsInputIdPressedDetour(void* unk, InputId inputId) => Suppress(inputId) ? (byte)0 : isInputIdPressedHook!.Original(unk, inputId);
    private byte IsInputIdDownDetour(void* unk, InputId inputId) => Suppress(inputId) ? (byte)0 : isInputIdDownHook!.Original(unk, inputId);
    private byte IsInputIdHeldDetour(void* unk, InputId inputId) => Suppress(inputId) ? (byte)0 : isInputIdHeldHook!.Original(unk, inputId);

    private bool Suppress(InputId inputId) => locked && Array.IndexOf(MovementInputs, inputId) >= 0;

    public void Dispose()
    {
        locked = false;
        isInputIdPressedHook?.Dispose();
        isInputIdDownHook?.Dispose();
        isInputIdHeldHook?.Dispose();
    }
}
