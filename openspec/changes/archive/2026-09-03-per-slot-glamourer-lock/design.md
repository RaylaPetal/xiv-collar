## Context

See `proposal.md` for motivation. This covers how the new per-slot lock actually works, based on decompiling `Glamourer.dll`/`Glamourer.Api.dll` (installed version 1.6.1.7) to find surfaces the public API docs don't fully spell out:

- `ActorState.Combination` (the field `ApplyFlag.Lock`/`UnlockState` manipulate) is one field per actor - never per slot. This change stops using it entirely for Collar/Outfit; every apply goes through `ApplyFlag.Once` instead (no Glamourer-level lock at all).
- `SetItem(objectIndex, slot, itemId, stains, key, flags)` already applies exactly one equipment slot - this is the primitive the new model builds on for both applying and re-asserting a locked slot.
- `GetDesignJObject(designId)` returns the design's own JSON, whose `Equipment.<SlotName>.Apply` boolean is Glamourer's own record of which slots that design is configured to change (confirmed via `DesignBase.SerializeEquipment()`). This is what "the slots the design itself changes" (proposal) actually means - not a diff of before/after equipped items, which would miss a slot the design intends to control but whose value happens to already match.
- `StateFinalized` (`Glamourer.RevertToAutomation` and friends already covered this) is a push IPC event, firing with a `StateFinalizationType` whenever an actor's tracked state finishes changing, for any reason (manual edit, another IPC call, automation, gearset change). This is what detecting "an outside change to a locked slot" is built on, in place of a per-frame poll.
- `RevertToAutomation` is confirmed (see `fix-testing-bugs-and-polish-controls`) to genuinely recompute and reapply automation, but only for the *entire* actor at once (`Applier.ApplyAll` on the whole `DesignData`) - there is no narrower "give me automation's value for slot X" surface anywhere in the public API.

## Goals / Non-Goals

**Goals:**
- Never touch Glamourer's own `Combination` lock for Collar or Outfit.
- Lock exactly the slots each category owns (Collar: Neck; Outfit: whatever the applied design's `Equipment.*.Apply` flags mark), and nothing else.
- Let Collar, Outfit, and (later) Restraints hold independent locks at the same time without conflicting.
- Make locks survive a plugin reload without needing any Glamourer-side key.
- Preserve the existing "unlock reverts to automation" guarantee (`fix-testing-bugs-and-polish-controls`) for the slot(s) being released, even though Glamourer can only recompute automation for the whole actor.

**Non-Goals:**
- Implementing the Restraints category itself - only making the lock mechanism generic enough that it can register a third owner later without changes to this mechanism.
- Changing Gesture's own temporary-Penumbra-activation mechanism (unrelated system, already self-cleaning per `fix-testing-bugs-and-polish-controls`).
- Locking customization/body data, bonus items, or weapons - scope stays the 10 standard `ApiEquipSlot` gear slots, matching what `SetItem` already operates on and what Collar/Outfit have ever meant by "locked" here.
- A UI for a Sub to hand-pick arbitrary slots to lock - Collar and Outfit each derive their own slot set from what they're already doing (Neck; the applied design's own flags).

## Decisions

