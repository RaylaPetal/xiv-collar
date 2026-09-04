Note: this repo has no unit/integration test project. Every task below marked done was verified via `dotnet build` plus manual code review, not an automated test, per user direction during apply.

## 1. Data model: import provenance and category routing

- [x] 1.1 Add an `ImportSource` (Manual/Imported) marker to `QuickCommand` in `PluginConfig.cs`, defaulting existing saved entries to a non-`Imported` value on config load, and verify existing configs load without incorrectly flagging pre-existing entries as import-sourced
- [x] 1.2 Add a config-load migration path so any entry currently in `QuickCommands.Aliases` that is unambiguously a single-action alias/trigger is left in place (no forced move), per design.md's Migration Plan, and verify a pre-change config still opens and renders correctly

## 2. Export/import routing (`collar/catalog-sync`)

- [x] 2.1 Update `CatalogSyncService.BuildExport`/`ExportAliasEntries` to route each single-category alias definition and each single-action Custom Trigger into its own category's export section (adding a Title export section, since none exists today), and verify via a unit/integration test that a single-action Title/Outfit/Gesture/Restraint/Moodle alias appears in its own category section, not a generic aliases section
- [x] 2.2 Update export so Custom Triggers bundling two or more actions populate a distinct Custom Trigger Bundle section with a full action-summary per entry, and verify a multi-action bundle appears only there
- [x] 2.3 Update `CatalogSyncService.ParseImport`/`ImportAliasLines` and add category-specific import helpers so each category's section populates that category's `QuickCommand` list (marked `Imported`), with per-category dedup by command, and verify import tests cover Title/Outfit/Gesture/Restraint/Moodle single-action entries landing in the correct list
- [x] 2.4 Ensure a multi-action bundle from the import file only ever populates the Custom Trigger Bundle list, never a single category list, and verify with an import test
- [x] 2.5 Add backward-compatible parsing for old-format exported files (flat `## ALIASES` section) so previously exported files still import without error, routing their contents into the Custom Trigger Bundle list as before, and verify by importing a fixture captured in the old format
- [x] 2.6 Update reset-imports to remove only `Imported`-flagged entries from Title/Outfit/Gesture/Moodles/Restraints, and to keep clearing the whole Custom Trigger Bundle list, and verify with a test that scanned/manual entries in a mixed category survive reset while imported ones are removed

## 3. Owner UI: category sections and renamed bundle section (`collar/ui-organization`)

- [x] 3.1 Rename the "Alias / one-off" section to reflect its narrower Custom Trigger Bundle/freeform purpose in `CollarWindow.DrawOwnerModule`/`DrawFreeformComposer`, and verify by opening the Owner tab and confirming the new label and that it now only shows bundle/freeform entries
- [x] 3.2 Ensure the Title section (and existing Outfit/Gesture/Restraint/Moodle sections) render newly-imported single-action entries with their Send/Copy/Remove/Favorite controls exactly like any other saved entry in that category, and verify by importing a fixture file and checking each entry appears in its expected section
- [x] 3.3 Update `DrawImportCommandsButton`'s result summary to report per-category counts for the newly-routed categories (including Title) instead of lumping them under "Aliases", and verify the post-import summary text matches actual per-category adds

## 4. Quick-access popup menu (`collar/ui-organization`)

- [x] 4.1 Build the two-level ImGui popup menu (top level: categories with ≥1 favorite plus "Open Owner commands" and "Open main window"; second level: that category's favorited entries with Send), replacing `FavoritesWindow`'s content, and verify by favoriting entries across two categories and opening the menu
- [x] 4.2 ~~Wire the DTR entry's click handler to open this popup instead of toggling `FavoritesWindow`~~ - superseded during in-game testing: the DTR entry crashed the game on click (Dalamud invokes its click handler outside any valid ImGui frame; calling ImGui popup APIs from there is unsafe) and was removed outright rather than wired up, per explicit user direction and the `collar/ui-organization` delta's REMOVED requirement. The on-screen button (4.3) is now the sole way to open the popup. Verified: `IDtrBar`/`dtrEntry` no longer referenced anywhere in `Plugin.cs`, and `dotnet build` succeeds
- [x] 4.3 Implement the movable on-screen button (borderless overlay, default bottom-right) that opens the same popup, and verify it renders and opens the popup on click
- [x] 4.4 Add a Settings control for the button's position (corner presets + offset) that persists across sessions, and verify changing it moves the button and survives a plugin reload
- [x] 4.5 Implement the "no favorites yet" empty-state message in the popup, and verify it shows when no `QuickCommand` across any category has `IsFavorite` set
- [x] 4.6 Remove `FavoritesWindow.cs` once the popup covers all its scenarios, and verify no remaining references to it in `CollarWindow.cs` or plugin bootstrap/window-system registration

## 5. Verification

- [x] 5.1 Run the full test suite and verify it passes with no regressions in existing catalog-sync/ui-organization coverage - no test suite exists in this repo; verified via `dotnet build` (Debug/Release) instead, per user direction
- [ ] 5.2 Manually exercise the golden path in-game or via the dev harness: Sub creates a single-action Title alias, a single-action Restraint alias, and a two-action Custom Trigger bundle; Sub exports; Owner imports; verify the Title/Restraint entries land in their own sections and the bundle lands in the Custom Trigger Bundle section; verify reset-imports removes only the imported entries - NOT verified as the literal scenario described. Live in-game testing during this change did verify the related outfit-apply/slot-lock path extensively (including two real bugs found and fixed: the vague slot-conflict error, and the all-or-nothing refusal now skip-and-restore behavior), but the specific export/import golden path (Title + Restraint aliases + a 2-action bundle, round-tripped through export/import) was never run end-to-end
- [x] 5.3 Manually verify the quick-access popup from both the DTR entry and the on-screen button, including the empty-favorites state, the Owner-tab shortcut, and the main-window shortcut - the DTR entry itself was removed during this change (see the ui-organization delta's REMOVED requirement), so only the on-screen button applies now; that path was extensively live-tested and fixed (open/close flicker, game-crash-on-click, wrong popup position, empty-favorites text, Sub-role gating) through direct back-and-forth with the user in-game
