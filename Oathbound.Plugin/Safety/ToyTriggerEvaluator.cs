using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Oathbound.Plugin.Commands;
using Oathbound.Plugin.Config;

namespace Oathbound.Plugin.Safety;

/// collar/toy-control "Local automatic toy triggers": entirely local, Sub-side automation - no Owner
/// involvement, no wire message, no network round-trip. Polled from the same per-frame `OnFrameworkUpdate`
/// dispatch every other per-frame checker in this plugin already uses. `HealthPercent` and
/// `RestrictionActive` are edge-triggered (fire on the transition into the condition, not on every tick it
/// stays true) so a Sub sitting below a health threshold, or holding an active restriction, for a long
/// stretch doesn't refire on every frame; `PlayerDamage`/`SpellCastOnYou` are inherently event-based
/// already (one hit, one candidate firing). Every rule additionally enforces its own cooldown (see
/// `TryFire`), with a 2-second floor beneath whatever the Sub configured, defensively - the same
/// clamp-not-reject posture every other numeric input in this plugin uses.
public sealed class ToyTriggerEvaluator : IDisposable
{
    private const int MinimumCooldownSeconds = 2;

    private readonly PluginConfig config;
    private readonly ToyControlCommand toyControl;
    private readonly SubRuntimeState runtimeState;
    private readonly RestrictionRuleManager restrictionRules;

    private readonly Dictionary<string, long> lastFiredTicks = new();
    private readonly Dictionary<string, bool> wasHealthBelowThreshold = new();
    private readonly Dictionary<string, bool> wasRestrictionActive = new();

    /// One entry per action-effect the hook callback observed aimed at the local player from another
    /// player character, since the last `OnFrameworkUpdate` tick drained it. A `ConcurrentQueue` rather
    /// than a plain list/flag - the hook callback and the framework-update tick are not guaranteed to be
    /// the same call stack, so this needs to be safe to enqueue into concurrently with the drain below.
    /// Deliberately not acted on from inside the hook callback itself - keeps every actual toy-triggering
    /// decision on the same framework-tick cadence as every other check here, and avoids doing IPC/Buttplug
    /// calls from inside a native-hook callback's call stack (see design.md Decision 4).
    private readonly ConcurrentQueue<PlayerActionHit> playerActionHits = new();

    private readonly record struct PlayerActionHit(uint ActionId, uint SourceJobId, bool IsDamage);

    public ToyTriggerEvaluator(PluginConfig config, ToyControlCommand toyControl, SubRuntimeState runtimeState, RestrictionRuleManager restrictionRules)
    {
        this.config = config;
        this.toyControl = toyControl;
        this.runtimeState = runtimeState;
        this.restrictionRules = restrictionRules;
        ActionEffect.ActionEffectEntryEvent += OnActionEffect;
    }

    /// collar/toy-control "Hit-by-player-action trigger fires"/"Spell cast on you": records every action
    /// effect (not just damage - `SpellCastOnYou` also wants heals/buffs/debuffs) aimed at the local player
    /// and sourced from an object of kind Pc (a real player character) - an NPC's action never enqueues an
    /// entry, per the "Hit-by-player-action" requirement's own Non-Goals note. `ActionEffectType.Nothing`
    /// entries (empty padding slots in a multi-target packet) are skipped.
    private void OnActionEffect(uint actionId, ushort animationId, ActionEffectType type, uint sourceId, ulong targetOid, uint damage)
    {
        if (type == ActionEffectType.Nothing) return;

        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer is null || targetOid != localPlayer.GameObjectId) return;

        var source = Plugin.ObjectTable.FirstOrDefault(o => o.EntityId == sourceId);
        if (source is null || source.ObjectKind != ObjectKind.Pc) return;

