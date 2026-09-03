## Context

See proposal.md - Why/What Changes. Relevant existing structure:

- Three independent scan cards in `SettingsWindow.cs` (`DrawWardrobeScanCard`, `DrawGestureScanCard`, `DrawMoodlesScanCard`), each with its own Rescan button, feedback, and a "Copy names"/"Copy" clipboard export in its own ad-hoc format: Wardrobe and Moodles copy plain names (one per line); Gesture copies versioned lines (`GestureCommand.ExportCatalog()`/`TryParseExport`, prefix `COLLAR-GESTURE-V1|<base64 json>`).
- Three per-category "Add from clipboard" buttons in `CollarWindow.DrawOwnerModule`'s quick-command sections, each calling either the generic `ImportQuickCommands(target, toCommand)` (Outfit, Moodles - parses plain name lines) or the Gesture-specific `ImportGestureCommands(target)` (parses versioned lines via `GestureCommand.TryParseExport`).
- `collar/restraints` (implemented in the prior `add-restraints-devices` change, not yet archived at the time this change was proposed) has its own scan/folder-allowlist (`PluginConfig.RestraintFolderAllowlist`, `RestraintCommand.Rescan()`, independent of Wardrobe's - see that change's own follow-up refactor) but no Settings scan section of its own yet, and no Owner-tab quick-command section. **This change assumes `add-restraints-devices` is archived before or alongside it** - `RestraintCommand`, `PluginConfig.RestraintMapping`, and the `restraint lock <name>`/`restraint unlock` override grammar it depends on only exist once that change's code is in place (it already is, in the working tree - only the OpenSpec archive step is pending).
- No file I/O exists anywhere in this codebase today - every export/import is clipboard-only. `Dalamud.Interface.ImGuiFileDialog.FileDialogManager` (confirmed present in the referenced Dalamud SDK build, no new package needed) is the standard Dalamud-plugin way to get a native save/open dialog; it needs its `Draw()` called every frame the dialog should render, the same shape `WindowSystem.Draw` already gets from `PluginInterface.UiBuilder.Draw`.

## Goals / Non-Goals

**Goals:**
- One scan action, one export file, one import action - without changing what each category's own catalog contains or how identity is matched (Gesture's mod+option+trigger triple, Outfit/Moodles/Restraints' plain names).
- Keep every per-category scope control (Wardrobe folder allowlist, Gesture mod picker) exactly as configurable as it is today - unifying the trigger button doesn't unify or remove the scoping.
- Reuse each category's existing per-line encoding inside the combined file rather than inventing a new universal entry format - Gesture's versioned lines carry more than a name (mod, option, trigger) and must not be flattened to a bare name.

**Non-Goals:**
- No cross-machine transport. The exported file still has to reach the Owner however the Sub already would (Discord, a shared folder, screen share) - this change only removes the requirement that both sides be copy-pasting on the same screen at the same time.
- No change to any category's own scan logic, permission gating, or apply/force-apply behavior - this is purely a scan-trigger/export/import UX consolidation.
- No general-purpose settings/config file export - only the four commandable catalogs.

## Decisions

**Combined file format: one `.txt` with `## <CATEGORY>` section headers, each section's body in that category's own existing per-line encoding.**
```
## WARDROBE
<plain name>
<plain name>
## GESTURE
COLLAR-GESTURE-V1|<base64 json>
COLLAR-GESTURE-V1|<base64 json>
## MOODLES
<plain name>
## RESTRAINTS
<plain name>
```
An empty category still emits its header with zero body lines (satisfies the spec's "empty category still represented" requirement, so a re-export after clearing a category doesn't get silently misread as "section absent, don't touch existing quick commands" on import - explicit emptiness is unambiguous, a missing header would be too). Chosen over a single interleaved/prefixed-line format (e.g. one universal `COLLAR-<CATEGORY>-V1|` prefix per line) because it requires zero changes to Gesture's already-shipping `ExportCatalog`/`TryParseExport` pair - the unified writer/reader just wraps each category's untouched output between its own header markers instead of re-encoding it.

**New `CatalogSyncService` owns the combined build/parse; it does not replace each category's own export/import methods, it composes them.** `BuildExport()` calls `OutfitCommand`'s/`MoodlesCommand`'s name lists, `GestureCommand.ExportCatalog()`, and a new `RestraintCommand` name-list method, and wraps each under its header. `ParseImport(text)` splits on `## ` headers and, per section, reuses the exact matching/dedup logic `ImportQuickCommands`/`ImportGestureCommands` already have today (moved from `CollarWindow` into `CatalogSyncService` so both the file-based path and - if ever needed later - a clipboard fallback share one implementation) plus a new equivalent for Restraints. Returns a per-category added-count summary for the Owner-facing result line.

**File I/O via `FileDialogManager`, owned by `Plugin` and drawn alongside `WindowSystem`.** One `FileDialogManager` instance lives on `Plugin` (like `WindowSystem` does), its `Draw()` called from the same `PluginInterface.UiBuilder.Draw` hook `WindowSystem.Draw` already uses. Export uses `SaveFileDialog` with a default filename like `collar-export-<TriggerPhrase-or-generic>.txt`; Import uses `OpenFileDialog` filtered to `.txt`. Both are async/callback-based (the dialog renders across multiple frames until the user picks or cancels) - the callback runs the existing `CatalogSyncService` build/parse synchronously once a path is chosen, consistent with how every other action in this plugin (scans, applies) already runs synchronously on the main thread.

**Owner tab: one centered "Import commands" button above the existing per-category `DrawOwnerSection` list, each category's own section keeps its list/send/remove UI and only loses its individual import button.** Matches the "keep per-category sections, remove only their import buttons" decision. The button's result (per-category added counts, or a file-read error) shows directly beneath it, the same transient-feedback shape `outfitImportError`/`gestureImportError`/`moodlesImportError` already use today (consolidated into one `importResult` string field instead of three).

**Restraints gains parity Owner-tab quick commands and a plain-name export, matching Outfit/Moodles rather than Gesture's richer format.** A restraint device is identified by name alone for Owner purposes (the same `restraint lock <name>` override grammar `ChatCommandListener.HandleForceRestraint` already resolves by name against the Sub's tagged catalog) - no need for a versioned entry the way Gesture needs its option/trigger metadata preserved.

## Risks / Trade-offs

- **[`FileDialogManager` behavior/API surface not exercised anywhere else in this codebase]** → Confirmed present in the referenced Dalamud SDK build by inspecting `Dalamud.dll` directly (class and `SaveFileDialog`/`OpenFileDialog`/`Draw` members all present), but its exact callback signature should be double-checked against the actual referenced Dalamud API docs/samples at implementation time rather than assumed from memory - flag if it differs from the shape assumed here.
- **[A stale exported file re-imported later re-adds already-removed entries]** → Out of scope to prevent structurally; the spec's dedup requirement only guards against *duplicate* entries for something already present, not staleness. Same limitation the current per-category clipboard import already has.
- **[Combined file grows large with big Gesture catalogs]** → No practical limit expected (this mirrors what today's three separate clipboard blobs already contain, just concatenated into one file instead of three clipboard copies) - not treated as a real risk.

## Migration Plan

Additive except for removing the three per-category import buttons and their now-unused `ImportQuickCommands`/`ImportGestureCommands`/`outfitImportError`/`gestureImportError`/`moodlesImportError` call sites (superseded by `CatalogSyncService` and one `importResult` field) and replacing the three Settings scan cards with one unified section. No config schema change beyond `OwnerQuickCommands.Restraints` (a new empty list, defaults cleanly for existing installs) and no change to any already-saved quick command or catalog entry.
