## Why

The Owner's import/command surface has grown past what its original UI patterns support: restraints import silently produces zero usable commands because export only carries Sub-tagged devices (not raw scanned designs), the gesture quick-command list is an unmanageable flat scroll for a Sub with 1000+ gestures, "Clear all" controls are inconsistently placed, there is no way to wipe a bad import without clearing categories one at a time, and the Moodles scan surfaces bundled presets when the Owner actually needs to command individual buffs/debuffs.

## What Changes

- Restraints export/import SHALL carry every scanned design, not only designs the Sub has pre-tagged as a device, so a scan showing "5 available" can actually produce imported entries.
- The Owner SHALL assign restriction rules (forced pose, walk-only, action block, gag) to each imported restraint entry directly in the Owner import flow, using the same rule-picker pattern the Sub's own restraint-tagging UI uses (`DrawRestraintsModule`) — this replaces the current requirement that the Sub pre-tag every device before an Owner can import it.
- The Owner's Gesture quick-command list SHALL be reworked to use the same grouped, searchable, collapsible presentation as the Sub's animation picker window (`AnimationPickerWindow`) instead of one flat scrolling dropdown.
- "Clear all" controls (Outfit, Gesture, Moodles, Restraints quick-command sections) SHALL move to the far right of each section's title row, matching the placement pattern already used for Outfit's title row.
- A new "Reset imports" control SHALL be added next to the existing "Import commands" button, clearing every import-populated quick-command list (Outfit, Gesture, Moodles, Restraints) back to empty in one action.
- Moodles scanning SHALL read the Sub's raw Moodles statuses (individual buffs/debuffs) instead of only bundled presets, and the Owner SHALL apply/clear by individual status rather than only by preset. **BREAKING**: existing Owner Moodles quick commands built from preset names will no longer resolve once the catalog switches to raw statuses, and must be re-imported.

## Capabilities

### New Capabilities
(none — all changes extend existing capabilities)

### Modified Capabilities
- `collar/restraints`: scan/export scope widens from "tagged devices only" to every scanned design; rule assignment (forced pose/walk-only/action block/gag) moves into the Owner's import flow instead of being a Sub-only pre-tagging step.
- `collar/catalog-sync`: restraints import changes from filling a ready-made quick-command list to importing raw scanned designs that still need per-item rule assignment before they're usable as quick commands.
- `collar/moodles`: local catalog scan changes from Moodles presets to raw Moodles statuses (individual buffs/debuffs); apply/clear commands operate on a status rather than a preset.
- `collar/ui-organization`: adds requirements for the Owner Gesture quick-command list's grouped/searchable presentation, the far-right "Clear all" placement on quick-command section title rows, and the "Reset imports" control next to "Import commands".

## Impact

- `CollarSystem.Plugin/Commands/RestraintCommand.cs` — scan/export/tag logic (`Rescan`, `ExportNames`, `TagDevice`).
- `CollarSystem.Plugin/Commands/CatalogSyncService.cs` — `ParseImport`/`BuildExport` restraints section format.
- `CollarSystem.Plugin/UI/CollarWindow.cs` — `DrawRestraintsModule`, `DrawGestureQuickSection`, `DrawImportCommandsButton`, `DrawOutfitQuickSection`, `DrawMoodlesQuickSection`, `DrawRestraintQuickSection`, `DrawOwnerModule`.
- `CollarSystem.Plugin/UI/AnimationPickerWindow.cs` — reused as the pattern source for the new gesture picker.
- `CollarSystem.Plugin/Ipc/MoodlesIpc.cs` — new raw-status IPC subscriber(s)/apply call alongside the existing preset calls.
- `CollarSystem.Plugin/Commands/MoodlesCommand.cs` — `Rescan`/`ForceApply`/`ExportNames` switch from presets to raw statuses.
- `CollarSystem.Plugin/Config/` — `RestraintMapping`/`MoodlesMapping`/`QuickCommands` shapes affected by the above.
- **BREAKING**: Owner Moodles quick commands referencing preset names stop resolving after the Moodles catalog switches to raw statuses.