        var sourceJobId = source is ICharacter character ? character.ClassJob.RowId : 0u;
        playerActionHits.Enqueue(new PlayerActionHit(actionId, sourceJobId, type == ActionEffectType.Damage));
    }

    public void OnFrameworkUpdate()
    {
        var hitsThisTick = new List<PlayerActionHit>();
        while (playerActionHits.TryDequeue(out var hit))
            hitsThisTick.Add(hit);

        if (!config.ToyTriggersAcknowledged || runtimeState.ToyTriggersSuspended)
            return;

        var localPlayer = Plugin.ObjectTable.LocalPlayer;

        foreach (var rule in config.ToyTriggerRules)
        {
            if (!rule.Enabled) continue;

            switch (rule.Kind)
            {
                case ToyTriggerKind.HealthPercent:
                    EvaluateHealthPercent(rule, localPlayer);
                    break;
                case ToyTriggerKind.PlayerDamage:
                    if (hitsThisTick.Any(h => h.IsDamage)) TryFire(rule);
                    break;
                case ToyTriggerKind.RestrictionActive:
                    EvaluateRestrictionActive(rule);
                    break;
                case ToyTriggerKind.SpellCastOnYou:
                    if (hitsThisTick.Any(h => MatchesSpellFilter(rule, h))) TryFire(rule);
                    break;
            }
        }
    }

    /// collar/toy-control "Spell cast on you": `SpellJobIds`/`SpellActionIds` are independent AND filters -
    /// an empty list on either side means "any" for that side, so a rule with both empty matches every hit
    /// (any action, from any player).
    private static bool MatchesSpellFilter(ToyTriggerRule rule, PlayerActionHit hit) =>
        (rule.SpellActionIds.Count == 0 || rule.SpellActionIds.Contains(hit.ActionId)) &&
        (rule.SpellJobIds.Count == 0 || rule.SpellJobIds.Contains(hit.SourceJobId));

    /// collar/toy-control "Health-percentage trigger fires": edge-triggered on the transition from at-or-
    /// above the threshold to below it, not on every tick health stays below it.
    private void EvaluateHealthPercent(ToyTriggerRule rule, IPlayerCharacter? localPlayer)
    {
        if (localPlayer is null || localPlayer.MaxHp == 0) return;

        var percent = localPlayer.CurrentHp * 100.0 / localPlayer.MaxHp;
        var isBelow = percent <= rule.HealthPercentThreshold;
        var wasBelow = wasHealthBelowThreshold.TryGetValue(rule.Id, out var prev) && prev;
        wasHealthBelowThreshold[rule.Id] = isBelow;

        if (isBelow && !wasBelow)
            TryFire(rule);
    }

    /// collar/toy-control "Restriction-active trigger fires": edge-triggered on the transition into active,
    /// reusing the existing RestrictionRuleManager state - no new tracking needed for "is it active".
    private void EvaluateRestrictionActive(ToyTriggerRule rule)
    {
        var isActive = restrictionRules.IsActive(rule.RestrictionKind);
        var wasActive = wasRestrictionActive.TryGetValue(rule.Id, out var prev) && prev;
        wasRestrictionActive[rule.Id] = isActive;

        if (isActive && !wasActive)
            TryFire(rule);
    }

    /// collar/toy-control "Automatic triggers are rate-limited per rule": the per-rule cooldown gate every
    /// firing path above funnels through - a rule that fails to actually start a toy action (e.g. Intiface
    /// disconnected) does not consume its cooldown, so it can fire as soon as the condition is next true and
    /// a connection exists.
    private void TryFire(ToyTriggerRule rule)
    {
        var now = Environment.TickCount64;
        var cooldownMs = Math.Max(MinimumCooldownSeconds, rule.CooldownSeconds) * 1000L;
        if (lastFiredTicks.TryGetValue(rule.Id, out var last) && now - last < cooldownMs)
            return;

        var fired = rule.PatternName is { Length: > 0 } patternName
            ? toyControl.ForceApplyPattern(patternName)
            : toyControl.ForceApplyVibrate(rule.IntensityPercent ?? 50, rule.DurationSeconds is { } d ? ToyDuration.Bounded(d) : ToyDuration.Unspecified);

        if (fired)
            lastFiredTicks[rule.Id] = now;
    }

    public void Dispose()
    {
        ActionEffect.ActionEffectEntryEvent -= OnActionEffect;
    }
}
