## Context

See proposal.md for motivation. Relevant existing architecture (all confirmed against the actual installed packages/plugins, not assumed):

- `GlamourerIpc` already wraps `SetItem(ApiEquipSlot slot, ulong itemId, IReadOnlyList<byte> stains, uint key, bool locked)` (Glamourer.Api 2.8.2's `SetItem` IPC subscriber) - single-slot apply+lock, unused by any command class today. `ApiEquipSlot.Neck` is a real enum member (confirmed via the shipped `Glamourer.Api.dll`). This is the same single-slot approach the user pointed to in GagSpeak.
- Glamourer.Api also exposes `GetState` (full state query, JObject) - not yet wrapped. Needed to read the item currently equipped in the Sub's own Neck slot, so the Sub never types a raw item ID.
- The installed Moodles plugin (1.1.3.5) exposes an IPC surface confirmed via its shipped DLL, includes (exact names): `GetPresetsInfoListV2`, `GetMyPreset`, `ApplyPresetByPlayerV2` / `ApplyPresetByNameV2` / `ApplyPresetByPtrV2`, `ClearStatusManagerByPlayerV2` / `ClearStatusManagerByPtrV2`. There is no `Moodles.Api` NuGet package in this solution today (unlike Glamourer/Penumbra/Honorific) - integration goes through raw `IDalamudPluginInterface.GetIpcSubscriber<...>("Moodles.<Name>")` calls, the same pattern Honorific already uses in `HonorificIpc`.
- `ChatCommandListener.ReservedCategoryWords` currently holds `["title", "outfit", "gesture"]`, checked case-insensitively as the first token of a command; `SubWindow`'s (now `CollarWindow`'s) alias-creation forms already validate new aliases against this list.
- The outfit force-lock precedent (`OutfitCommand.ForceApply`/`ForceUnlock`, `SubRuntimeState.OutfitLockKey`/`OutfitForceLocked`) is the exact shape to reuse for collar: a locally-generated `Random.Shared.Next(1, int.MaxValue)` key, a bool flag blocking the alias-triggered path, unconditional release in `PanicHandler`.

## Goals / Non-Goals

**Goals:**
- Reuse the outfit force-lock pattern exactly for the collar lock (same key-generation, same panic-unconditional-release guarantee) rather than inventing a new lock mechanism.
- Reuse the gesture local-scan pattern for Moodles preset discovery (Sub-side only, Owner never sees raw data, only names).
- Keep both new commands (`collar unlock`, `moodle apply|clear`) inside the existing reserved-keyword override grammar - no new message format, no new transport concept.

**Non-Goals:**
- No UI inside this plugin for creating/editing Moodles presets themselves - Sub manages those in Moodles' own window, this plugin only reads and applies them.
- No support for applying an arbitrary Moodle by raw GUID typed by the Owner - only by matching a name against the Sub's own scanned catalog, consistent with how outfit/gesture overrides already work.
- No multi-item "collar look" (necklace + something else) - one Neck-slot item, matching the proposal's explicit scope.

## Decisions

**Collar capture, not manual entry.** The Sub equips the item they want (any way they like - Penumbra mod, real gear, Glamourer's own UI) and clicks "Use my current Neck item" in the new Collar tab. The plugin calls the new `GlamourerIpc.GetCurrentNeckItem()` (wraps `GetState`, extracts the Neck slot's item id + stain bytes) and stores that. Alternative considered: let the Sub type a numeric item ID directly - rejected, item IDs aren't something anyone has memorized and typing one wrong silently produces the wrong collar.

**Collar lock reuses the outfit force-lock shape, not a new lock type.** `CollarLockKey`/`CollarForceLocked` on `SubRuntimeState`, mirroring `OutfitLockKey`/`OutfitForceLocked` field-for-field. `PanicHandler` gets one more unconditional-release line, same shape as the existing outfit line. This was confirmed with the user directly: panic overrides the collar lock, no exception - matching every other lock in the plugin.

