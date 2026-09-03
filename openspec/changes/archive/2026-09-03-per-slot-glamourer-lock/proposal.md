## Why

Collar and Outfit locks are both implemented as Glamourer's own whole-actor `Combination` lock (confirmed by decompiling `Glamourer.dll` while investigating `fix-testing-bugs-and-polish-controls`). That field covers the entire character, not a slot: locking a collar (Neck only, conceptually) or a locked outfit design blocks *every* slot and every manual edit in Glamourer, not just the one thing the Sub actually agreed to lock. Worse, because it's a single field, a second lock from a different source (e.g. Collar locking while Outfit is already locked) silently fails to take - `ActorState.Lock()` refuses to overwrite an existing different key, and neither `SetItem` nor `ApplyDesign` surfaces that refusal as an error - so the caller believes it succeeded while Glamourer's actual state never re-locked. And because the unlock key is the only thing that can ever release that field, losing track of it (a plugin reload at the wrong moment - see `fix-testing-bugs-and-polish-controls` task 1.4) can strand a Sub's entire Glamourer control, not just the one locked item, with no in-plugin recovery.

None of this matches what the Sub actually consents to per action category: locking the collar should only ever affect the Neck slot; locking an outfit design should only affect the slots that design itself changes; everything else must stay exactly as free to edit (by the Sub, by other tools, by Glamourer's own UI) as if nothing were locked at all. A future Restraints category is planned to lock its own slot(s) the same way, alongside Collar and Outfit, at the same time - so the lock model needs to support multiple independent, slot-scoped locks coexisting, not just fix Collar/Outfit in isolation.

## What Changes

- Stop using Glamourer's own state-wide `Combination` lock (`ApplyFlag.Lock` / `UnlockState` / `RevertToAutomation`'s implicit re-lock) for both Collar and Outfit. **BREAKING**: changes how "locked" is enforced for both categories, though the Sub/Owner-facing commands and their names stay the same.
- Introduce a plugin-internal, per-slot lock: each lock (Collar's Neck, an Outfit design's own affected slots) is tracked independently by which equipment slot(s) it covers, never by a single actor-wide flag.
- Enforce a locked slot by continuously re-asserting it (applied with `ApplyFlag.Once`, no Glamourer-level lock) whenever an outside change to that slot is detected, rather than relying on Glamourer to refuse the change itself.
- Allow multiple slot locks from different sources (Collar, Outfit, and later Restraints) to be active at the same time without conflicting, as long as they don't claim the same slot.
- A slot with no active lock from this plugin remains completely free to edit through any means (Glamourer's own UI, another plugin, this plugin's own aliases) - locking one thing must never touch or restrict any other slot.
- Releasing a lock (Sub's own unlock alias, Owner's override, panic) stops enforcing that slot and reverts it to Glamourer's automation-managed state for that slot alone, without touching any other currently-locked or currently-free slot.
- Design the tracking/enforcement mechanism to be reusable by a future Restraints category (not implemented in this change) - a third independent slot-lock source, not a special case bolted onto Collar/Outfit.

## Capabilities

### New Capabilities

- `collar/slot-locking`: the shared per-slot lock/enforcement behavior - multiple independent slot locks coexisting, a locked slot resisting outside changes, and every non-locked slot staying freely editable regardless of what else is locked.

### Modified Capabilities

- `collar/collaring`: the collar lock now scopes to the Neck slot only, enforced by this plugin rather than by Glamourer's own state-wide lock; every other slot stays freely editable while the collar is locked.
- `collar/outfit`: an Owner-held outfit lock now scopes to only the slots the applied design itself changes, enforced by this plugin rather than by Glamourer's own state-wide lock; every other slot stays freely editable while the outfit is locked.

## Impact

- `GlamourerIpc` - remove reliance on `ApplyFlag.Lock`, `UnlockState`, and `RevertToAutomation`'s re-lock behavior; add whatever per-slot query/apply surface the enforcement loop needs (e.g. reading a design's own affected slots, applying a single slot with `Once`).
- `OutfitCommand`, `CollarCommand` - replace their `Lock`/`Unlock`/`ForceApply`/`ForceUnlock` Glamourer calls with the new per-slot tracking and enforcement.
- A new shared slot-lock tracking/enforcement component (naming/shape TBD in design.md), advanced from the existing `Framework.Update` hook already driving the panic hotkey and Gesture's temporary-activation timers.
- `SubRuntimeState`/`PluginConfig.Locks` - the persisted lock-key model (`fix-testing-bugs-and-polish-controls` task 1.4) is replaced or adapted for the new per-slot tracking, since there's no longer a single Glamourer key to persist per category.
- `PanicHandler` - its outfit/collar revert steps change to release every active plugin-tracked slot lock instead of a single Glamourer `Unlock`/`Revert` call per category.
- README's Consent model and Automation risk sections, wherever they currently describe collar/outfit locking as "resists casual removal" without qualifying its scope.
