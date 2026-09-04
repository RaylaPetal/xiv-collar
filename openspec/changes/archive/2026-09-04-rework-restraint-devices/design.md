## Context

See `proposal.md` - Why. Relevant current-state details (from direct code inspection):

- **Single-item capture precedent already exists**: `CollarState` (`PluginConfig.cs`) captures one Neck-slot item (`ItemId`/`Stain`/`Stain2`) from whatever the Sub currently has equipped, via `GlamourerIpc.GetCurrentNeckItem()` - a thin wrapper around the fully generic `GlamourerIpc.GetEquipSlotValue(ApiEquipSlot slot)`, which already reads any of the 10 `LockableEquipSlots.All` from Glamourer's `GetState` IPC. `CollarCommand.ForceApply` locks that single item via `SlotLockManager.TryLock(owner, new Dictionary<ApiEquipSlot, SlotLockValue>{[ApiEquipSlot.Neck] = value})` directly - it never touches a whole-design apply path.
- **Current restraints are design-based**: `RestraintDeviceDefinition.DesignId` references a Glamourer design; `RestraintCommand.ApplyDevice` calls `glamourer.GetDesignEquipSlots(designId)` (which slots the design touches) then `glamourer.ApplyDesign(designId)`, then registers the resulting per-slot values with `slotLocks.TryRegisterAlreadyApplied` (a "the apply already happened externally, just track+enforce" registration, distinct from `TryLock`'s "apply and lock in one step").
- **`SlotLockManager`** already operates purely on `Dictionary<ApiEquipSlot, SlotLockValue>` - it has no idea whether a lock came from a whole-design apply or a single `SetItemOnce` call. Both `TryLock` (apply + lock) and `TryRegisterAlreadyApplied` (lock only, apply already happened) are already implemented and slot/item-generic.
- **`RestrictionRuleManager`** tracks activation per `RestraintRuleKind` via `RegisterEnforcer(kind, IRestrictionEnforcer)` where `Engage()`/`Release()` take no parameters - it assumes a rule kind's enforcer is uniform across every instance. `ForcedPose` is the one existing exception: its actual pose-application (`ApplyPose(poseModeId)`, a one-shot `/groundsit`-style command) happens directly in `RestraintCommand.ApplyDevice`, bypassing the generic enforcer entirely; only the conflict check (`WouldConflict`, comparing `PoseModeId` values across owners) and the presumed movement-block enforcer go through the generic mechanism.
- **Gesture's temporary Penumbra activation** (`GestureCommand.Execute`/`activeTemporary`/`ResetActiveTemporary`) already implements "temporarily enable a mod's options, redraw, play the trigger" via `PenumbraIpc.TrySetTemporarySettings`/`TryRedrawLocalPlayer`, but couples it to a 30-second idle-timeout auto-revert meant for one-shot gesture playback - not appropriate for a restraint rule that must hold until explicitly released.
- **Gesture's animation catalog** (`GestureCatalogEntry`, `AnimationPickerWindow`) is already a full searchable/grouped picker over every installed mod's playable animations (slash-emote or pose triggers), identified by a stable `Id`.

## Goals / Non-Goals

**Goals:**
- Replace whole-design restraint devices with single captured gear pieces, reusing the Collar's existing capture/lock pattern.
- Drop Restraints out of the unified scan step while keeping it in unified export/import.
- Let the Owner add a restraint quick command by name directly, not only via import.
- Add Arms Cuffed, Legs Cuffed, and Full Body Cuffed rule kinds, each carrying its own chosen animation (reusing Gesture's catalog/picker), held for the rule's duration with no idle timeout; Full Body Cuffed additionally blocks movement like forced pose.
- Rename the Gag rule's displayed label to "Gagged" without breaking saved configs.

**Non-Goals:**
- Migrating existing DesignId-based restraint devices - this is a breaking change; the Sub re-captures each device (see proposal.md's **BREAKING** note).
- Changing `collar/gesture`'s own behavior or its idle-timeout semantics - restraints get their own, separate temporary-activation tracking rather than sharing Gesture's `activeTemporary` state.
- Allowing a restraint device to capture more than one gear piece - each device is exactly one slot's item, per the proposal.
- Changing how `collar/outfit` or `collar/collaring` apply/lock items - only `collar/restraints` changes.

## Decisions

### 1. Device capture mirrors `CollarCommand`, parameterized by slot
`RestraintCommand` gains `CaptureCurrentAsDevice(ApiEquipSlot slot, string name)`, calling `glamourer.GetEquipSlotValue(slot)` and saving `RestraintDeviceDefinition{ Slot, ItemId, Stain, Stain2, Name, Rules }` - replacing `DesignId`. `RestraintMapping.ScannedDesigns` and `RestraintFolderAllowlist` are removed entirely; there is no scan step left for Restraints.

**Alternative considered**: keep a "scan" concept that enumerates currently-equipped items across all 10 slots at once, letting the Sub capture several devices from one action. Rejected as unnecessary complexity - `GetEquipSlotValue` is cheap to call per slot, and capturing one device at a time (naming each) matches how Collar already works and how the user described the flow ("save this piece specifically").

### 2. `ApplyDevice` drops the whole-design path entirely
Instead of `GetDesignEquipSlots`/`ApplyDesign`/`TryRegisterAlreadyApplied`, `ApplyDevice` calls `slotLocks.TryLock(Owner, new Dictionary<ApiEquipSlot, SlotLockValue>{[device.Slot] = new(device.ItemId, device.Stain, device.Stain2)})` directly - the same one-step apply-and-lock `CollarCommand.ForceApply` already uses. `WouldOverlap`/conflict-refusal behavior is unchanged since `SlotLockManager` doesn't care how a lock's caller decided its value.

### 3. Bound-animation rule kinds bypass the generic per-kind enforcer, same as ForcedPose
`ArmsCuffed`, `LegsCuffed`, and `FullBodyCuffed` each carry a chosen animation (`GestureCatalogEntry.Id`) per `RestraintRuleAssignment` instance, stored in a new nullable `AnimationId` field alongside the existing `PoseModeId` field (only `ForcedPose` uses `PoseModeId`; only these three use `AnimationId`). Like `ForcedPose`, the actual temporary-activation/hold/revert work happens directly in `RestraintCommand.ApplyDevice`/`ReleaseDevice` - not through a registered `IRestrictionEnforcer` - because each active instance's configuration (which animation) can differ, which the parameterless `Engage()/Release()` enforcer contract can't express.

`RestraintCommand` gains its own small per-device temporary-activation tracking (`Dictionary<string deviceId, (Guid Collection, string ModDirectory)>`), separate from `GestureCommand.activeTemporary`, so a restraint's held animation is never subject to Gesture's own 30-second idle-timeout revert and vice versa. Apply: `PenumbraIpc.TrySetTemporarySettings` + `TryRedrawLocalPlayer`, then play the catalog entry's trigger once (same one-shot activation step Gesture's `Execute` does - a pose trigger then holds naturally, a slash-emote trigger plays once). Release: `PenumbraIpc.TryRemoveTemporarySettings` for that device's tracked collection/mod.

`FullBodyCuffed` additionally engages the *existing* movement-suppression enforcer the forced-pose rule already registers with `RestrictionRuleManager` (parameterless, refcounted) - reusing it rather than adding a second movement-block mechanism.

**Alternative considered**: extend `IRestrictionEnforcer.Engage()` to take a parameter (e.g. the `RestraintRuleAssignment`). Rejected - it would ripple into `WalkOnly`/`ActionBlock`/`GagChat`'s existing parameterless enforcers for no benefit, when the codebase already has an established pattern (ForcedPose) for "this rule kind needs per-instance data, handle it in `RestraintCommand` directly."

### 4. Conflict-checking generalizes `RestrictionRuleManager.WouldConflict`'s existing ForcedPose special-case
`WouldConflict` already special-cases `ForcedPose` (compares `PoseModeId` across owners). It's extended to run the same same-kind-different-configuration check for `ArmsCuffed`/`LegsCuffed`/`FullBodyCuffed`, comparing `AnimationId` instead of `PoseModeId`. `WalkOnly`/`ActionBlock`/`GagChat` remain uniformly reference-counted (no configuration to conflict over).

### 5. Gag rule keeps its internal enum name, only the displayed label changes
`RestraintRuleKind.GagChat` is NOT renamed in code - `PluginConfig` serializes this enum, and renaming the C# member risks breaking already-saved configs if the serializer round-trips enum values by name rather than by ordinal. Only the UI strings ("Gag chat" → "Gagged") and this spec's user-facing wording change.

### 6. Owner manual "Add Command" mirrors Title's freeform pattern
`DrawRestraintQuickSection` gains an input box + "Add Command" button, identical in shape to `DrawTitleQuickSection`'s free-text entry: the Owner types a device name, and a new `QuickCommand{Label=name, Command="restraint lock \"<name>\""}` (no rules yet) is added - immediately eligible for the same "Configure rules" flow already built for imported entries (see the prior `owner-import-ui-fixes` change).

### 7. Wire format extends the existing rule-token grammar
`RestraintCommand.BuildLockCommand`/`TryParseLockCommand` (established in the prior change) already encode rules as `rules:pose=N,walkonly,actionblock,gag` after a quoted device name. New tokens are added additively: `armscuffed=<animationId>`, `legscuffed=<animationId>`, `fullbodycuffed=<animationId>` - an older paired client ignores unrecognized tokens (same graceful-degradation property already documented for this grammar).

## Risks / Trade-offs

- **[Risk]** Reverting a restraint's temporary Penumbra activation on an ungraceful disconnect/crash (no explicit release) could leave a mod's temporary settings active. → Mitigation: same exposure Gesture already accepts for its own temporary activations; unaffected by this change specifically, and panic/unpair already release every active rule including this one.
- **[Risk]** `RestraintRuleAssignment.AnimationId` referencing a `GestureCatalogEntry.Id` that no longer exists after the Sub rescans Gesture (mod removed/changed) would fail silently when applied. → Mitigation: same failure mode `GestureCommand.ForceApply` already has for a stale catalog id; not a new class of risk.
- **[Trade-off]** Full Body Cuffed reuses the forced-pose movement-block enforcer rather than introducing a dedicated one. Accepted since the two rules already provide byte-identical movement suppression semantics - a second implementation would be pure duplication.
- **[Trade-off]** No migration path for existing DesignId-based devices (explicitly a breaking change per proposal.md) - accepted given the plugin's current 0.0.0.1 pre-release status.