### A new `SlotLockManager` owns all locking, apply, and enforcement
A new class (`CollarSystem.Plugin.Safety.SlotLockManager`, alongside `PanicHandler`) becomes the single place that talks to Glamourer for anything lock-related. It holds `Dictionary<ApiEquipSlot, SlotLock>` where `SlotLock` is `(string Owner, ulong ItemId, byte Stain, byte Stain2)`, persisted through a new `PluginConfig.SlotLocks` (a plain list, replacing `fix-testing-bugs-and-polish-controls`'s key-based `PluginConfig.Locks`/`LockState` entirely - there is no Glamourer key left to persist).

- `TryLock(string owner, IReadOnlyDictionary<ApiEquipSlot, (ulong ItemId, byte Stain, byte Stain2)> slots)`: refuses (returns false, changes nothing) if any requested slot is already owned by a *different* owner (`collar/slot-locking`'s overlap-refusal requirement). Otherwise applies each slot via `SetItem(..., ApplyFlag.Once)` (never `Lock`) and records it.
- `Release(string owner)`: see "Release restores every other slot" below.
- `HasLock(string owner)`: read-only check for UI enable/disable, same shape as `GestureCommand.HasActiveTemporary`.

`CollarCommand`/`OutfitCommand` stop calling `GlamourerIpc`'s lock/unlock surface directly and go through `SlotLockManager` instead - `CollarCommand.ForceApply` calls `TryLock("Collar", { Neck: configured item })`; `OutfitCommand.Apply`/`ForceApply` first resolve the design's affected slots (see below), then call `TryLock("Outfit", ...)`.

Alternative considered: keep the lock/key bookkeeping inside `OutfitCommand`/`CollarCommand` themselves, each independently. Rejected - the whole point is one shared place that knows about every slot every category owns, so the overlap check and enforcement loop have a single source of truth; duplicating that per-category is exactly the "special case bolted onto Collar/Outfit" the proposal rules out for Restraints later.

### An outfit design's locked slots come from its own `Equipment.*.Apply` flags, not a before/after diff
Before applying a design with a lock, `GlamourerIpc` calls `GetDesignJObject(designId)` and reads `Equipment.<Slot>.Apply` for each of the 10 gear slots to build the set of slots to lock. A before/after diff of `GetState` was considered and rejected: a design can be configured to control a slot (`Apply: true`) whose target item happens to already match what's currently equipped, in which case a diff would see no change and wrongly leave that slot unlocked - the design's own flags are what the Sub/Owner actually configured to be part of the look, independent of what's currently equipped.

### Enforcement is event-driven off both `StateChangedWithType` and `StateFinalized`, not a per-frame poll
`SlotLockManager` subscribes to *both* of Glamourer's change events (filtered to the local player's own actor pointer) - not `StateFinalized` alone as first implemented. In-game testing showed a single manual edit in Glamourer's own UI (e.g. unequipping one piece) only ever raises `StateChangedWithType`; `StateFinalized` only fires once a *grouped* change (a full design apply, a gearset switch) completes. Decompiling GagSpeak's own `GlamourListener` confirmed this is exactly why it also subscribes to both: reacting to `StateFinalized` alone silently misses every individual slot edit, which is exactly the "I can unequip a locked piece in Glamourer and nothing stops me" symptom this was corrected from. On either event firing, for each currently-tracked slot `SlotLockManager` reads the actor's current value (`GetState`) and reapplies (`SetItem`, `Once`) any slot whose value no longer matches the locked value. Because the reapply itself sets exactly the locked value, the event(s) it triggers find everything already matching on the very next pass and do nothing further - self-terminating without needing an explicit reentrancy guard, though the `isEnforcing` guard flag is still included as a cheap safety net against a pathological event storm (now more load-bearing than originally scoped, since `StateChangedWithType` fires far more often than `StateFinalized` alone would have).

Alternative considered (original decision, corrected after testing): `StateFinalized` alone. Rejected once in-game testing showed it misses individual manual edits entirely - see above.

Alternative considered: poll every `Framework.Update` tick (the pattern `GestureCommand`'s timers already use). Rejected for this specific job - re-reading the whole actor state 60 times a second just to notice nothing changed most of the time is wasted IPC/JSON-parsing work, where Glamourer's own change events already tell us exactly when something worth checking happened.

### Releasing a lock does a snapshot-revert-restore dance to keep the "reverts to automation" guarantee
Per explicit direction: releasing a lock should still make the released slot(s) pick up Glamourer's automation-managed value, even though `RevertToAutomation` only operates on the whole actor. `SlotLockManager.Release(owner)`:
1. Reads the *current* value of every one of the 10 gear slots via `GetState` (not just tracked ones - this must also preserve a slot the Sub freely customized with no lock at all).
2. Calls `RevertToAutomation(key: 0, flags: ApplyFlag.Equipment)` - `Equipment` only, deliberately excluding `Customization`, so a slot-scoped unlock never touches face/body data.
3. Immediately reapplies (`SetItem`, `Once`) every slot from step 1's snapshot *except* the slots being released - this restores every other lock (still enforced, matching its own owner's intended value) and every free slot (restored to whatever the Sub had) to exactly where they were, so only the released slot(s) keep whatever automation just computed for them.
4. Removes the released owner's entries from the tracked dictionary.

This briefly touches every equipment slot under the hood around an unlock (a possible single-frame flicker), which is the accepted cost of preserving the automation-revert guarantee without a narrower Glamourer surface to lean on.

Alternative considered (raised and rejected during design): a release that just stops enforcing the slot and leaves its current value in place, with no forced change. Simpler and has no flicker, but silently drops the "outfit unlock reverts to automation" behavior `fix-testing-bugs-and-polish-controls` just established, for exactly the slots this change touches - rejected in favor of preserving that guarantee.

### Panic keeps a single, unconditional whole-actor revert - not the snapshot/restore dance
`PanicHandler` is a full teardown by design (`collar/pairing`: "reverts all Glamourer state") - it does not need to preserve anything, unlike a normal single-category release. Its Glamourer step becomes: revert the whole actor unconditionally, then clear every entry from `SlotLockManager` (bookkeeping only, no further Glamourer calls needed since the whole-actor revert already covered every slot including every lock). This keeps panic exactly as simple and single-purpose as it already is - the one path in this plugin that deliberately does *not* try to be surgical.

### `SlotLockManager` state persists through `PluginConfig`, no key required
`PluginConfig.SlotLocks` (`List<SlotLockEntry { ApiEquipSlot Slot; string Owner; ulong ItemId; byte Stain; byte Stain2 }>`) is loaded into `SlotLockManager`'s in-memory dictionary at construction and saved on every change, mirroring `fix-testing-bugs-and-polish-controls` task 1.4's persistence pattern. Unlike that fix, there is no key-loss failure mode left to guard against at all: since nothing is ever locked through Glamourer's own `Combination` field, re-establishing enforcement after a reload just means resuming the same `SetItem`/`Once` reapply logic against the persisted slot values - no key, no `InvalidKey`, no stuck state. This directly satisfies `collar/slot-locking`'s "active slot locks survive a plugin reload" requirement, and is strictly more robust than the model it replaces.

## Risks / Trade-offs

- [The release-time snapshot/restore dance briefly touches every equipment slot, which could itself trigger `StateFinalized` reentrancy or a visible one-frame flicker] → The `isEnforcing` guard prevents the enforcement loop from reacting to the plugin's own in-flight release sequence; the flicker is a single Framework tick, accepted per the explicit trade-off decision above.
- [`GetDesignJObject`'s exact JSON shape is undocumented in the public API surface, only confirmed by decompiling this specific Glamourer version] → Parse defensively (missing/malformed `Apply` treated as `false`, not thrown), consistent with `GlamourerIpc.GetCurrentNeckItem`'s existing "never throw on unexpected shape" precedent; a future Glamourer update changing this shape degrades to "no slots locked" rather than crashing.
- [A Sub or Owner tool could, in principle, still change a locked slot for one single frame before `StateFinalized` fires and the reapply lands] → Matches the accepted latency of any event-driven reassertion approach (including GagSpeak's own model); not a regression versus not locking at all, and still vastly better than the previous whole-actor lock's total lack of recovery when its key was lost.
- [`SlotLockManager` becomes a new required dependency for `CollarCommand`/`OutfitCommand`/`PanicHandler`, all of which need updating together] → Scoped entirely to this change's tasks; no partial-migration state where some code still uses the old `GlamourerIpc.Unlock`/`RevertToAutomation`-with-`Lock`-flag path.

## Migration Plan

`PluginConfig.Locks`/`LockState` (from `fix-testing-bugs-and-polish-controls`) is removed and replaced by `PluginConfig.SlotLocks`. There is no way to carry forward an existing key-based lock into a slot-based one (they're different concepts - a key protects the whole actor, a `SlotLockEntry` protects one slot with a known target value), so on first load after this change, any config still carrying an old-style `OutfitForceLocked`/`CollarForceLocked: true` from before the upgrade is treated as unlocked (`SlotLocks` starts empty) - if the Sub's Glamourer state happens to still be actor-locked from before the upgrade (the old failure mode this change eliminates going forward), they resolve it the same way as any pre-existing stuck lock today: unlock directly through Glamourer's own UI once. No further migration step is needed since the old fields are simply no longer read.
