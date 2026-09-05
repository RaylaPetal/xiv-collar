## 1. Nav restructure scaffolding

- [x] 1.1 Update `CollarWindow.NavItems` to the new 10-entry set (Title, Outfit, Animation, Moodles, Restraints, Custom Triggers, Collar, Follow / Leash, Permissions, Sync), with tab ids and icons decided per design.md; remove the old `"owner"` entry from the array. Verify by building and confirming the nav bar renders 10 tabs with no duplicate ids.
- [x] 1.2 For each shared category (Title, Outfit, Animation, Moodles, Restraints, Custom Triggers), replace the tab's `Draw()` switch case with a role-aware dispatch: Sub-role calls the existing `Draw*Module` method, Owner-role calls the existing `Draw*QuickSection`/accordion-body method (minus its own collapsible-header wrapper, now redundant with the tab itself), each keeping a small role-mismatch preview banner matching `DrawOwnerModule`'s existing pattern. Verify manually that every existing add/import/compose/copy/send/scan/favorite operation in each category still works after the move, under both Roles.
- [x] 1.3 Remove `DrawOwnerModule` and its now-unused accordion-only scaffolding (`DrawOwnerSection`, if not reused elsewhere) once every category and the Sync tab (task 3) have their content relocated. Verify by building with no unused-method warnings introduced.

## 2. Collar / Follow split

- [x] 2.1 Split `DrawCollarModule`'s existing body into two methods: collar-item/Moodle config (Collar tab) and leash trigger-word config (Follow / Leash tab), with no change to either section's own controls. Wire both into the Sub-role dispatch from task 1.2.
- [x] 2.2 Wire the existing Owner-side `DrawCollarQuickSection` and `DrawFollowQuickSection` into the Owner-role dispatch for the Collar and Follow / Leash tabs respectively. Verify manually that Sub-side collar config, Sub-side leash config, Owner-side collar quick actions, and Owner-side leash quick commands each still work exactly as before, now from two separate tabs.

## 3. Sync tab

- [x] 3.1 Add the Sync tab's Owner-role view by calling the existing `DrawCatalogRelaySection`/`DrawImportCommandsButton` (currently at the top of `DrawOwnerModule`) from the new tab instead. Verify manually that catalog relay sync, import, and reset all still work identically.
- [x] 3.2 Add the Sync tab's Sub-role view: a short relay-sync explanation, the per-category scanning controls and "Scan all" button, and the offline/manual export action (all moved from Settings' former "Scanning" tab - see task 9.1) per the "Sync tab holds catalog relay sync and import/reset" requirement's Sub scenario. Verify manually that a Sub-configured client sees the explanation, can rescan each category, and can export a file from this tab.

## 4. Animation rename (display-only)

- [x] 4.1 Rename every user-facing string that names the Gesture category to "Animation": the nav tab tooltip/label, `DrawGestureModule`'s title, the Owner-side section header, and the Permissions checkbox label (`ImGuiCheckbox("Gesture", ...)` -> `"Animation"`). Leave `GestureCatalogEntry`, `GestureCommand`, `DrawGestureModule`/`DrawGestureQuickSection` method names, `QuickCommands.Gestures`, `permissions.Gesture`, and `ChatCommandListener.ReservedCategoryWords`'s `"gesture"` entry unchanged. Verify by grepping the UI files for remaining user-visible "Gesture" strings and confirming only internal identifiers remain.
- [x] 4.2 Verify manually that the Owner's direct-override `gesture <name>` tell grammar still works unchanged (task 4.1 must not have touched `ReservedCategoryWords` or any parsing of the `gesture` keyword).

## 5. Outfit rename (display-only)

- [x] 5.1 Rename the Sub-side nav tab label and `DrawWardrobeModule`'s on-tab title from "Wardrobe" to "Outfit", matching the Owner-side view's existing "Outfit" label. Leave Settings' "Wardrobe design allowlist & scan" section title and all `DrawWardrobeScanBody`/allowlist-related identifiers unchanged. Verify by confirming Settings' scan section still reads "Wardrobe design allowlist & scan" while the main window's tab reads "Outfit".

## 6. Smaller fixes

