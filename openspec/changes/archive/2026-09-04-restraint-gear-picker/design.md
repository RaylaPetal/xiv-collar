## Context

Restraint devices are stored as `RestraintDeviceDefinition { Id, Slot (ApiEquipSlot), ItemId (ulong), Stain, Stain2, Name, Rules }` - this schema already fits a picker-based flow with no changes. Today only one code path fills it in: `RestraintCommand.CaptureCurrentAsDevice` reads `GlamourerIpc.GetEquipSlotValue(slot)` off the live character, so the Sub must already be wearing the piece. `GlamourerIpc.SetItemOnce(slot, itemId, stains)` (used to *apply* a device) is confirmed to accept any valid item id unconditionally - Glamourer's own IPC contract does not require the item to be equipped or even owned first, which is what makes a picker-first flow possible at all.

There is an existing, proven UI pattern for "search and pick one thing from a large game data set": `AnimationPickerWindow.cs`, opened via `plugin.AnimationPickerWindow.Open(callback)`, with a text filter over a scrollable `ImRaii.Child` list. The new item picker follows the same shape, backed by `Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>()` instead of the mod/animation catalog.

See proposal.md - Why/What Changes for the motivating gap (Sub must own+wear a piece to capture it; Owner must know an exact Sub-chosen name) and the two confirmed behavior changes (replace equip-first capture; add Owner ad-hoc authoring).

## Goals / Non-Goals

**Goals:**
- One reusable item-by-slot picker usable from both the Sub's capture UI and the Owner's new ad-hoc-device UI.
- Sub capture no longer touches live Glamourer state at all.
- Owner can send a fully self-contained restraint device (slot + item + rules) with no Sub-side name lookup.

**Non-Goals:**
- Dye/stain selection UI - ad-hoc and picker-captured devices are undyed (stain 0/0); the fields already exist for a later dye-picker addition.
- Changing how already-captured (pre-existing) devices are stored, applied, released, or conflict-checked - only how a device's slot+item gets *chosen* changes.
- Removing or changing the existing name-based Owner flow (import/manual "Add Command" + per-name rule assignment) - it stays as an alternative to the new ad-hoc path.

## Decisions

**Item enumeration source**: `Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>()`, filtered per chosen slot by the matching non-zero `EquipSlotCategory` field (confirmed field names via other installed Dalamud plugins' compiled Lumina structs: Head, Body, Legs, Hands, Feet, Ears, Neck, Wrists, FingerL, FingerR for the 10 lockable slots already enumerated by `LockableEquipSlots.All`). `Item.Name` is a `ReadOnlySeString` and needs `.ExtractText()`; `Item.RowId` is the item id sent over the wire and stored as `ItemId`. Alternative considered: hand-authored per-slot item lists - rejected, it would need manual upkeep against game patches and can't cover "every equippable item" as the user asked.

**One shared picker component, two call sites**: `ItemPickerWindow` takes a slot and a completion callback (`Action<uint /* itemId */, string /* item name */>`), mirroring `AnimationPickerWindow.Open(callback)`. The Sub's capture button and the Owner's new "Define device" button both open it; neither call site needs slot-specific picker logic of its own. Alternative considered: separate Sub/Owner picker windows - rejected as pure duplication, since both need identical slot-filtered item search.

**Ad-hoc device identity**: an Owner-authored ad-hoc device has no Sub-side name to key off, so its runtime device id (used by `RestrictionRuleManager`'s conflict tracking and by release/force-unlock) is derived deterministically from `$"adhoc:{slot}:{itemId}"`. This mirrors the existing pattern where a name-referenced quick command's ephemeral id is derived from its device name - conflict checking and revert logic don't need to change, only what id gets passed in.

**Wire grammar**: a new sub-verb, `restraint wear <slot> <itemId> "<label>" rules:<same rule grammar as lock>`, sent alongside the existing `restraint lock "<name>" rules:...`. Kept as a separate verb rather than overloading `lock` because `lock`'s whole shape is "resolve a name to a stored definition," while `wear` carries the full definition inline - conflating the two would mean every parser branch has to disambiguate a quoted name from a slot token, for no benefit. `ChatCommandListener.HandleForceRestraint` dispatches on the sub-verb the same way it already dispatches `lock` vs `unlock`.

**Sub capture path stops reading live state**: `RestraintCommand.CaptureCurrentAsDevice(slot, name, rules)` is removed; `CaptureDeviceFromItem(slot, itemId, name, rules)` takes the picker's chosen item id directly and never calls `GlamourerIpc.GetEquipSlotValue`. Applying a captured device is unaffected - `ForceApply`/`ApplyDevice` already call `SetItemOnce` with the stored item id regardless of what produced it.

## Risks / Trade-offs

- **Owner can force any equippable item without prior Sub review of that specific item** → Mitigated by the existing category-level gates (Restraints permission + ToS acknowledgement) and by treating it as a documented, deliberate design choice in the proposal rather than a silent capability expansion - not a new mitigation, but an explicit one.
- **Full `Item` sheet is large (tens of thousands of rows)** → filter by slot first (cuts it to the few hundred valid for that slot) and apply the existing text-filter-as-you-type pattern from `AnimationPickerWindow`, same as how that picker already handles a large mod/animation catalog.
- **Some `Item` rows are non-equippable junk (event-only, deprecated, zero-name)** → filter out rows with an empty `Name` after `ExtractText()`, matching the minimum-viable filter already implied by requiring a name for display; no further filtering (e.g. by rarity or level) is attempted, since the user asked for "any item," not a curated subset.
