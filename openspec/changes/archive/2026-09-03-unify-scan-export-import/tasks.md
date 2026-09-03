## 1. Restraints catalog/quick-command parity

- [x] 1.1 Add `OwnerQuickCommands.Restraints` (`List<QuickCommand>`) to `PluginConfig`, and verify a saved/reloaded config round-trips it the same as `Outfits`/`Moodles`.
- [x] 1.2 Add a plain-name catalog export method to `RestraintCommand` (tagged device names, deduplicated), matching `OutfitCommand`'s/`MoodlesCommand`'s existing name-export shape, and verify it returns each tagged device's name exactly once. (Added matching `ExportNames()` to `OutfitCommand`/`MoodlesCommand` too, since neither had one yet - the "Copy names" logic previously lived inline in `SettingsWindow`, not on the command classes `CatalogSyncService` needs to call.)
- [x] 1.3 Add `DrawRestraintQuickSection` to `CollarWindow` (send/remove list + a fixed "Restraint unlock"-style row if applicable, following `DrawMoodlesQuickSection`'s shape) and wire it into `DrawOwnerModule` via `DrawOwnerSection`, and verify a manually-added restraint quick command sends the expected `restraint lock <name>` text. (No per-category import button on this new section, since the unified import is this change's whole point - built directly in its final shape rather than adding a button in task 1 only to delete it in task 5.)

## 2. `CatalogSyncService`

- [x] 2.1 Create `CatalogSyncService` with `BuildExport()`: assembles the `## WARDROBE` / `## GESTURE` / `## MOODLES` / `## RESTRAINTS` sectioned text from each category's existing export output (`OutfitCommand`/`GestureCommand.ExportCatalog()`/`MoodlesCommand`/`RestraintCommand`'s name exports), emitting every section header even when its body is empty, and verify the output round-trips through `ParseImport` with matching per-category entries. (No test project exists in this repo - verified by build success plus a manual trace of `BuildExport`/`ParseImport` against each other, same as the prior change's `RestrictionRuleManager` verification gap.)
- [x] 2.2 Implement `ParseImport(text)`: splits on `## ` headers and, per section, builds the same `QuickCommand` entries `ImportQuickCommands`/`ImportGestureCommands` build today (moved into this service), skipping entries already present in the target list (by command text) and returning a per-category added-count summary, and verify re-importing an already-imported file adds zero new entries to every category.
- [x] 2.3 Verify a section header present with zero body lines leaves that category's existing quick-command list unchanged (the "empty but explicit" case), and a malformed/unrecognized line within a section is skipped rather than aborting the whole import. (Deviates slightly from the old `ImportGestureCommands`, which aborted the whole import on the first unparseable line - the unified importer skips just that line, matching this task's explicit "skipped rather than aborting" wording, which supersedes the old per-category behavior it replaces.)

## 3. File I/O plumbing

