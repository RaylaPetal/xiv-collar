using System;
using System.Collections.Generic;
using System.Linq;
using ECommons.GameHelpers;
using Oathbound.Plugin.Config;
using Oathbound.Plugin.Ipc;

namespace Oathbound.Plugin.Safety;

/// collar/attached-moodles: the single place that knows which Oathbound source is keeping which Moodles
/// status on this Sub, so a moodle is only ever removed one status at a time, and only once nothing else
/// still holds it (a shared "Bound" stays while any restraint carrying it is engaged). Source keys:
///
///   outfit                 the current outfit's moodle (at most one)
///   restraint:<deviceId>   one per engaged restraint device
///   follow                 the leash
///   collar                 the collar's assigned moodle
///   manual:<statusId>      a standing Owner `moodle apply` / Sub moodle alias
///
/// Persisted in PluginConfig.AttachedMoodleHolds so a moodle whose source didn't survive a reload can still
/// be found and removed afterwards (see OnFrameworkUpdate).
public sealed class AttachedMoodleLedger
{
    public const string OutfitSource = "outfit";
    public const string FollowSource = "follow";
    public const string CollarSource = "collar";
    public const string RestraintPrefix = "restraint:";
    private const string ManualPrefix = "manual:";

    private readonly PluginConfig config;
    private readonly MoodlesIpc moodles;
    private bool reconciledAfterLoad;

    public AttachedMoodleLedger(PluginConfig config, MoodlesIpc moodles)
    {
        this.config = config;
        this.moodles = moodles;
    }

    private Dictionary<string, Guid> Holds => config.AttachedMoodleHolds;

    public static string RestraintSource(string deviceId) => RestraintPrefix + deviceId;
    public static string ManualSource(Guid statusId) => ManualPrefix + statusId;

    /// Applies `statusId` and records `source` as holding it. Re-holding the same status re-applies it
    /// (which is how the collar's periodic reassertion works); holding a different one first releases
    /// whatever `source` held before, so an outfit swap never stacks two outfit moodles. Returns false (and
    /// records nothing) if Moodles couldn't apply it - the caller's own action still stands.
    public bool Hold(string source, Guid statusId)
    {
        if (Holds.TryGetValue(source, out var previous) && previous != statusId)
            Release(source);

        if (!moodles.ApplyStatus(statusId))
            return false;

        if (!Holds.TryGetValue(source, out var existing) || existing != statusId)
        {
            Holds[source] = statusId;
            config.Save();
        }
        return true;
    }

    /// Stops `source` holding anything; removes that one status from the Sub only if no other source still
    /// holds it. A no-op for a source that holds nothing.
    public bool Release(string source)
    {
        if (!Holds.Remove(source, out var statusId))
            return false;

        config.Save();
        if (!Holds.ContainsValue(statusId))
            moodles.RemoveStatus(statusId);
        return true;
    }

    public void ReleaseAllWithPrefix(string prefix)
    {
        foreach (var source in Holds.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            Release(source);
    }

    /// The Owner's `moodle clear` / the fixed `clear-moodle` word: drops every standing manual moodle, clears
    /// the Sub's moodles, then re-applies whatever an active outfit/restraint/leash/collar still holds. Clear-
    /// then-reapply rather than removing unheld statuses one by one, since listing the Sub's current statuses
    /// would mean mirroring Moodles' large MoodlesStatusInfo tuple field-for-field (design.md D1). Re-applied
    /// moodles get their duration reset, which is the accepted trade-off.
    public bool ClearUnheld()
    {
        foreach (var source in Holds.Keys.Where(k => k.StartsWith(ManualPrefix, StringComparison.Ordinal)).ToList())
            Holds.Remove(source);
        config.Save();

        if (!moodles.ClearStatus())
            return false;

        foreach (var statusId in Holds.Values.Distinct().ToList())
            moodles.ApplyStatus(statusId);
        return true;
    }

    /// The Owner's `revert all`: every moodle goes - manual, outfit, restraint, leash, and any the Sub applied
    /// themselves - except the collar's own, which is re-applied afterward since the collar is the one thing
    /// a revert never touches. Same clear-then-reapply shape as ClearUnheld.
    public bool ClearAllExceptCollar()
    {
        foreach (var source in Holds.Keys.Where(k => k != CollarSource).ToList())
            Holds.Remove(source);
        config.Save();

        if (!moodles.ClearStatus())
            return false;

        if (Holds.TryGetValue(CollarSource, out var collarStatus))
            moodles.ApplyStatus(collarStatus);
        return true;
    }

    /// Panic: everything goes, held or not.
    public void ClearAllForPanic()
    {
        Holds.Clear();
        config.Save();
        moodles.ClearStatus();
    }

    /// collar/attached-moodles "Attached moodles do not outlive their action across a reload": restraint
    /// devices and the leash never survive a reload, so their moodles are removed once, as soon as the local
    /// player exists (Moodles can't act on a character that isn't loaded yet). The outfit, collar and manual
    /// holds are kept - those sources do persist.
    public void OnFrameworkUpdate()
    {
        if (reconciledAfterLoad || Player.Object is null)
            return;

        reconciledAfterLoad = true;
        ReleaseAllWithPrefix(RestraintPrefix);
        Release(FollowSource);
    }
}
