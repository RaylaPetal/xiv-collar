## 1. Bug fixes

- [x] 1.1 Change `CollarCommand.ForceApply`'s stains argument from a `byte[]` to a `List<byte>`; verify Collar Lock (Owner `collar lock` and the local Test) applies and locks without an `IpcTypeMismatchError`.
- [x] 1.2 Change `OutfitCommand.Unlock()`/`ForceUnlock()` to call Glamourer's `RevertToAutomation(key)` followed by `Unlock(key)` instead of `Unlock(key)` alone (see design.md - `RevertToAutomation` reapplies the automation look but, confirmed by decompiling `Glamourer.dll`, never releases the lock itself); verify the Sub's own unlock alias, the Owner's `outfit unlock` override, and the outfit Test Unlock all revert the character to Glamourer's automation-managed appearance and release the lock.
- [x] 1.3 Add a frame-driven delay between a gesture's Penumbra redraw and playing its tied trigger, advanced from the existing `Framework.Update` hook rather than blocking or backgrounding any IPC call; verify gesture playback reliably fires for both a slash-emote and a pose trigger after the redraw visually settles.
- [x] 1.4 (found during in-game testing) Persist `SubRuntimeState`'s Outfit/Collar lock keys and force-locked flags to `PluginConfig.Locks` instead of keeping them in-memory only; verify a plugin reload between locking and unlocking no longer permanently strands the character locked in Glamourer with no key anywhere (including `/collarpanic`) able to recover it.

## 2. Gesture temporary-activation cleanup

- [x] 2.1 Track the currently active temporary gesture activation (collection + mod directory) on `GestureCommand` and add a method that reverts it via Penumbra's existing `TryRemoveTemporarySettings`; verify reverting restores the mod's saved settings and clears the tracked state.
- [x] 2.2 Add a manual Reset control in the Gesture module that reverts the active temporary activation on demand; verify it is only enabled while an activation is active and has no effect when none is active.
- [x] 2.3 Add an automatic ~30-second idle-timeout, advanced from the same `Framework.Update` hook, that reverts the active temporary activation if no further gesture is played; verify a new gesture play before the timeout restarts it instead of stacking timers, and verify playing a different mod's gesture first reverts the previous mod's temporary settings.

## 3. Local Test polish

- [x] 3.1 Give every local Test control an action-specific label (e.g. "Test Lock", "Test Unlock", "Test Apply", "Test Clear", "Test Play", "Test Engage", "Test Release") in place of the generic "Test"; verify every Sub-facing Test button's label alone identifies its action without hovering a tooltip.
- [x] 3.2 Make Test result feedback clear itself automatically a few seconds after being shown instead of persisting indefinitely; verify a result is still visible immediately after the click and gone after the timeout elapses, without needing another test to overwrite it.
- [x] 3.3 Add a "Hide local Test controls" setting (default off) and gate every Test control's rendering behind it centrally; verify enabling it removes every Test control from the Sub-facing UI without affecting any other control, and disabling it restores them.

## 4. Copy polish

- [x] 4.1 Relabel the Collar module's collar-capture button to "Save Collar"; verify its click behavior, tooltip, and disabled state while locked are unchanged.

## 5. Documentation and verification

- [x] 5.1 Update README and inline help for outfit-unlock-reverts-to-automation, gesture temporary-activation cleanup (manual Reset and automatic idle-timeout), transient Test feedback, per-action Test labels, and the new hide-Test-controls setting.
- [x] 5.2 Build the solution and validate the OpenSpec change strictly; resolve all warnings/failures.
- [x] 5.3 Perform an in-game smoke test covering: Collar Lock/Unlock Test succeeding without error, outfit unlock (Sub alias, Owner override, and Test) reverting to automation, gesture playback reliably firing after redraw for both a slash-emote and a pose trigger, the gesture Reset control and idle-timeout both reverting temporary settings, Test feedback auto-clearing, per-action Test labels, the hide-Test-controls setting, and the renamed Save Collar button.
