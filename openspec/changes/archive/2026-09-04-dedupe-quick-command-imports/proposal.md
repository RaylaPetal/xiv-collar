## Why

Importing a Sub-exported file can leave the Owner with quick commands that are duplicates in effect even though nothing already catches them as duplicates in form: an Outfit/Gesture/Moodle alias and the plain scanned catalog entry can both resolve to the exact same Glamourer design, animation, or Moodles status, landing as two separate, unrelated-looking buttons that do the identical thing. Separately, nothing stops two entries anywhere in the Owner's quick commands from carrying the literal same alias word - since the wire tell is only ever the bare alias word, two entries sharing one word always send byte-identical text, so keeping both is redundant at best and confusing at worst (the Owner has no way to tell, from the UI alone, that pressing either button does exactly the same thing).

## What Changes

- Each single-action Outfit, Gesture, and Moodle alias now carries its target's identity (design, animation, or status id) alongside its existing human-readable description in the export file, so import-time matching no longer depends on parsing free text.
- Importing an Outfit, Gesture, or Moodle entry (whether a plain scanned name or a single-action alias) SHALL be skipped if the Owner's existing quick-command list for that category already has an entry targeting the exact same design/animation/status - regardless of whether the existing entry got there via its alias word or its plain scanned name.
- Importing any entry, in any category, SHALL be skipped if its exact command (the literal text sent over the wire) already exists anywhere else in the Owner's quick commands, not just within its own category's list as today.
- The "Import commands" result summary reports a duplicate-skipped count alongside the existing per-category added counts, so the Owner can see deduplication happened without the summary growing unbounded.
- Title, Restraints, and Custom Trigger Bundles are explicitly unchanged: Title has no shared "target" identity to compare (free text), Restraint devices are already captured and named individually by the Sub with no scan-derived duplicate source, and a bundle's actions are inherently allowed to overlap with other entries without that making the bundle itself a duplicate.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `collar/catalog-sync`: export carries target-identity data for Outfit/Gesture/Moodle single-action aliases; import gains same-target duplicate detection for Outfit/Gesture/Moodle and global (cross-category) same-command duplicate detection for every category, plus a duplicate-skipped count in the import summary.

## Impact

- `CollarSystem.Plugin/Commands/CatalogSyncService.cs`: `AliasExportEntry` gains a target-identity field; `ExportCategoryAliasEntries`/`DescribeOutfitAlias`/`DescribeGestureAlias`/`DescribeMoodleAlias` (or their callers) populate it; `ImportPlainNames`/`ImportAliasLines`/`ImportGestureLines` gain same-target and cross-category same-command duplicate checks; `CatalogImportResult` gains a duplicate-skipped count.
- `CollarSystem.Plugin/UI/CollarWindow.cs`: `DrawImportCommandsButton`'s result-summary text includes the duplicate count.
