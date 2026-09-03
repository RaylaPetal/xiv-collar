## 1. Project Scaffolding

- [x] 1.1 Create the `CollarSystem.Plugin` Dalamud project (csproj, `DalamudPackager` config, entry point `Plugin.cs`) and verify it builds and loads in-game as an empty plugin.
- [x] 1.2 Add `Penumbra.Api`, `Glamourer.Api`, Honorific IPC contracts, `ECommons`, and `FFXIVClientStructs` as project dependencies and verify the project restores/builds with them referenced.
- [x] 1.3 Add the Owner/Sub role switch to `Plugin.cs` (config-driven) with placeholder `DomWindow`/`SubWindow` UI and verify each role's window opens correctly based on config.

## 2. Pairing & Relay Foundation (`collar/pairing`, `collar/relay`)

- [x] 2.1 Stand up `CollarSystem.Relay` (minimal websocket API) modeled on the `lovense-media-hud` relay's auth/session pattern, and verify a client can connect and authenticate.
- [x] 2.2 Implement `RelayClient.cs` in the plugin (connect, send, receive) using the `{ pairingId, category, commandId, payload, timestamp }` / `{ pairingId, commandId, status, detail? }` envelope from design.md, and verify a round-trip message between two local clients.
- [x] 2.3 Implement the pairing handshake (one-time code generation on Sub side, entry + explicit accept on Owner side) and verify no command is accepted before the Sub confirms pairing, satisfying `collar/pairing` Requirement: Explicit pairing handshake.
- [x] 2.4 Implement per-category permission toggles (title/outfit/gesture/follow) in `PluginConfig.cs` and the Sub UI, checked as the first step of inbound command handling, and verify a disabled category produces a `rejected` ack while other categories still apply.
- [x] 2.5 Implement the local panic/safeword action (hotkey + command) in `PanicHandler.cs` that unpairs, reverts Glamourer, clears Honorific title, and cancels any movement lock using only local state, and verify it completes correctly with the relay connection killed.
- [x] 2.6 Add the "uninstall is the ultimate safeword" statement to the README's consent section and verify it's present before any control-surface feature ships.

## 3. Title Control (`collar/title`)

- [x] 3.1 Implement `HonorificIpc.cs` wrapping `SetCharacterTitle`/`ClearCharacterTitle` against the Sub's own `objectIndex 0`, and verify calling it changes the local nameplate title in-game.
- [x] 3.2 Implement `TitleCommand.cs` handling in `CommandDispatcher.cs` (permission check, apply, ack) and the Owner-side title command UI, and verify an end-to-end Owner→Sub title command applies and acks correctly.
- [x] 3.3 Wire title-clear into `PanicHandler.cs` and verify a title set by an Owner is cleared when the Sub triggers panic, satisfying `collar/title` Requirement: Title reverts on panic or unpair.

## 4. Outfit Control (`collar/outfit`)

- [x] 4.1 Implement `GlamourerIpc.cs` wrapping `SetItem`/`ApplyState`/`UnlockState` against the Sub's own `objectIndex 0`, and verify each call changes local equipped appearance in-game.
- [x] 4.2 Implement `OutfitCommand.cs` handling (permission check, apply with `key` + `ApplyFlag.Lock`, ack) and the Owner-side outfit command UI, and verify an end-to-end Owner→Sub outfit command applies, locks, and acks correctly.
- [x] 4.3 Implement lock enforcement so the Sub cannot revert/change a locked outfit without the Owner's key, and an Owner-issued unlock command, and verify both the block and the unlock path with an integration test.
- [x] 4.4 Wire outfit-lock release into `PanicHandler.cs` and verify a locked outfit is released (without needing the Owner's key) when the Sub triggers panic, satisfying `collar/outfit` Requirement: Locked outfit released on panic or unpair.

## 5. Panic/Safety Hardening

- [ ] 5.1 Write an integration test exercising the full panic sequence (unpair + Glamourer revert + title clear + movement-lock cancel) across all categories implemented so far, and verify it passes with the relay both up and down.
- [x] 5.2 Review and finalize the ToS disclosure section in the README (chat-injection and input-hook risk, per design doc §5) and verify it is visible before a Sub can enable gesture or follow permissions (e.g. an in-UI acknowledgement gate).

## 6. Gesture Control (`collar/gesture`)

- [x] 6.1 Implement Penumbra mod scanning (`GetModList`/`GetChangedItems`) and automatic emote resolution into a gesture catalog, and verify a mod with recognized emote animation files is correctly labeled without manual tagging.
- [x] 6.2 Implement the mod-folder allowlist config for scoping which mods are scanned, and verify only allowlisted-folder mods appear in the generated catalog.
- [x] 6.3 Implement unresolved-mod surfacing in the Sub UI (multi-select / manual assignment fallback) and verify a mod with no recognized emote mapping appears as unresolved rather than being dropped.
- [x] 6.4 Implement catalog relay to the paired Owner (send + local cache on Owner's client) and verify the Owner's client shows the current catalog and can browse it offline.
- [x] 6.5 Implement the gesture request queue on the Sub's client (Owner sends a prompt, Sub UI visibly queues it, no auto-fire) and verify a sent prompt does not trigger anything until the Sub confirms.
- [x] 6.6 Implement gesture trigger on Sub confirmation (activate mapped mod/collection via `TrySetMod`/`AddTemporaryMod`, fire the emote via `ECommons.Automation.Chat.SendMessage`) and verify an end-to-end Owner-prompt → Sub-confirm → emote-plays flow in-game.

## 7. Follow/Leash Control (`collar/follow`)

- [x] 7.1 Implement the dedicated "movement-lock" permission toggle, separate from the other three categories, and verify a follow command is rejected when only the other permissions are enabled.
- [x] 7.2 Implement movement input blocking via `Hook<T>` on `IsInputIdPressed`/`IsInputIdDown`/`IsInputIdHeld` for the movement input IDs, gated by an active lock flag, and verify movement keys have no effect on the character while locked.
- [x] 7.3 Implement suppression of movement-cancels-follow logic (`NoAutoMoveActive`/`NoUnfollowingActive`-equivalent) and verify a key press during an active leash does not interrupt follow/auto-move.
- [x] 7.4 Implement lock release on Owner release-command, on panic, and on unpair, restoring normal input handling, and verify all three release paths in an integration test.
- [x] 7.5 Add patch-compatibility guarding: verify the required hook signatures resolve at plugin load, and fail closed (movement lock disabled, never silently left active) if they don't.

## 8. Documentation

- [x] 8.1 Write the top-level README covering setup, the consent model, and all ToS caveats referenced above, and verify it accurately reflects the shipped feature set.
