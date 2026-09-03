## Why

Testing the local pre-pair Test controls in-game surfaced several real bugs and rough edges: outfit unlock leaves the character stuck in its manually-applied look instead of returning to Glamourer's automation, the Collar Lock test crashes on an IPC type mismatch, gesture playback doesn't reliably fire right after a redraw and never cleans up its temporary mod override afterward, and the Test controls themselves are confusing (unlabeled, permanent feedback, no way to hide them) or use unclear copy (the collar capture button). These need to be fixed and polished before the local-testing feature is trustworthy day to day.

## What Changes

- Fix the Collar Lock (and Owner `collar lock`) crash: `CollarCommand.ForceApply` passes a `byte[]` where Glamourer's `SetItem` IPC expects `IReadOnlyList<byte>`, and Dalamud's IPC transport can't round-trip a `byte[]`'s base64 JSON encoding back into that interface type - it must pass a `List<byte>` instead.
- Change outfit Unlock (Sub's own alias-triggered unlock, the Owner's `outfit unlock` override, and the new local Test) to revert the character to Glamourer's automation-managed appearance instead of only removing the lock and leaving the manual design applied. **BREAKING**: changes the already-archived `collar/outfit` unlock behavior.
- Add a short delay between the Gesture temporary-mod redraw and playing its tied trigger, so the animation reliably starts after the redraw settles instead of racing a visible flicker.
- Add cleanup for a gesture's temporary Penumbra override: a manual Reset control, plus an automatic revert after roughly 30 seconds of no further gesture activity, so a played gesture's temporary mod settings don't linger indefinitely.
- Make local Test feedback transient: each result clears itself a few seconds after showing, instead of persisting until overwritten by the next test.
- Give every local Test control an action-specific label (e.g. "Test Lock" / "Test Unlock") instead of a bare "Test", so its effect is clear without hovering a tooltip.
- Add a setting to hide all local Test controls from the Sub-facing UI entirely.
- Relabel the Collar module's "Capture my current Neck item as my collar" button to a shorter, clearer "Save Collar".

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `collar/outfit`: Unlock (alias-triggered, Owner override, and local Test) reverts the outfit to Glamourer's automation-managed state instead of only removing the lock.
- `collar/gesture`: A played gesture's temporary Penumbra mod override is now revertible - manually via a Reset control, and automatically after a period of inactivity.
- `collar/ui-organization`: Local Test feedback is transient (auto-clears), each Test control is individually labeled by its action, and the Sub can hide all local Test controls via a setting.

## Impact

- `CollarCommand.ForceApply` (the `byte[]`/`IReadOnlyList<byte>` IPC crash).
- `OutfitCommand.Unlock`/`ForceUnlock`, and `LocalTestCoordinator.TestOutfitUnlock`.
- `GestureCommand.Execute`/`Play` (redraw-to-play timing) and a new temporary-override reset path, surfaced in `CollarWindow`'s Gesture module.
- `CollarWindow`'s and `SettingsWindow`'s Test button feedback rendering (transient timing, per-action labels) and Collar module's capture button label.
- `PluginConfig` (a new "hide local Test controls" setting) and every Test button's visibility check in `CollarWindow`/`SettingsWindow`.
