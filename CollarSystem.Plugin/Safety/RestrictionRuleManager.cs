using System;
using System.Collections.Generic;
using System.Linq;
using CollarSystem.Plugin.Config;

namespace CollarSystem.Plugin.Safety;

/// A single enforcement mechanism a restriction rule kind drives (movement lock, walk-only, action-block,
/// gag chat) - Engage/Release must be idempotent, the same contract MovementLockService.Engage/Release
/// already has, since RestrictionRuleManager only calls Engage on a 0->1 transition and Release on a 1->0
/// transition but never assumes anything about prior state beyond that.
public interface IRestrictionEnforcer
{
    void Engage();
    void Release();
}

/// collar/restraints: generalizes SlotLockManager's per-owner claim tracking (collar/slot-locking) from
/// equipment slots to restriction rule kinds. Deliberately diverges from SlotLockManager's strict
/// one-owner-per-key model: ForcedPose is the only rule kind where two simultaneously active instances can
/// actually disagree (different pose targets), so it alone is conflict-checked against its Param; the
/// other three rule kinds (WalkOnly, ActionBlock, GagChat) are reference-counted - any number of devices
/// may hold the same rule kind active at once, and the underlying enforcer only releases once the last
/// holder releases. See design.md's "Decisions" section for why this diverges from SlotLockManager.
public sealed class RestrictionRuleManager
{
    private readonly Dictionary<RestraintRuleKind, Dictionary<string, int>> activeByKind = new();
    private readonly Dictionary<RestraintRuleKind, IRestrictionEnforcer> enforcers = new();

    public void RegisterEnforcer(RestraintRuleKind kind, IRestrictionEnforcer enforcer) => enforcers[kind] = enforcer;

    public bool IsActive(RestraintRuleKind kind) => activeByKind.TryGetValue(kind, out var owners) && owners.Count > 0;

    /// True if `rules` contains a ForcedPose assignment whose PoseModeId differs from an already-active
    /// ForcedPose claim held by a different owner - the only case where two rule instances can conflict.
    public bool WouldConflict(IEnumerable<RestraintRuleAssignment> rules, string owner)
    {
        var pose = rules.FirstOrDefault(r => r.Kind == RestraintRuleKind.ForcedPose);
        if (pose is null)
            return false;
        if (!activeByKind.TryGetValue(RestraintRuleKind.ForcedPose, out var owners) || owners.Count == 0)
            return false;

        return owners.Any(kv => kv.Key != owner && kv.Value != pose.PoseModeId);
    }

    /// Activates every rule in `rules` for `owner`. Refuses (activating nothing) if WouldConflict is true -
    /// callers must not have applied anything visible yet when this returns false, same "refuse the whole
    /// action, never partially" guarantee OutfitCommand.ApplyDesign gives via SlotLockManager.WouldOverlap.
    public bool TryActivate(string owner, IReadOnlyList<RestraintRuleAssignment> rules)
    {
        if (rules.Count == 0)
            return false;
        if (WouldConflict(rules, owner))
        {
            Plugin.Log.Warning($"RestrictionRuleManager: \"{owner}\" refused - a ForcedPose rule conflicts with a different pose already active.");
            return false;
        }

        foreach (var rule in rules)
        {
            var owners = activeByKind.TryGetValue(rule.Kind, out var existing) ? existing : activeByKind[rule.Kind] = new Dictionary<string, int>();
            var wasEmpty = owners.Count == 0;
            owners[owner] = rule.PoseModeId;
            if (wasEmpty && enforcers.TryGetValue(rule.Kind, out var enforcer))
                enforcer.Engage();
        }

        return true;
    }

    /// Deactivates every rule kind `owner` holds. A rule kind only actually disengages its enforcer once no
    /// other owner still holds it active.
    public void Release(string owner)
    {
        foreach (var (kind, owners) in activeByKind)
        {
            if (!owners.Remove(owner))
                continue;
            if (owners.Count == 0 && enforcers.TryGetValue(kind, out var enforcer))
                enforcer.Release();
        }
    }

    /// Panic's own release: drops every tracked claim and unconditionally releases every registered
    /// enforcer, regardless of refcount - mirrors SlotLockManager.ReleaseAllForPanic's "drop bookkeeping,
    /// don't bother with per-owner accounting" shape.
    public void ReleaseAllForPanic()
    {
        activeByKind.Clear();
        foreach (var enforcer in enforcers.Values)
            enforcer.Release();
    }
}
