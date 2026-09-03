## Why

Wardrobe, Gesture, and Moodles each have their own scan card in Settings and their own "Add from clipboard" import button in the Owner tab, and Restraints (just added) has neither - so setting up a fresh pairing means running three separate scans, three separate copies, and will soon mean four separate Owner-side pastes, each in a slightly different format. That's needless per-category ceremony for something that's really one task: "here's everything my Owner can command me with." A single scan-everything section that exports one file, and a single Owner-side import that fills every category at once, turns setup into one action per side instead of four.

## What Changes

- New **"Scan & Export"** section in Settings, replacing the Wardrobe/Gesture/Moodles scan cards: one "Scan all" button runs the Wardrobe, Gesture, Moodles, and Restraints scans together, each with its own independent scope controls (Wardrobe and Restraints each have their own folder allowlist - bondage-themed designs and everyday outfits live in different Glamourer folders in practice, so they need different filters; Gesture keeps its mod picker) kept exactly as configurable as they are today, just grouped under one section.
- One **"Export"** action writes a single `.txt` file to disk (via a native save dialog) containing every category's catalog - Wardrobe, Gesture, Moodles, and Restraints - so the Sub can hand the Sub off to their Owner however they like (Discord, file share, etc.), not only same-screen copy/paste.
- New centered **"Import commands"** button at the top of the Owner tab opens a native file-open dialog, reads the exported file, and populates every category's quick-command list (Title is unaffected - it has no scannable catalog) in one action.
- **BREAKING**: removes the per-category "Add from clipboard" import buttons (Outfit, Gesture, Moodles) from the Owner tab. Each category's quick-command list (browse/send/remove) stays exactly where it is; only its own separate import button goes away.
- Restraints gains an Owner-tab quick-command section (browse/send/remove tagged devices) for the first time, so it has something for the unified import to fill - it didn't have Owner-side quick commands before this change.

## Capabilities

### New Capabilities
- `collar/catalog-sync`: the unified scan-all / export-to-file / import-from-file flow spanning Wardrobe, Gesture, Moodles, and Restraints catalogs.

### Modified Capabilities
- (none - each category's own catalog-identity and export-content guarantees are unchanged; only where scanning/export/import happens in the UI changes, which `collar/catalog-sync` owns)

### New requirement inside an existing capability
- `collar/restraints`: adds Owner-tab quick-command apply/release entries and a name-based catalog export for restraint devices, matching the parity Outfit/Moodles/Gesture already have (needed for `collar/catalog-sync` to have something to import into for this category).

## Impact

- New `CollarSystem.Plugin/Commands/CatalogSyncService.cs`: builds the combined export text (delegating to each category's existing per-category export logic) and parses an imported file back into per-category entries.
- `CollarSystem.Plugin/UI/SettingsWindow.cs`: replace `DrawWardrobeScanCard`/`DrawGestureScanCard`/`DrawMoodlesScanCard` with one unified scan section; per-category scope controls (folder allowlist, mod picker, feedback) are preserved, just regrouped.
- `CollarSystem.Plugin/UI/CollarWindow.cs`: remove the three per-category "Add from clipboard" buttons and their `ImportQuickCommands`/`ImportGestureCommands` call sites; add one centered "Import commands" button at the top of the Owner tab; add a Restraints quick-command section.
- `CollarSystem.Plugin/Config/PluginConfig.cs`: `OwnerQuickCommands` gains a `Restraints` list.
- `CollarSystem.Plugin/Commands/RestraintCommand.cs` (or a small extension): a name-based `ForceApply`/`ForceUnlock` quick-command path already exists via `ChatCommandListener`'s `restraint lock/unlock` grammar - this change only adds the catalog-export/quick-command UI plumbing around it, not new apply logic.
- New file-dialog dependency: `Dalamud.Interface.ImGuiFileDialog.FileDialogManager`, bundled with the Dalamud SDK already referenced (`Dalamud.dll`) - no new package reference needed.
