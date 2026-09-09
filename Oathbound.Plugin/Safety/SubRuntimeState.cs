using System.Numerics;
using Oathbound.Plugin.Config;

namespace Oathbound.Plugin.Safety;

/// What an Owner has currently applied to this Sub, kept purely so PanicHandler can revert everything
/// using only local state - no relay round-trip, no Owner cooperation. Which Glamourer slots are actually
/// locked lives in SlotLockManager (collar/slot-locking) now, not here - this only tracks the
/// Owner-override "force locked" bookkeeping (persisted through PluginConfig so it survives a reload, same
/// as the slot locks themselves) plus a few other in-memory-only flags with no external state to lose.
public sealed class SubRuntimeState
{
    private readonly PluginConfig config;

    public SubRuntimeState(PluginConfig config)
    {
        this.config = config;
    }

    public bool TitleApplied { get; set; }
    public bool MovementLockActive { get; set; }

    /// Set by TitleCommand.ForceApply/OutfitCommand.ForceApply (the Owner's "joker" override - see
    /// ChatCommandListener's reserved-keyword grammar). While true, the Sub's own alias-triggered
    /// Apply/Clear/Unlock for that category is refused - only the matching Force* release (or panic,
    /// which always works regardless) can undo it.
    public bool TitleForceLocked { get; set; }

    /// collar/title "Force-applied title reasserts if removed or changed": the style TitleCommand.ForceApply
    /// most recently sent to Honorific, replayed by TitleCommand.OnFrameworkUpdate for as long as
    /// TitleForceLocked stays true. In-memory only, matching TitleForceLocked itself - there's nothing to
    /// reassert after a reload since the lock doesn't survive one either.
    public string? TitleForceText { get; set; }
    public bool TitleForceIsPrefix { get; set; }
    public Vector3 TitleForceColor { get; set; } = new(1, 1, 1);
    public Vector3? TitleForceGlow { get; set; }

    public bool OutfitForceLocked
    {
        get => config.OutfitForceLocked;
        set { config.OutfitForceLocked = value; config.Save(); }
    }

    /// collar/collaring: set when CollarCommand.ForceApply locks the Sub's configured collar at pairing
    /// acceptance. Released by CollarCommand.ForceUnlock (the Owner's `collar unlock` override) or
    /// unconditionally by panic.
    public bool CollarForceLocked
    {
        get => config.CollarForceLocked;
        set { config.CollarForceLocked = value; config.Save(); }
    }

    /// collar/restraints: set by RestraintCommand.ForceApply (the Owner's "joker" override). While true,
    /// the Sub's own alias-triggered device apply/release is refused - only the matching ForceUnlock (or
    /// panic) can undo it, same pattern as OutfitForceLocked/CollarForceLocked.
    public bool RestraintsForceLocked
    {
        get => config.RestraintsForceLocked;
        set { config.RestraintsForceLocked = value; config.Save(); }
    }

    public void Reset()
    {
        TitleApplied = false;
        MovementLockActive = false;
        TitleForceLocked = false;
        TitleForceText = null;
        OutfitForceLocked = false;
        CollarForceLocked = false;
        RestraintsForceLocked = false;
    }
}