- [x] 3.1 Add a `FileDialogManager` instance to `Plugin`, drawn from the existing `PluginInterface.UiBuilder.Draw` hook alongside `WindowSystem.Draw`, disposed/unsubscribed in `Plugin.Dispose` the same way `WindowSystem` is torn down. (`FileDialogManager` has no `Dispose` - confirmed its actual public API by reflecting the referenced `Dalamud.dll` directly via `MetadataLoadContext` before writing this, since design.md flagged the signature as unconfirmed. Calls `Reset()` on plugin unload instead, to close any in-flight dialog.)
- [x] 3.2 Wire an "Export" action (Settings' new unified scan section) that opens `SaveFileDialog`, and on a chosen path writes `CatalogSyncService.BuildExport()`'s text via `System.IO.File.WriteAllText`, and verify the written file's content matches `BuildExport()`'s return value exactly.
- [x] 3.3 Wire the "Import commands" action (Owner tab) that opens `OpenFileDialog` filtered to `.txt`, reads the chosen file via `System.IO.File.ReadAllText`, and feeds it to `CatalogSyncService.ParseImport`, and verify a read/parse failure surfaces an error next to the button instead of throwing or silently doing nothing.

## 4. Unified Settings scan section

- [x] 4.1 Replace `DrawWardrobeScanCard`/`DrawGestureScanCard`/`DrawMoodlesScanCard` with one "Scan & Export" section in `SettingsWindow.cs` that keeps each category's existing scope controls (Wardrobe folder allowlist, Gesture mod picker/filter) and per-category feedback exactly as they render today, just grouped under one heading.
- [x] 4.2 Add one "Scan all" button that calls `OutfitCommand.Rescan()`, `GestureCommand.Rescan()`, and `MoodlesCommand.Rescan()` in sequence, and verify triggering it produces the same per-category scan results (`LastScanTotalDesigns`/`LastScanTotalMods`/`LastScanTotalPresets`) as triggering each category's rescan individually with the same scope configured. (Individual per-category "Rescan ___" buttons were kept alongside "Scan all," not removed - useful when only one category's scope changed; nothing in the proposal/design called for removing them.)
- [x] 4.3 Add the "Export" button (wired in 3.2) to this section, positioned after the scan results, and verify it's disabled or shows a clear hint when nothing has been scanned yet this session.

## 5. Owner tab: unified import, removed per-category buttons

- [x] 5.1 Add a centered "Import commands" button (wired in 3.3) at the top of `DrawOwnerModule`, above the existing per-category `DrawOwnerSection` list, showing the per-category added-count summary or error beneath it.
- [x] 5.2 Remove the "Add from clipboard" button and its import call from `DrawOutfitQuickSection`, `DrawGestureQuickSection`, and `DrawMoodlesQuickSection` (keep each section's list/send/remove/"Clear all" controls exactly as they are), and remove the now-unused `ImportQuickCommands`, `ImportGestureCommands`, `outfitImportError`, `gestureImportError`, `moodlesImportError` from `CollarWindow`.
- [x] 5.3 Verify each of Outfit/Gesture/Moodles/Restraints' quick-command list still displays, sends, and removes entries correctly with only the shared unified import button as the way to populate them from a file. (Verified by code review/build - no test harness exists to exercise ImGui interactively from here; matches the same verification-method note recorded in the prior change.)

## 6. Documentation

- [x] 6.1 Update the README's setup-flow description (wherever it currently walks through scanning/exporting per category) to describe the unified scan/export/import flow, and verify it no longer references the removed per-category "Add from clipboard" buttons. (Also fixed a pre-existing gap from the prior `add-restraints-devices` change while touching this section: the "two ways to command" paragraph's reserved-word list and Quick Commands paragraph had never been updated to mention `restraint`/Restraints at all - included here since it's directly adjacent to what this task already touches.)

## 7. Post-completion refactor: independent Restraints scan scope

Requested directly after the above was implemented (not a separately proposed change): Restraints was
scoped from `collar/outfit`'s Wardrobe scan/allowlist, but bondage/restriction-themed designs and everyday
outfits live in different Glamourer folders in practice and need different filters.

- [x] 7.1 Add `PluginConfig.RestraintFolderAllowlist` and `RestraintMapping.ScannedDesigns`, independent of `WardrobeFolderAllowlist`/`WardrobeMapping.LocalDesigns`.
- [x] 7.2 Add `RestraintCommand.Rescan()`/`LastScanTotalDesigns` (same "empty allowlist = all designs" semantics as `OutfitCommand.Rescan`), and repoint `RestraintCommand.ScannedDesigns()` at the new independent catalog instead of `WardrobeMapping`.
- [x] 7.3 Add a "Restraints design allowlist & scan" section (own allowlist editor, own "Rescan restraints" button, own feedback) to Settings' unified Scan & Export card, and include `RestraintCommand.Rescan()` in "Scan all".
- [x] 7.4 Update `collar/restraints` and `collar/catalog-sync`'s spec/design text (in the still-unarchived `add-restraints-devices` and `unify-scan-export-import` changes) to describe the independent scan instead of "no scan step of its own".
