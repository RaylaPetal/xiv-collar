## 1. Data model: single-item device capture

- [x] 1.1 Replace `RestraintDeviceDefinition.DesignId` with `Slot` (`ApiEquipSlot`), `ItemId`, `Stain`, `Stain2`, and verify the config round-trips through save/load
- [x] 1.2 Remove `RestraintMapping.ScannedDesigns` and `PluginConfig.RestraintFolderAllowlist`, and verify the project still builds with no remaining references
- [x] 1.3 Add `RestraintCommand.CaptureCurrentAsDevice(ApiEquipSlot slot, string name)` calling `glamourer.GetEquipSlotValue(slot)` and saving a new `RestraintDeviceDefinition`, mirroring `CollarCommand.CaptureCurrentAsCollar`, and verify capturing while a piece is equipped in the chosen slot produces a device with the correct item/stain/stain2

## 2. Apply/release: direct slot-lock instead of whole-design apply

- [x] 2.1 Rewrite `RestraintCommand.ApplyDevice` to call `slotLocks.TryLock(Owner, {[device.Slot] = SlotLockValue})` directly, removing `GetDesignEquipSlots`/`ApplyDesign`/`TryRegisterAlreadyApplied`, and verify applying a device changes only its own slot
- [x] 2.2 Verify `ReleaseDevice`'s existing refcounted `slotLocks.Release(Owner)` behavior is unaffected by the capture-mechanism change
- [x] 2.3 Verify `WouldOverlap`-based lock-conflict refusal (a captured device's slot already locked by a different owner) still refuses correctly

## 3. New rule kinds: Arms Cuffed, Legs Cuffed, Full Body Cuffed

- [x] 3.1 Add `RestraintRuleKind.ArmsCuffed`, `LegsCuffed`, `FullBodyCuffed`, and an `AnimationId` field on `RestraintRuleAssignment`, and verify the config round-trips through save/load
- [x] 3.2 Add per-device temporary-activation tracking in `RestraintCommand` (`Dictionary<string, (Guid Collection, string ModDirectory)>`), separate from `GestureCommand.activeTemporary`, and verify it does not interact with Gesture's own idle-timeout revert — implemented as `Dictionary<(string DeviceId, RestraintRuleKind Kind), ...>` instead of keyed by device id alone, since one device can carry both Arms Cuffed and Legs Cuffed simultaneously with different animations
- [x] 3.3 On `ApplyDevice`, for each of the three new rule kinds present, resolve `AnimationId` against `GestureMapping.LocalCatalog`, call `PenumbraIpc.TrySetTemporarySettings`/`TryRedrawLocalPlayer`, and play the entry's trigger once, and verify the chosen animation engages and holds with no automatic revert
- [x] 3.4 On `ReleaseDevice`, call `PenumbraIpc.TryRemoveTemporarySettings` for any tracked temporary activation belonging to that device, and verify it reverts to the mod's saved settings
- [x] 3.5 Make `FullBodyCuffed` additionally engage/release the existing forced-pose movement-suppression enforcer, and verify movement is blocked while it's active and restored on release — implemented as a *second* `MovementLockEnforcer` instance with its own token ("RestraintsFullBody"), not the same instance as ForcedPose's: sharing one token across two independently-refcounted rule kinds would let one kind's release prematurely drop the other's still-active claim (found while implementing, not anticipated in design.md)
- [x] 3.6 Extend `RestrictionRuleManager.WouldConflict` to compare `AnimationId` for `ArmsCuffed`/`LegsCuffed`/`FullBodyCuffed` the same way it already compares `PoseModeId` for `ForcedPose`, and verify two devices with the same kind but different animations are refused while the same animation coexists — generalized via a shared `ConfigKey(rule)` string comparison for all four config-checked kinds
- [x] 3.7 Extend `ReleaseAllForPanic`/panic handling to revert every held bound-animation temporary activation, and verify panic clears them alongside every other active rule — added `RestraintCommand.ReleaseAllBoundAnimationsForPanic()`, wired into `PanicHandler` as a new step (this tracking lives in `RestraintCommand`, not `RestrictionRuleManager`, so it needed its own panic hook and a new `RestraintCommand` dependency on `PanicHandler`)
- [x] 3.8 Refuse saving a rule assignment for any of the three new kinds with no `AnimationId` chosen, and verify the UI/command path blocks it with a visible reason

## 4. Rename Gag to Gagged

- [x] 4.1 Update every user-facing string ("Gag chat", "Clear moodle"-style labels, help text) from "Gag"/"Gag chat" to "Gagged" without renaming the `RestraintRuleKind.GagChat` enum member, and verify existing saved gag rule assignments still load and function unchanged

## 5. UI: capture flow, new rule pickers, manual quick-command add

- [x] 5.1 Replace `DrawRestraintsModule`'s scanned-design combo with a slot dropdown (`LockableEquipSlots.All`) plus a "Capture current item" button, and verify capturing produces a named device without any prior scan
- [x] 5.2 Add Arms Cuffed / Legs Cuffed / Full Body Cuffed checkboxes to the Sub's device-capture UI and the Owner's per-quick-command rule editor, each opening `AnimationPickerWindow` (or an equivalent picker) to choose an animation, and verify a chosen animation is saved with the rule
- [x] 5.3 Add a manual "Add Command" input + button to the Owner's Restraints quick-command section (mirroring `DrawTitleQuickSection`), and verify it creates a new unconfigured restraint quick command ready for rule assignment
- [x] 5.4 Remove the Restraints scan section from `SettingsWindow`'s "Scan & Export" card, including its "Scan all" participation, and verify "Scan all" still correctly scans Wardrobe/Gesture/Moodles

## 6. Cross-cutting validation

- [ ] 6.1 Run through every affected `collar/restraints` and `collar/catalog-sync` scenario manually or via existing test coverage, and confirm each scenario's WHEN/THEN holds — **not run**: repo has no automated test suite and a live game session is unavailable here; verified statically against the code instead (build succeeds with 0 warnings/errors after every change)
- [ ] 6.2 Full manual pass: Sub equips a bracelet, captures it as a device in the Wrists slot, assigns Arms Cuffed with a chosen animation, Owner force-applies it, Sub sees the animation engage and hold, force-release reverts it and the slot — **not run**: requires a live paired Owner/Sub game session, unavailable in this environment
- [x] 6.3 Document the breaking-change impact (existing design-based restraint devices stop working; Sub must re-capture) in the README, mirroring how the Moodles preset→status breaking change was documented
