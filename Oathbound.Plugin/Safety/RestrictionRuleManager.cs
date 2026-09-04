using System;
using System.Collections.Generic;
using System.Linq;
using Oathbound.Plugin.Config;

namespace Oathbound.Plugin.Safety;

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
/// one-owner-per-key model: some rule kinds carry per-instance configuration that two simultaneously
/// active instances can actually disagree on (ForcedPose's pose target; ArmsCuffed/LegsCuffed/
/// FullBodyCuffed's chosen animation), so those alone are conflict-checked against a string "config key"
/// (see ConfigKey) - the other kinds (WalkOnly, ActionBlock, GagChat) are reference-counted with no
/// config-key comparison: any number of devices may hold the same rule kind active at once, and the
/// underlying enforcer only releases once the last holder releases. See design.md's "Decisions" section
/// for why this diverges from SlotLockManager.
public sealed class RestrictionRuleManager
{
    private readonly Dictionary<RestraintRuleKind, Dictionary<string, string>> activeByKind = new();
    private readonly Dictionary<RestraintRuleKind, IRestrictionEnforcer> enforcers = new();

    public void RegisterEnforcer(RestraintRuleKind kind, IRestrictionEnforcer enforcer) => enforcers[kind] = enforcer;

    public bool IsActive(RestraintRuleKind kind) => activeByKind.TryGetValue(kind, out var owners) && owners.Count > 0;

    /// The per-instance configuration a rule kind conflict-checks on - ForcedPose's pose target,
    /// ArmsCuffed/LegsCuffed/FullBodyCuffed's chosen animation id. Null for kinds with no such
    /// configuration (WalkOnly/ActionBlock/GagChat are never conflict-checked).
    private static string? ConfigKey(RestraintRuleAssignment rule) => rule.Kind switch
    {
        RestraintRuleKind.ForcedPose => rule.PoseModeId.ToString(),
        RestraintRuleKind.ArmsCuffed or RestraintRuleKind.LegsCuffed or RestraintRuleKind.FullBodyCuffed => rule.AnimationId,
        _ => null,
    };

    /// True if `rules` contains a config-checked assignment (see ConfigKey) whose configuration differs
    /// from an already-active claim of the same kind held by a different owner - the only case where two
    /// rule instances can conflict.
    public bool WouldConflict(IEnumerable<RestraintRuleAssignment> rules, string owner)
    {
        foreach (var rule in rules)
        {
            var configKey = ConfigKey(rule);
            if (configKey is null)
                continue;
            if (!activeByKind.TryGetValue(rule.Kind, out var owners) || owners.Count == 0)
                continue;
            if (owners.Any(kv => kv.Key != owner && kv.Value != configKey))
                return true;
        }
        return false;
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
            Plugin.Log.Warning($"RestrictionRuleManager: \"{owner}\" refused - a rule conflicts with a different configuration already active.");
            return false;
        }

        foreach (var rule in rules)
        {
            var owners = activeByKind.TryGetValue(rule.Kind, out var existing) ? existing : activeByKind[rule.Kind] = new Dictionary<string, string>();
            var wasEmpty = owners.Count == 0;
            owners[owner] = ConfigKey(rule) ?? "";
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
