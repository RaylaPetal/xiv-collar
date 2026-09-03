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

    public void Reset()
    {
        TitleApplied = false;
        OutfitLockKey = null;
        MovementLockActive = false;
    }
}
