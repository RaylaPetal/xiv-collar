using System;
using System.Collections.Generic;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.System.Input;

namespace Oathbound.Plugin.Commands;

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

    /// Which independent callers currently want movement suppressed - a Set rather than a bare bool so two
    /// unrelated callers (Follow's leash, a forced-pose restraint device - collar/restraints) can each
    /// Engage/Release their own claim without one caller's Release prematurely lifting the other's.
    /// Add/Remove are naturally idempotent for repeat same-token calls, unlike a naive increment/decrement
    /// counter would be.
    private readonly HashSet<string> engagedBy = new();

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

    public bool IsLocked => IsAvailable && engagedBy.Count > 0;

    /// collar/follow: "Movement lock releases on panic, unpair, or Owner release" - all three paths call
    /// Release(owner) for their own token, and it is safe to call Engage even if IsAvailable is false
    /// (engagedBy just never gains an entry that would suppress anything).
    public void Engage(string owner)
    {
        if (IsAvailable)
            engagedBy.Add(owner);
    }

    public void Release(string owner) => engagedBy.Remove(owner);

    /// Panic's own release: drops every caller's claim unconditionally, regardless of who engaged it -
    /// same "full teardown, nothing needs preserving" shape as SlotLockManager.ReleaseAllForPanic.
    public void ReleaseAll() => engagedBy.Clear();

    private byte IsInputIdPressedDetour(void* unk, InputId inputId) => Suppress(inputId) ? (byte)0 : isInputIdPressedHook!.Original(unk, inputId);
    private byte IsInputIdDownDetour(void* unk, InputId inputId) => Suppress(inputId) ? (byte)0 : isInputIdDownHook!.Original(unk, inputId);
    private byte IsInputIdHeldDetour(void* unk, InputId inputId) => Suppress(inputId) ? (byte)0 : isInputIdHeldHook!.Original(unk, inputId);

    private bool Suppress(InputId inputId) => engagedBy.Count > 0 && Array.IndexOf(MovementInputs, inputId) >= 0;

    public void Dispose()
    {
        engagedBy.Clear();
        isInputIdPressedHook?.Dispose();
        isInputIdDownHook?.Dispose();
        isInputIdHeldHook?.Dispose();
    }
}
