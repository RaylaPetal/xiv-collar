## 1. Data model

- [x] 1.1 Add a `Target` field (string) to `AliasExportEntry` in `CatalogSyncService.cs`, and verify the encode/decode round-trip (`EncodeAliasEntry`/`TryParseAliasEntry`) preserves it, including for an older entry with no `Target` present - `Target` is a plain nullable auto-property, so System.Text.Json round-trips it (and defaults missing/older entries to null) with no custom (de)serialization code; verified via `dotnet build` and code review, no test suite exists
- [x] 1.2 Add a nullable `Target` field to `QuickCommand` in `PluginConfig.cs`, defaulting to null/absent for existing and manually-added entries, and verify a pre-change config still deserializes correctly - same reasoning as 1.1

## 2. Export: carry target identity

- [x] 2.1 Populate `Target` with the design name for a single-action Outfit alias, the `GestureId` for a single-action Gesture alias, and the markup-stripped status name for a single-action Moodle alias, in whichever export helper builds each category's `AliasExportEntry` list, and verify by exporting a fixture with one alias per category and checking each entry's decoded `Target` - implemented in `BuildExport`'s three category calls and `SingleActionTriggerEntries`/`TargetForSingleAction` for the Custom-Trigger-as-single-action path; verified via build + code review only (no test suite, no game client in this environment)
- [x] 2.2 Leave Title, Restraint, and Custom Trigger Bundle export entries with no `Target` populated, and verify they still export/import correctly with `Target` absent - `TargetForSingleAction`'s switch defaults to `null` for Title/Restraint kinds, and Title/Restraint/Bundle `AliasExportEntry` construction sites never pass a target argument

## 3. Import: same-target duplicate detection (Outfit/Gesture/Moodle)

- [x] 3.1 Populate `QuickCommand.Target` when importing a plain scanned Outfit/Moodle name (the name itself) and when importing a plain scanned Gesture entry (its id), and verify newly-imported entries carry the expected `Target` - `ImportPlainNames` gained a `targetSelector` parameter (identity for Outfit, `MoodlesTextFormat.StripMarkup` for Moodles, `null` for Restraints); `ImportGestureLines` sets `Target = entry.Id` directly
- [x] 3.2 Populate `QuickCommand.Target` when importing a single-action Outfit/Gesture/Moodle alias from its exported `Target`, and verify the same - `ImportAliasLines` now carries `entry.Target` onto the created `QuickCommand`
- [x] 3.3 Before adding a new Outfit/Gesture/Moodle entry during import, skip it if any existing entry in that category's list already has a matching `Target`..., and verify with a fixture - implemented as `isDuplicateTarget` checks in all three import helpers; verified via build + code review only, not an automated fixture test (none exists in this repo)
- [x] 3.4 Verify same-target dedup does not apply to Title, Restraints, or Custom Trigger Bundle imports - confirmed by construction: Title/Restraint/Bundle entries never get a non-null `Target` on export or import, so `isDuplicateTarget` is always false for them; only the (now-global) same-command check applies

## 4. Import: cross-category same-command duplicate detection

- [x] 4.1 Before any category import runs, build a single case-insensitive set of every command already present across all of `Titles/Outfits/Gestures/Moodles/Restraints/Aliases`, and skip adding any new entry (any category) whose command is already in that set - implemented as `usedCommands` in `ParseImport`, seeded up front and added to by every import helper as it goes (so an in-file cross-category collision is caught too, not just pre-existing ones); verified via build + code review only
- [x] 4.2 Verify re-importing the same file still behaves as before (no new duplicates, no regression) now that the check is global instead of per-category - the global set is a superset of each category's own prior exact-command check (every existing command in a category is also in the global set), so re-importing an unchanged file still adds zero and reports the same entries as duplicates it always would have; not verified via an automated fixture (none exists)

## 5. Import summary

- [x] 5.1 Add a `Duplicates` count to `CatalogImportResult`, incremented by both the same-target and cross-category same-command dedup paths, and verify `TotalAdded` still reflects only genuine additions - `TotalAdded` sums only the six per-category added counts, unchanged; `Duplicates` is a separate field
- [x] 5.2 Update `DrawImportCommandsButton`'s result text in `CollarWindow.cs` to append a duplicate-skipped count when `Duplicates > 0`, and verify the displayed text for a fixture that produces at least one duplicate - appended as `" N duplicate(s) skipped."` to both the "nothing new" and "imported N" branches; verified via build + code review only, no fixture harness in this repo

## 6. Verification

- [x] 6.1 Run `dotnet build` and verify it succeeds with no errors/warnings (no automated test suite exists in this repo) - Debug and Release builds both succeed, 0 warnings/0 errors
- [ ] 6.2 Manually exercise the golden path in-game or via the dev harness: Sub defines an Outfit alias and also has the same design reachable via the plain Wardrobe scan; Sub exports; Owner imports; verify only one Outfit quick command results, and the import summary reports the duplicate; also verify a same-alias-word collision across two categories is caught - NOT verified; requires the user's own in-game testing, not possible from this sandboxed environment
