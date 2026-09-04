## Why

A restraint device can currently only be captured by physically equipping the desired gear piece first, then reading it back from Glamourer's live state - this requires the Sub to own and wear the exact piece before it can become a device at all, and the Owner has no way to define a device except by referencing a name the Sub already captured and told them out of band. Glamourer's own `SetItem` IPC already applies any valid item id to a slot unconditionally - it never requires the item to be currently equipped (confirmed against Glamourer.Api's own documented contract) - so a proper gear picker (choose a slot, then browse/search every game item valid for that slot) is both feasible and a meaningfully better experience, matching how the comparable plugin GagSpeak already lets an Owner pick gear directly.

## What Changes

- A new searchable item picker (modeled on the existing Gesture animation picker) lets the Sub or Owner choose any equippable game item for a chosen slot, backed by the game's own item data - no mod, no prior scan, and no need for the item to be currently equipped.
- The Sub's own device-capture flow SHALL replace "equip the piece, then capture what's equipped" with "pick a slot, then pick an item from the picker." **BREAKING**: the equip-first capture flow is removed, not kept as an alternative - a Sub who already captured devices under the old flow keeps them (their stored item id/stain is unaffected), but capturing a *new* device always goes through the picker now.
- The Owner SHALL be able to define a restraint device's slot and item directly, via the same picker, entirely independently of the Sub - no Sub-side capture, no name the Sub has to share out of band. The Owner assigns rules to it exactly as they already do for named devices. This travels to the Sub as a new command carrying the full slot/item/rule definition, since there is no Sub-side name to look up.
- The existing name-based flow (Sub captures and names a device locally; Owner references it by that name, via import or manual entry) remains available unchanged, for a Sub who wants to curate a specific, named, pre-approved list rather than relying entirely on the Owner's own picks.
- Stain/dye selection is out of scope for this change - a picker-captured or Owner-authored device is undyed (stain 0/0) for now; the underlying data already has stain fields, so adding a dye picker later is a pure UI addition, not a data-model change.

**Consent note (deliberate, not an oversight)**: an Owner-authored ad-hoc device lets the Owner force *any* equippable game item onto *any* of the Sub's lockable slots, without the Sub ever having reviewed that specific item in advance - a broader grant than every other Owner-forced action in this plugin, which is always scoped to something the Sub themselves captured, scanned, or explicitly tagged. This is intentional, matches the explicitly-referenced precedent (GagSpeak), and remains behind the existing category-level consent gates: the Sub must still have enabled the "Restraints" permission and completed the automation-risk ToS acknowledgement before *any* restraint device - named or ad-hoc - can be force-applied at all.

## Capabilities

### New Capabilities
(none — extends the existing `collar/restraints` capability)

### Modified Capabilities
- `collar/restraints`: device capture moves from "equip then read live state" to "pick a slot and an item from a searchable picker, no live-equip step needed"; adds an Owner-authored ad-hoc device path (slot + item + rules, no Sub-side name or pre-capture required) alongside the existing named-device path.

## Impact

- `CollarSystem.Plugin/UI/ItemPickerWindow.cs` (new) — searchable item-by-slot picker, modeled on `AnimationPickerWindow.cs`, backed by `Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>()` filtered by `EquipSlotCategory`.
- `CollarSystem.Plugin/UI/CollarWindow.cs` — `DrawRestraintsModule`'s capture section replaces the "capture current item" button with a "Choose item..." picker button; Owner's Restraints quick-command section gains a new "Define device (pick gear)" flow alongside the existing name-based "Add Command."
- `CollarSystem.Plugin/Commands/RestraintCommand.cs` — new `CaptureDeviceFromItem(ApiEquipSlot slot, ulong itemId, string name, List<RestraintRuleAssignment> rules)` (Sub-side, no Glamourer read); new `ForceApplyAdHoc(ApiEquipSlot slot, ulong itemId, string label, List<RestraintRuleAssignment> rules)` (Owner-authored, no name lookup); `CaptureCurrentAsDevice` (equip-first) is removed; wire format gains a new grammar for ad-hoc devices.
- `CollarSystem.Plugin/Commands/ChatCommandListener.cs` — `HandleForceRestraint` recognizes the new ad-hoc grammar alongside the existing `lock "<name>"` grammar.
- `CollarSystem.Plugin/Plugin.cs` — wires the new `ItemPickerWindow` into the `WindowSystem`, same as `AnimationPickerWindow`.
- **BREAKING**: the Sub's "capture what's currently equipped" flow is removed; capturing a new device always uses the item picker going forward. Already-captured devices are unaffected.
