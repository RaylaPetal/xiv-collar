## Context

See `proposal.md` for motivation - this covers how each fix/polish item works. All items were found by exercising the previous change's local-testing UI in-game.

Relevant existing pieces:
- `CollarCommand.ForceApply` builds its stains argument as `new byte[] { ... }` and passes it to `GlamourerIpc.SetItem(..., IReadOnlyList<byte> stains, ...)`. Dalamud's IPC transport (`CallGateChannel`) serializes the argument using its runtime type - a `byte[]` gets Newtonsoft's special-cased base64-string encoding - then deserializes it back against the declared parameter type. Deserializing a base64 string into the interface type `IReadOnlyList<byte>` has no concrete type to construct, so it throws (`IpcTypeMismatchError`). `List<byte>` doesn't get that special-cased encoding; Newtonsoft serializes it as a plain JSON array, which round-trips into `IReadOnlyList<byte>` without issue.
- `GlamourerIpc.Revert(key)` (`RevertState` IPC) already exists and is what `PanicHandler` uses for the outfit slot - but it reverts to Glamourer's bare *game* state, not automation, despite the name suggesting otherwise. Glamourer separately exposes `RevertToAutomation` (`RevertToAutomation` IPC), which reverts to the automation-managed state, but - per Glamourer's own decompiled `StateApi.RevertToAutomation` - only *re-locks* the state if asked to and never unlocks it, so it doesn't release an existing lock on its own; a following `UnlockState` call (`GlamourerIpc.Unlock`) is required to actually free the state. `OutfitCommand.Unlock`/`ForceUnlock` currently call `GlamourerIpc.Unlock(key)` alone, which only clears the lock and leaves the manually-applied design in place.
- `GestureCommand.Execute` calls `penumbra.TrySetTemporarySettings(...)`, then `penumbra.TryRedrawLocalPlayer()`, then immediately `Play(trigger)` in the same synchronous call - no gap for the redraw to visually settle before the emote/pose command fires.
- `PenumbraIpc` already wraps `RemoveTemporaryModSettings` as `TryRemoveTemporarySettings(collection, directory)`, unused by any caller today - it's the correct call to revert a temporary activation, but nothing currently tracks which collection/directory to pass it or calls it.
- `Plugin.cs` already subscribes `Framework.Update` for the panic hotkey's edge detection - a per-frame hook already runs on the game's own main thread, the only thread IPC/game-state calls are safe on.
- `CollarWindow`/`SettingsWindow` each have their own `Dictionary<string, LocalTestResult> testResults` and a `DrawTestButton(string key, Func<LocalTestResult> run)` helper (see the last change's `organize-owner-controls-and-scan-defaults`), always labeled bare `"Test##{key}"`.
- `PluginConfig` has no existing "hide UI controls" style setting to follow as precedent - this introduces the first one.

## Goals / Non-Goals

**Goals:**
- Fix the two real bugs (Collar Lock IPC crash, outfit unlock not reverting) without changing any other command's wire behavior.
- Make gesture playback and its temporary Penumbra activation reliable and self-cleaning, using only the existing per-frame update hook - no new threads, no async IPC calls.
- Make the local Test surface self-explanatory and dismissable without touching the underlying `LocalTestCoordinator` gating logic added in the previous change.

**Non-Goals:**
- Changing any Owner-facing command wire format, permission model, or pairing behavior.
- Reworking gesture catalog scanning or trigger detection.
- Persisting Test visibility per-window instead of one global setting.
- Building a generalized "deferred action" scheduler - the gesture play delay and idle-timeout are purpose-built for this one case.

## Decisions

### Fix the stains IPC crash by passing a `List<byte>`, not a `byte[]`
`CollarCommand.ForceApply` changes its stains construction from `new byte[] { ... }` to `new List<byte> { ... }`. This is the minimal fix - `GlamourerIpc.SetItem`'s parameter stays `IReadOnlyList<byte>` (unchanged, matches Glamourer's own API signature), only the concrete type passed at the one call site changes, avoiding Newtonsoft's byte-array-to-base64 special case that Dalamud's IPC round-trip can't reverse.

Alternative considered: change `GlamourerIpc.SetItem`'s signature to accept `byte[]` directly. Rejected - it would still fail the same way, since the mismatch is between the *argument's runtime type* and the *declared parameter type* at the IPC boundary, not something fixable by changing which type the wrapper method declares.

