## Why

Restraint devices are currently defined as a whole Glamourer design, applied via a full-design apply that can touch several equipment slots at once. In practice a restraint is a single specific gear piece (a bracelet, a chest harness, a specific pair of cuffs) the Sub already owns as an equipped item, not a saved multi-slot outfit design - and the existing scan-a-design-library flow forces the Sub to maintain designs in Glamourer purely to tag them as devices. Separately, the restriction-rule set (forced pose, walk-only, action block, gag) has no way to visually lock a Sub into a *specific* bound-looking animation from their own installed mods, and the "gag" rule's display name reads oddly next to the others.

## What Changes

- A restraint device SHALL capture a single equipped gear piece (one equipment slot's item, stain, and stain2) directly from what the Sub currently has equipped, the same capture mechanism `collar/collaring`'s Collar item already uses - not a reference to a whole Glamourer design. **BREAKING**: existing design-based restraint devices are dropped; the Sub re-captures each device from the actual piece now equipped in its slot.
- Restraints SHALL drop out of the unified "Scan & Export"/"Scan all" action entirely (there is no design library left to batch-scan) - a device is captured one at a time, on demand, mirroring how the Collar item is captured. Restraints keeps participating in the unified Export/Import file, unchanged.
- The Owner SHALL be able to add a restraint quick command themselves by typing a device name the Sub told them, the same freeform "Add Command" pattern Title already provides, instead of only being able to populate the list via importing a Sub-exported file.
- Three new restriction rule kinds SHALL be added: **Arms Cuffed**, **Legs Cuffed**, and **Full Body Cuffed**. Each carries its own chosen animation, picked from the Sub's own installed-mod animation catalog using the same searchable picker `collar/gesture`'s animation picker already provides. Applying the rule SHALL temporarily activate that mod/animation and hold the Sub in it for as long as the rule stays active (no idle timeout, unlike Gesture's own temporary-activation use), reverting the temporary activation when the rule is released. Full Body Cuffed additionally suppresses all movement input while active, the same way the existing forced-pose rule does, making it a fully custom-animation counterpart to forced pose.
- The Gag rule SHALL be relabeled **Gagged** everywhere it is shown to the Sub or Owner (its underlying behavior is unchanged).

## Capabilities

### New Capabilities
(none — all changes extend existing capabilities)

### Modified Capabilities
- `collar/restraints`: device identity changes from a Glamourer design reference to a single captured slot+item; adds the Arms Cuffed / Legs Cuffed / Full Body Cuffed rule kinds with an animation selection (Full Body Cuffed also suppresses movement like forced pose); renames the Gag rule's display label to Gagged; adds an Owner-side manual "Add Command" entry point for restraint quick commands.
- `collar/catalog-sync`: Restraints is removed from the unified "Scanning every catalog together" requirement (no scan step remains for it); Export/Import requirements are otherwise unaffected since they already operate on named devices.

## Impact

- `CollarSystem.Plugin/Config/PluginConfig.cs` — `RestraintDeviceDefinition` (swap `DesignId` for a captured slot+item), `RestraintMapping` (drop `ScannedDesigns`), `RestraintRuleKind` (add `ArmsCuffed`/`LegsCuffed`), `RestraintRuleAssignment` (carry a chosen animation reference for the two new kinds).
- `CollarSystem.Plugin/Commands/RestraintCommand.cs` — replace `Rescan`/`TagDevice`-from-scan/`ApplyDevice`'s design-apply path with a `CaptureCurrentAsDevice(slot)` flow and direct `SlotLockManager.TryLock` application, mirroring `CollarCommand`.
- `CollarSystem.Plugin/Safety/RestrictionRuleManager.cs` and a new enforcer for the bound-animation rule kinds (temporary Penumbra activation held for the rule's duration, reverted on release) - reusing `PenumbraIpc.TrySetTemporarySettings`/`TryRemoveTemporarySettings` the way `GestureCommand.Execute` does, without Gesture's own idle-timeout revert.
- `CollarSystem.Plugin/UI/CollarWindow.cs` — `DrawRestraintsModule` (slot picker + capture button instead of scanned-design combo; animation picker for the two new rule checkboxes), `DrawRestraintQuickRow`'s rule editor (same two new checkboxes + animation picker), a new manual "Add Command" control in the Owner's Restraints quick-command section.
- `CollarSystem.Plugin/UI/SettingsWindow.cs` — remove Restraints from the "Scan & Export"/"Scan all" card; drop the `RestraintFolderAllowlist` setting.
- `CollarSystem.Plugin/Commands/CatalogSyncService.cs` — no scan-step change needed (it never scanned Restraints itself), but its export source simplifies now that every device is always named at capture time.
- **BREAKING**: existing Glamourer-design-based restraint devices stop working; the Sub must re-capture each one as a single gear piece.
