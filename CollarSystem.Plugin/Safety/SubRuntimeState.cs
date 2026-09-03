namespace CollarSystem.Plugin.Safety;

/// In-memory (not persisted) record of what an Owner has currently applied to this Sub, kept purely so
/// PanicHandler can revert everything using only local state - no relay round-trip, no Owner cooperation.
/// Glamourer's lock in particular can only be released with the same key that set it (see GlamourerIpc),
/// so OutfitLockKey has to be retained here the moment the lock is applied.
public sealed class SubRuntimeState
{
    public bool TitleApplied { get; set; }
    public uint? OutfitLockKey { get; set; }
    public bool MovementLockActive { get; set; }

    /// Set by TitleCommand.ForceApply/OutfitCommand.ForceApply (the Owner's "joker" override - see
    /// ChatCommandListener's reserved-keyword grammar). While true, the Sub's own alias-triggered
    /// Apply/Clear/Unlock for that category is refused - only the matching Force* release (or panic,
    /// which always works regardless) can undo it.
    public bool TitleForceLocked { get; set; }
    public bool OutfitForceLocked { get; set; }

    /// collar/collaring: set when CollarCommand.ForceApply locks the Sub's configured collar at pairing
    /// acceptance. Same in-memory-only, per-session shape as OutfitLockKey/OutfitForceLocked - released by
    /// CollarCommand.ForceUnlock (the Owner's `collar unlock` override) or unconditionally by panic.
    public uint? CollarLockKey { get; set; }
    public bool CollarForceLocked { get; set; }

    public void Reset()
    {
        TitleApplied = false;
        OutfitLockKey = null;
        MovementLockActive = false;
        TitleForceLocked = false;
        OutfitForceLocked = false;
        CollarLockKey = null;
        CollarForceLocked = false;
    }
}