- [x] 6.1 Fix `DrawMoodlesQuickSection`'s icon from `FontAwesomeIcon.TheaterMasks` to `FontAwesomeIcon.Smile`, matching the Moodles nav tab's own icon. Verify visually that the Moodles tab's icon is consistent between its Sub-role and Owner-role views.

## 7. Favorites menu cleanup

- [x] 7.1 Remove the quick-access favorites menu's "Open Owner commands" entry and its associated menu-building code. Verify manually that the menu still shows "Open main window" and "Open settings" under both Roles, and the favorites list/second-level select under Owner, with no "Open Owner commands" entry remaining.
- [x] 7.2 Remove `CollarWindow.OpenOwnerTab()` now that nothing calls it. Verify by building and confirming no remaining references.

## 8. Full-flow verification

- [x] 8.1 `dotnet build Oathbound.slnx` succeeds with no warnings introduced by this change.
- [x] 8.2 Manually walk every one of the 10 tabs under both Role=Sub and Role=Owner (switching Role in Settings between passes), confirming: each shared category's existing operations still work, Collar/Follow behave independently, Sync behaves correctly for both roles, Animation and Outfit read their new labels everywhere expected, and Settings' allowlist wording is untouched.
- [x] 8.3 Manually verify the favorites quick-access menu (both Roles) and confirm opening the main window as Owner lands on Owner-role views without a dedicated "Owner tab" to jump to.

## 9. Post-verification fixes (found while checking task 8.2)

- [x] 9.1 Move scanning and export entirely out of Settings and into the Sync tab's Sub-role view: `DrawScanAndExportCard` ("Scan all"), `DrawWardrobeScanBody`/`Feedback`, `DrawGestureScanBody`/`Feedback`, `DrawRestraintScanBody`, `DrawMoodlesScanBody`/`Feedback`, `DrawPenumbraFolderPicker`, `ParentFolders`, `DrawAllowlistBody`, and their three backing fields (`gestureModSearch`, `penumbraFolderSearch`, `newWardrobeAllowlistFolder`) all moved from `SettingsWindow` to `CollarWindow`; the export button (moved earlier this task group) now follows them in `DrawSubExportSection`. Removed Settings' "Scanning" tab item and its now-unused `scanAndExportResult` field entirely - `DrawTestButton` stayed in `SettingsWindow` since "Test an Owner command" still uses it. Verify manually that Settings shows only Identity & Pairing and ToS tabs, and that every scan control and Export produce identical results from the Sync tab.
- [x] 9.2 Fix `CatalogSyncService.ParseImport` missing a `stagedRestraintCatalog.Clear()` before re-populating it on a refreshed Restraints section (present for `stagedGestureCatalog` but absent for restraints) - the Owner-role Restraints tab's mod browser was showing the same mod multiple times after repeated imports. Verify manually that importing the same catalog file twice in a row no longer duplicates any mod in the Restraints browser.
- [x] 9.3 Replace the stale "use \"Import commands\" above" message in the Owner-role Outfit/Animation/Moodles quick-command sections (accurate only when Import commands lived in the same accordion) with a `DrawGoToSyncTabPrompt` helper that switches `activeModule` to `"sync"` directly. Verify manually that clicking it from each of the three tabs lands on the Sync tab.
- [x] 9.4 Fix "Reset imports" only clearing the imported quick-command lists, never `GestureMapping.ImportedPeerCatalog`/`RestraintMapping.ImportedPeerCatalog` - the dictionaries the Owner's mod-browser dropdowns read from - so a stale entry from before the 9.2 fix (or any future one) had no way to be cleared short of a fresh import. Added `.Clear()` on both dictionaries to the Reset imports handler. Verify manually that clicking "Reset imports" removes any currently-duplicated or stale mod from the Animation/Restraints browsers immediately, with no new import needed.
- [x] 9.5 Grow the Owner-role Moodles and Outfit quick-command list boxes from a fixed 120px to fill the rest of their tab (`Math.Max(120, ImGui.GetContentRegionAvail().Y)`) - a leftover from when both shared one scrolling accordion with several other sections; now that each has its own tab, the fixed height left most of the tab empty. Verify visually that both lists now use the available vertical space instead of a small fixed box.

Note: there is no automated test project for the plugin (see the pairing-invitation-reliability change's removal of `Oathbound.Plugin.Tests`) - every verification step above is a build check or a manual in-game/in-UI check, not an automated test.
