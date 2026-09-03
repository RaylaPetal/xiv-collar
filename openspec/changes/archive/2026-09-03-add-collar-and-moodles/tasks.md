## 1. Glamourer: read the currently-equipped Neck item

- [x] 1.1 Add a `GlamourerIpc.GetCurrentNeckItem()` wrapping Glamourer's `GetState` IPC subscriber, extracting the Neck slot's item id and stain bytes from the returned state, and verify it returns the actual currently-equipped Neck item when called against a live character (manual test: equip a known item, call, compare ids).

## 2. Config model (`collar/collaring`, `collar/moodles`)

- [x] 2.1 Add a `CollarState` class to `PluginConfig` (configured item id, stains, lock key, force-locked flag - same shape as the existing outfit lock state) and a `PluginConfig.Collar` property, and verify it round-trips through save/reload.
- [x] 2.2 Add a `MoodlesMapping` class (a `LocalCatalog` of preset id/name pairs) and a `PluginConfig.MoodlesMapping` property, and verify it round-trips through save/reload.
- [x] 2.3 Add `Collar` and `Moodles` fields to `PermissionSet`, defaulting to `false`, and verify existing configs without these fields load with both defaulted off (no crash on missing JSON keys).

## 3. Collar command and safety wiring (`collar/collaring`)

- [x] 3.1 Add `SubRuntimeState.CollarLockKey`/`CollarForceLocked`, mirroring `OutfitLockKey`/`OutfitForceLocked` exactly, reset in `Reset()`, and verify `Reset()` clears both.
- [x] 3.2 Create `CollarCommand` with `CaptureCurrentAsCollar()` (reads `GlamourerIpc.GetCurrentNeckItem()`, saves into `PluginConfig.Collar`, refuses while `CollarForceLocked`), `ForceApply()` (applies+locks the configured item via `GlamourerIpc.SetItem(ApiEquipSlot.Neck, ...)` with a freshly generated key, sets `CollarForceLocked = true`), and `ForceUnlock()` (releases using the stored key, clears the flag), and verify each against `collar/collaring`'s requirements (capture-while-unlocked-only, apply-uses-local-key, unlock-uses-stored-key).
- [x] 3.3 Wire `PanicHandler` to unconditionally release an active collar lock (same unconditional pattern as the existing outfit/title release steps), and verify panic still completes fully even if the collar-release step itself throws (each step stays isolated in its own try/catch, per the existing pattern).
- [x] 3.4 Wire pairing acceptance (`PairingCommand.AcceptPeer`'s call site in `ChatCommandListener.AcceptPending`) to call `CollarCommand.ForceApply()` immediately after `Paired` is set true, only when `Permissions.Collar` is enabled and a collar item is configured, and verify accepting with no collar configured (or the permission off) applies nothing, per `collar/pairing`'s new scenarios.

## 4. Moodles IPC and command (`collar/moodles`)

- [x] 4.1 Create `MoodlesIpc` wrapping the confirmed Moodles IPC labels (`GetPresetsInfoListV2`, `ApplyPresetByPlayerV2` or the confirmed-correct apply-by-local-player call, `ClearStatusManagerByPlayerV2`) via `GetIpcSubscriber<...>`, verifying actual parameter/return shapes against a running Moodles instance rather than assuming them (design.md's Open Question), and verify a scan against a Moodles install with at least one saved preset returns that preset's id and name.
- [x] 4.2 Create `MoodlesCommand` with `Rescan()` (populates `MoodlesMapping.LocalCatalog` from `MoodlesIpc`), `ForceApply(string presetName)` (case-insensitive match against the local catalog; no-op if unmatched), and `ForceClear()`, and verify each against `collar/moodles`'s requirements (name-matching, unrecognized-name no-op, immediate application with no confirmation step).

## 5. Chat grammar (`collar unlock`, `moodle apply|clear`)

- [x] 5.1 Add `"collar"` and `"moodle"` to `ChatCommandListener.ReservedCategoryWords`, and verify a Sub alias can no longer be saved under either name (`CollarWindow`'s reserved-word check already reads this list, so no separate UI change needed - just verify it rejects the new words too).
- [x] 5.2 Add `HandleForceCollar`/`HandleForceMoodle` dispatch cases in `ChatCommandListener.Resolve`, gated on `Permissions.Collar`/`Permissions.Moodles` respectively: `collar unlock` -> `CollarCommand.ForceUnlock()`; `moodle apply <name>` -> `MoodlesCommand.ForceApply(name)`; `moodle clear` -> `MoodlesCommand.ForceClear()`, and verify each command applies only when the matching permission is enabled, per `collar/collaring` and `collar/moodles`'s permission-gate requirements.

## 6. UI

- [x] 6.1 Add a **Collar** nav tab to `CollarWindow` (capture-current-Neck-item button, current configured item display, clear button disabled while locked, lock-status display), and verify it reflects `PluginConfig.Collar`/`SubRuntimeState.CollarForceLocked` correctly and disables editing while locked.
- [x] 6.2 Add a Moodles scan card to `SettingsWindow` (Rescan button, scan-result feedback, "Copy names" button), mirroring the existing Wardrobe/Gesture scan cards, and verify it round-trips a rescan and produces a correct clipboard export.
- [x] 6.3 Add a Moodles Quick Command section to `CollarWindow`'s Owner tab (Add from clipboard with the existing garbage-paste validation, one-click Send/Copy per imported preset name, Clear all), mirroring the Outfit/Gesture Quick Command sections exactly, and verify imported names produce working `moodle apply <name>` Quick Commands.
- [x] 6.4 Add a "Collar unlock" fixed Quick Command row (mirrors "Unlock outfit"/"Clear title") to the Owner tab, and verify it composes `collar unlock` and Sends/Copies correctly.
- [x] 6.5 Add **Collar** and **Moodles** checkboxes to `CollarWindow`'s Permissions tab with `HelpMarker`s explaining each, and verify toggling each persists and gates the corresponding command per task 5.2's verification.

## 7. Documentation

- [x] 7.1 Update the README's "How commands travel" / consent model sections to describe the collar-at-pairing behavior and the Moodles override commands, and the Project layout section to mention the new `CollarCommand`/`MoodlesCommand`/`MoodlesIpc` files, and verify it accurately describes the shipped flow.

## 8. Collar lock override (post-implementation addition)

- [x] 8.1 Add `collar lock` (no item argument) alongside `collar unlock` in `ChatCommandListener.HandleForceCollar`, calling `CollarCommand.ForceApply()`; update `ForceApply()` to reuse the existing lock key when already locked (Glamourer needs the current key to modify an already-locked slot) rather than always generating a new one, and verify re-locking after an unlock, and locking for the first time outside pairing acceptance, both work per `collar/collaring`'s new "Owner can (re-)apply the collar directly" requirement.
- [x] 8.2 Add a "Collar lock" fixed Quick Command row alongside "Collar unlock" in `CollarWindow`'s Owner tab, and verify it composes `collar lock` and Sends/Copies correctly.