### Outfit unlock reverts to Glamourer automation, then unlocks
`OutfitCommand.Unlock()` and `ForceUnlock()` change their Glamourer call from a single `Unlock(key)` to `RevertToAutomation(key)` followed by `Unlock(key)` - not `Revert(key)`/`RevertState` as first assumed (that reverts to bare game/vanilla state, not automation), and not `RevertToAutomation` alone as assumed next. Decompiling Glamourer's own `StateApi.RevertToAutomation` (`Glamourer.dll`) shows it reapplies the automation-managed look via `ReapplyAutomationState`, then only *re-locks* the state if the `Lock` flag is passed (`ApiHelpers.Lock`) - it never calls the state's own `Unlock`, so a lock present before the call survives it untouched. A following `Unlock(key)` call is required to actually release it, exactly like `UnlockState`'s own implementation (`state.Unlock(key)`) that a bare `RevertToAutomation` call never reaches. `runtimeState.OutfitLockKey`/`OutfitForceLocked` clearing stays exactly as it is today - only the Glamourer call sequence changes.

This is a **BREAKING** change to the already-archived `collar/outfit` spec's "Owner unlocks the outfit" scenario (see proposal). Collar's own unlock (`CollarCommand.ForceUnlock`) is intentionally left untouched - `collar/collaring`'s spec explicitly documents collar-unlock as "releases the lock using the key that applied it" with the item staying equipped, a deliberately different contract from outfit's "revert to automation."

Alternative considered: `RevertToAutomation(key)` alone, treating it as equivalent to `Revert`/`RevertState`'s "reverts and unlocks in one call" shape. Rejected after decompiling `Glamourer.dll` - confirmed it reapplies automation but never unlocks, so the lock (and the outfit alias list's "(locked)" indicator on the underlying Glamourer state) would persist indefinitely with no way to release it besides panic.

Alternative considered: keep `Unlock` alone and additionally call `Revert`/`RevertState` right after (or before). Rejected - `RevertState` targets bare game/vanilla state, not automation, so it doesn't satisfy the "reverts to Glamourer's automation-managed state" contract regardless of ordering; only `RevertToAutomation` + `Unlock` together satisfy both "look" and "lock released."

### Defer gesture playback and idle-timeout through the existing Framework.Update tick
`GestureCommand` gains two pieces of frame-driven state, both advanced from a new `GestureCommand.OnFrameworkUpdate()` called from `Plugin.OnFrameworkUpdate` (the same hook already driving the panic hotkey):
- **Pending play**: `Execute(entry)` still calls `TrySetTemporarySettings` and `TryRedrawLocalPlayer()` synchronously as today, but instead of calling `Play(trigger)` immediately, it records `(trigger, readyAtTicks)` using `Environment.TickCount64` plus a fixed ~500ms delay. `OnFrameworkUpdate()` calls `Play(trigger)` and clears the pending entry once `TickCount64` passes `readyAtTicks`. Because this runs on the framework thread (not a background `Task`), `Play`'s `Chat.SendMessage`/`PlayerState`/FFXIVClientStructs calls stay exactly as thread-safe as they are today - no `async`/`Task.Delay` introduced, avoiding the risk of calling unsafe game-state code off the main thread.
- **Active temporary activation + idle timeout**: `Execute(entry)` also records `(collection, modDirectory, idleUntilTicks)`, refreshed on every successful play (satisfies "new gesture play resets the timeout"). If a different mod directory was already active, its temporary settings are removed via `TryRemoveTemporarySettings` before the new one is applied, so switching gestures never leaves an unrelated mod's temporary state behind. `OnFrameworkUpdate()` reverts (and clears) the active entry once `TickCount64` passes `idleUntilTicks` with no intervening play. A new public `ResetActiveTemporary()` performs the same revert on demand for the manual Reset control, and a public `HasActiveTemporary`-style read lets `CollarWindow` show/enable the Reset control only when there's something to revert.

Alternative considered: use `Task.Delay` + `Plugin.FireAndForget` for the play delay. Rejected - `Task.Delay`'s continuation resumes on a thread-pool thread by default, and the existing `Play`/redraw calls are not documented or exercised as safe to call off Dalamud's framework thread; a frame-counted delay guarantees the same thread the rest of `GestureCommand` already runs on.

Alternative considered: revert every gesture's temporary activation immediately after playing it (no idle window at all). Rejected - the temporary settings need to still be active while the emote/pose animation is actually playing; reverting them immediately after firing the trigger command would very likely un-apply the visual mid-animation.