**Collar auto-apply happens inside `PairingCommand.AcceptPeer`'s call path, not only as a separate Owner command.** The collar applies automatically "as part of" accepting - `AcceptPeer` (or an immediate caller in `ChatCommandListener.AcceptPending`) checks `Permissions.Collar` and a configured collar item, and if both hold, calls `CollarCommand.ForceApply()` right after setting `Paired = true`. Revised after initial implementation: `collar lock` (no item argument - there's only ever one configured collar) also exists as a direct Owner override, calling the same `ForceApply()`, for re-attaching a collar after `collar unlock` or applying one that wasn't configured/enabled yet at pairing time. `ForceApply()` reuses the existing lock key when already locked rather than generating a new one, since Glamourer needs the current key to modify an already-locked slot.

**Moodles apply/clear is immediate, no confirmation gate - unlike Gesture.** Gesture requires Sub confirmation because it fires a real emote/animation via chat automation (`ECommons.Automation.Chat.SendMessage`), the plugin's one automation-risk-relevant action. A Moodle is a visual status icon with no automation footprint - applying/clearing it is the same risk class as title/outfit, which also apply immediately. Alternative considered: require confirmation for consistency with Gesture - rejected as unnecessary friction for a lower-risk action; can be revisited if Moodles integration turns out to have side effects beyond the status icon.

**Two new reserved keywords, `collar` and `moodle`, added to `ChatCommandListener.ReservedCategoryWords`.** Grammar: `collar lock` / `collar unlock` (no item name - there's only ever one configured collar, unlike `outfit lock <name>`); `moodle apply <preset name>` / `moodle clear`. Both parsed with the same `SplitFirstToken`/quote-stripping helpers already used for `title`/`outfit`/`gesture`.

**Moodles preset catalog mirrors `GestureMapping`'s shape**: a `MoodlesMapping` with a `LocalCatalog` (preset id/name pairs from `GetPresetsInfoListV2`), refreshed by an explicit Rescan button in Settings (no folder allowlist - Moodles presets don't have a folder-organization concept the way Penumbra mods or Glamourer designs do, so every saved preset is eligible, matching how Moodles itself presents them as one flat list).

**Owner-side UX matches the existing Outfit/Gesture Quick Command pattern exactly**: Settings gets a "Copy names" button on the Moodles scan card; `CollarWindow`'s Owner tab gets a Moodles Quick Command section with "Add from clipboard" auto-populating one button per imported preset name, Send/Copy per entry, same validation-on-import guard against pasting garbage. The Collar tab itself (Sub-side capture/clear) has no Quick Command list - there's nothing to import, it's a single local capture action.

## Risks / Trade-offs

[Moodles has no official `.Api` NuGet package in this solution, unlike Glamourer/Penumbra/Honorific] → `MoodlesIpc` calls `GetIpcSubscriber<...>` with bare string labels and hand-written parameter/return types, the same lower-safety pattern `HonorificIpc` already uses. Exact parameter shapes (does apply take a player name+world, an object pointer, or something else; what a preset "info" record contains) must be verified against Moodles' actual runtime behavior during implementation - the method *names* are confirmed from the shipped DLL, the call *signatures* are not. If a signature doesn't match what's assumed here, the affected task gets revised in place rather than silently guessed at.

[A Sub could equip something else in their Neck slot after being collared, then re-open the game/plugin] → Out of scope for this change: Glamourer's own lock already prevents changing a locked slot through Glamourer/Penumbra/this plugin; anything that bypasses Glamourer entirely (raw client memory edits, a different tool) is the same class of limitation every other lock in this plugin already has, not something new introduced here.

[Two more permission toggles (Collar, Moodles) adds more surface to `PermissionsCard`] → Same UI pattern as the existing four, no new interaction model - just two more checkboxes with `HelpMarker`s, consistent with the rest of that card.

## Migration Plan

No persisted-state migration - `CollarState`/`MoodlesMapping`/the two new `PermissionSet` fields are additive to `PluginConfig`, defaulting to "nothing configured, permission off," so existing installs load unaffected and the collar/Moodles features are opt-in from a clean-off state.

## Open Questions

- Exact Moodles IPC parameter shapes (player identifier form, preset-info record fields) - resolved during implementation by inspecting the actual IPC calls against a running Moodles instance, not a spec/design-level decision (doesn't change what this change's specs require, only how `MoodlesIpc` is written).
