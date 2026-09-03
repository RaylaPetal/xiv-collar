## 1. Glamourer IPC surface

- [x] 1.1 Add `GlamourerIpc.GetDesignEquipSlots(Guid designId)` reading `GetDesignJObject`'s `Equipment.<Slot>.Apply` for each of the 10 gear slots, defensive against a missing/malformed shape (treat as not-applied rather than throw); verify against a scanned design with a known subset of slots enabled.
- [x] 1.2 Add `GlamourerIpc.GetEquipSlotValue(ApiEquipSlot slot)` generalizing `GetCurrentNeckItem`'s existing `Equipment.Neck.{ItemId,Stain,Stain2}` parsing to any of the 10 slots, returning null on any failure; verify it still returns the correct Neck value where `GetCurrentNeckItem` did.
- [x] 1.3 Add `GlamourerIpc.SetItemOnce(ApiEquipSlot slot, ulong itemId, IReadOnlyList<byte> stains)` wrapping `SetItem` with `ApplyFlag.Once` only (never `Lock`); verify the applied slot is not locked in Glamourer's own state afterward.
- [x] 1.4 Add `GlamourerIpc.RevertToAutomationEquipmentOnly()` wrapping `RevertToAutomation` with `ApplyFlag.Equipment` only (excluding `Customization`); verify a call leaves customization/body data untouched.
- [x] 1.5 Expose Glamourer's `StateChangedWithType` and `StateFinalized` IPC events through `GlamourerIpc` as one combined `LocalPlayerStateChanged` event (subscribe to both, forward only the local player's own actor-pointer firings); verify it fires both for a single manual slot edit through Glamourer's own UI and for a full design apply (corrected from `StateFinalized` alone after in-game testing showed individual edits never raise it - see design.md).
- [x] 1.6 Remove `GlamourerIpc.Unlock`, `Revert`, and the `ApplyFlag.Lock` branches in `ApplyDesign`/`SetItem`, since nothing in this plugin locks through Glamourer's own state anymore; verify no remaining caller references them and the build succeeds.

## 2. SlotLockManager

- [x] 2.1 Add `PluginConfig.SlotLocks` (`List<SlotLockEntry { ApiEquipSlot Slot; string Owner; ulong ItemId; byte Stain; byte Stain2 }>`) and remove `PluginConfig.Locks`/`LockState`; verify a config file saved before this change still deserializes (the old fields are simply dropped, `SlotLocks` defaults empty).
- [x] 2.2 Create `SlotLockManager` in `CollarSystem.Plugin.Safety`, loading `PluginConfig.SlotLocks` into an in-memory `Dictionary<ApiEquipSlot, SlotLock>` at construction, with `TryLock(owner, slots)`, `Release(owner)`, and `HasLock(owner)`; verify `TryLock` succeeds for a fresh slot, re-locking the same owner's own slot succeeds, and locking a slot already owned by a different owner fails without changing anything.
- [x] 2.3 Wire `SlotLockManager` to `GlamourerIpc`'s combined `LocalPlayerStateChanged` event with the reapply-on-divergence enforcement loop and an `isEnforcing` reentrancy guard; verify manually unequipping a single locked piece through Glamourer's own UI reverts back to the locked value (not just a full design/gearset change).
- [x] 2.4 Implement `Release(owner)`'s snapshot-all-slots → `RevertToAutomationEquipmentOnly` → reapply-every-other-slot sequence; verify releasing one owner's lock leaves a different owner's still-active lock and an unrelated freely-customized slot unchanged, while only the released slot picks up Glamourer's automation-managed value.
- [x] 2.5 Construct `SlotLockManager` in `Plugin.cs` (after `Configuration`/`GlamourerIpc` are available) and dispose it (unsubscribing `StateFinalized`) alongside the plugin's other IPC teardown; pass it into `CollarCommand`, `OutfitCommand`, and `PanicHandler`.

## 3. Collar/Outfit command migration

- [x] 3.1 Update `CollarCommand.ForceApply`/`ForceUnlock` to call `SlotLockManager.TryLock("Collar", { Neck: <configured item> })` / `Release("Collar")` instead of `GlamourerIpc.SetItem(..., locked: true)`/`Unlock`; verify Collar Test Lock/Unlock still succeed and leave every other slot untouched.
- [x] 3.2 Update `OutfitCommand.Apply`/`Unlock`/`ForceApply`/`ForceUnlock` to resolve a design's slots via `GlamourerIpc.GetDesignEquipSlots` before calling `SlotLockManager.TryLock("Outfit", ...)`/`Release("Outfit")`; keep the existing `OutfitForceLocked` bool gate (the Owner-override precedence over the Sub's own alias-triggered Apply/Unlock) exactly as-is, since it is plugin-level bookkeeping independent of the Glamourer key model being replaced; verify Outfit Test Apply/Unlock still succeed and only affect the applied design's own slots.
- [x] 3.3 Remove `SubRuntimeState.OutfitLockKey`/`CollarLockKey` (superseded by `SlotLockManager.HasLock`); keep `OutfitForceLocked`/`CollarForceLocked` as plain persisted flags; verify the build succeeds with no remaining references to the removed properties.

## 4. Panic

- [x] 4.1 Update `PanicHandler.Panic()`'s outfit/collar steps to perform one unconditional whole-actor Glamourer revert (no snapshot/restore dance - see design.md's "Panic keeps a single, unconditional whole-actor revert") plus clearing every tracked entry in `SlotLockManager` and resetting `OutfitForceLocked`/`CollarForceLocked`; verify panic still releases every active slot lock regardless of which categories were locked.

## 5. Documentation and verification

- [x] 5.1 Update README's Consent model and Automation risk sections to describe per-slot lock scope (Collar locks only Neck; Outfit locks only the applied design's own slots; every other slot stays freely editable throughout), replacing language that implied a locked category restricted the whole character.
- [x] 5.2 Build the solution and validate the OpenSpec change strictly; resolve all warnings/failures.
- [x] 5.3 Perform an in-game smoke test covering: locking the collar restricts only Neck while every other slot (including Glamourer's own UI) stays freely editable; locking an outfit design restricts only that design's own slots; manually changing a locked slot through Glamourer's own UI is reverted back automatically; Collar and Outfit can be locked simultaneously without either refusing or fighting the other; a plugin reload while a lock is active still allows it to be released afterward through its normal release action; and panic releases every active lock at once.