### Test control polish stays inside the existing `DrawTestButton` helpers
- **Per-action labels**: `DrawTestButton` gains a `label` parameter (e.g. `"Test Lock"`, `"Test Unlock"`, `"Test Apply"`, `"Test Clear"`, `"Test Play"`, `"Test Engage"`, `"Test Release"`) used in place of the bare `"Test"`; every call site names its own action. No change to `LocalTestCoordinator` - it already returns an action-specific result message, only the button's own caption was generic.
- **Transient feedback**: the `testResults` dictionaries in `CollarWindow`/`SettingsWindow` change value type from `LocalTestResult` to `(LocalTestResult Result, long ShownAtTicks)`. `DrawTestButton` renders the stored result only while `Environment.TickCount64 - ShownAtTicks` is under a fixed ~4-second window, and prunes it once expired - no timer/callback machinery, just a per-draw check against the same tick clock used for the gesture delay.
- **Hide-all setting**: a new `PluginConfig.HideTestControls` (`bool`, defaults `false`) is checked once at the top of each `DrawTestButton` helper - when true, it draws nothing and returns, leaving every call site untouched. The setting itself is a checkbox in `SettingsWindow` (grouped with the other Sub-facing toggles), off by default so existing behavior is unchanged until a Sub opts in.

Alternative considered: a per-category "hide tests" toggle instead of one global setting. Rejected - proposal asks for hiding Test controls entirely, and a single global setting is simplest to reason about and to explain in the UI/README.

### "Save Collar" is a pure label change
`CollarWindow`'s `"Capture my current Neck item as my collar"` button text changes to `"Save Collar"`; its click handler, tooltip, and behavior are unchanged. Not spec-worthy (copy only), tracked as a task.

## Risks / Trade-offs

- [A fixed ~500ms play delay might still be too short/long on some systems] → Use a single named constant so it's a one-line tune; not exposed as a setting since it's an internal timing detail, not user-facing behavior.
- [Idle-timeout could revert a temporary activation while its animation is still visibly looping] → 30s comfortably exceeds every currently-supported trigger's play length (one-shot emotes and static sit/ground-sit/doze poses); a new play before expiry resets the timer, and the manual Reset control gives explicit early control either way.
- [Changing outfit Unlock to Revert changes already-archived, previously-agreed spec behavior] → Called out as **BREAKING** in the proposal and reflected as a MODIFIED requirement in `collar/outfit`'s delta; README/help text updated in tasks so the new behavior is documented, not silently changed.
- [A global "hide Test controls" setting could hide a gating-failure explanation a Sub actually wants to see while debugging] → Off by default, one click to re-enable, and it only hides the controls themselves - the underlying permission/acknowledgement gates in `LocalTestCoordinator` are completely unaffected.
- [Discovered live during in-game testing: `SubRuntimeState.OutfitLockKey`/`CollarLockKey` were in-memory only, so a plugin reload between locking and unlocking permanently stranded the character locked in Glamourer - even `/collarpanic` couldn't recover it, since it uses the same lost key] → Both keys (and their force-locked flags) now persist through a new `PluginConfig.Locks` and are read/written there directly by `SubRuntimeState`, saved on every change; a reload now survives with the lock still recoverable. Scoped to just these four fields - Title/movement-lock state has no equivalent externally-persisted lock to lose track of, so it stays in-memory only, unchanged.
- [Glamourer's lock (`ActorState.Combination`, confirmed via decompiling `Glamourer.dll`) is one field covering the *entire* actor state, not per-slot - locking the collar via `SetItem(..., locked: true)` locks every other slot too, not just Neck, despite `collar/collaring`'s "resists casual removal" language reading as collar-scoped] → Out of scope for this change; achieving true per-slot locking (the way GagSpeak's restraint sets appear to, by continuously re-applying an item with `ApplyFlag.Once` rather than using Glamourer's own lock) would need an active reapplication/watch loop, not a small fix - a real redesign of what "locked" means here, tracked as a follow-up rather than folded in silently.

## Migration Plan

No persisted-data migration is needed for any item here - `PluginConfig.HideTestControls` is a new field that deserializes to its `false` default for every existing config file, and every other change here is a behavior/timing fix that takes effect the moment the new build runs, with nothing to convert. Rollback simply reverts to the previous build; the only user-visible regression on rollback is the Collar Lock crash and the non-reverting outfit unlock returning, both already-known issues.
