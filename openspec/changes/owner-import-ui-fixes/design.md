## Context

See `proposal.md` - Why. Relevant current-state details (from direct code inspection):

- **Restraints**: `RestraintCommand.Rescan()` populates `config.RestraintMapping.ScannedDesigns` (the "N available" count). `RestraintCommand.ExportNames()` reads only `config.RestraintMapping.Devices` (populated solely by the Sub's manual `TagDevice(...)`, which also assigns rules). `CatalogSyncService.ParseImport` adds whatever `ExportNames()` produced. Scan count and export count are two different collections today.
- **Gesture menu**: `CollarWindow.DrawGestureQuickSection` renders `plugin.Configuration.QuickCommands.Gestures` as one flat list in a fixed-height scrolling child, one row per `DrawSavedQuickRow` call. `AnimationPickerWindow` already implements the target UX: a search box filtering by mod/group/animation/trigger, a rescan button, a shown/discovered counter, and `CollapsingHeader` (mod) → `TreeNodeEx` (group) nesting.
- **Clear all placement**: today's four "Clear all" buttons (`CollarWindow.cs` lines ~779/809/859/883) sit on their own line under the section's title text. No existing "title ... [button on far right]" row exists anywhere in the UI yet; the closest precedent for right-alignment math is `AnimationPickerWindow.cs`/`SafewordEditor.cs`, which right-pad an input box to leave room for a trailing same-line button.
- **Import button**: `CollarWindow.DrawImportCommandsButton` sits at the top of the Owner tab, above all per-category sections. It aggregates counts from `CatalogSyncService.ParseImport` into `CatalogImportResult.TotalAdded`.
- **Moodles IPC**: `MoodlesIpc.cs` only wires the preset-level call gates (`Moodles.GetPresetsInfoListV2`, `Moodles.ApplyPresetByPlayerV2`, `Moodles.ClearStatusManagerByPlayerV2`). `MoodlesCommand` consumes `GetOwnPresets()` and stores `MoodlesPresetEntry{PresetId, Name}` into `config.MoodlesMapping.LocalCatalog`. No raw per-status call gate exists yet.

## Goals / Non-Goals

**Goals:**
- Make restraint import produce usable entries from a scan alone, with the Owner - not the Sub - deciding each entry's restriction rules.
- Bring the Owner's Gesture quick-command list to the same usable scale (1000+ items) as the existing animation picker.
- Standardize "Clear all" placement and add a one-action import reset.
- Switch Moodles' local catalog source from presets to raw statuses.

**Non-Goals:**
- Redesigning the Sub-side Restraints tab or its own tagging UI (`DrawRestraintsModule` stays as-is for Sub self-use via alias).
- Building a generic reusable "picker widget" component library; the gesture list adopts the animation picker's layout pattern, not a shared abstraction, unless the implementer finds extraction trivial.
- Changing Wardrobe/Outfit or general-alias import/export behavior.
- Preserving old Moodles preset-based quick commands across the switch to raw statuses (explicitly a breaking change per proposal.md).

## Decisions

### 1. Restraint rules move from "Sub-tagged device" to "Owner-assigned per quick command"
Export/import now carries every scanned design name (tagged or not). The Owner assigns rules (forced pose + pose target, walk-only, action block, gag) to each restraint quick command locally, using the same rule set and UI pattern as `DrawRestraintsModule`'s "Tag a new device" section (checkboxes + pose picker). The `restraint lock <name>` command payload is extended to carry the Owner's chosen rules, and the Sub's client applies exactly those rules on receipt - it does not consult its own local tag for that design when the command is Owner-forced.

**Alternative considered**: keep rules tied to the Sub's local tag and just fix the export to also include untagged designs (so the Owner could reference them, but sending one with no local Sub tag would do nothing). Rejected because it doesn't satisfy the actual ask - the Owner needs to be able to decide behavior directly, not merely see more names to pick from that still silently no-op without Sub-side setup.

**Alternative considered**: require Owner-assigned rules to exactly replicate the Sub's own rule-conflict validation client-side before sending. Deferred - the Sub's client already runs the existing conflict-refusal logic (`collar/restraints` - "Conflicting rule requests are refused") on any incoming force-apply, so re-validating on the Owner's side is redundant, not required for correctness.

### 2. Gesture quick-command list reuses `AnimationPickerWindow`'s layout, not its window
The rework happens inline inside `DrawGestureQuickSection` (same embedded-section context the current flat list uses), reusing the grouping/search/collapsing logic from `AnimationPickerWindow` as a shared drawing routine rather than popping a separate window, since the quick-command list is a management view (send/copy/remove existing entries) rather than a picker that hands back a single selection.

**Alternative considered**: extract a fully shared generic component used by both `AnimationPickerWindow` and the new quick-command view. Left as an implementation-time judgment call in `tasks.md` rather than mandated here, since the two views' row content (pick-and-close vs. send/copy/remove-in-place) differ enough that forcing one shared component could add more complexity than it saves.

### 3. "Clear all" placement is a new right-aligned title-row helper
Since no exact "title ... [button]" precedent exists, a small helper (e.g., drawing the title via `IconGlyph.Text`, then `SameLine()` + `SetCursorPosX(GetContentRegionAvail().X - buttonWidth)` before the button) is added and reused across all four sections (Outfit, Gesture, Moodles, Restraints), based on the right-alignment math already used in `AnimationPickerWindow.cs`/`SafewordEditor.cs`.

### 4. Reset-imports is an explicit, separate control - not a repurposed "Clear all"
"Reset imports" clears all four import-populated `QuickCommands` lists (`Outfits`, `Gestures`, `Moodles`, `Restraints`) in one call, placed next to "Import commands" at the top of the Owner tab. It does not touch `Titles`, `Follow`, or `Aliases`, which are hand-built by the Owner and never import-populated. This mirrors the existing "Scan all" pattern in `SettingsWindow.cs` that batches four individual `Rescan()` calls.

### 5. Moodles switches its call gate from preset-level to status-level IPC
`MoodlesIpc.cs` gains a new `ICallGateSubscriber` for Moodles' raw status-list endpoint and a matching apply-by-status endpoint, alongside (not replacing, until cutover) the existing preset gates. `MoodlesCommand.Rescan()/ForceApply()/ExportNames()` are repointed to the new raw-status source. The exact IPC signature names must be confirmed against the currently-integrated Moodles plugin version (see Open Questions).

## Risks / Trade-offs

- **[Risk]** Extending the `restraint lock` command payload to carry rules is a wire-format change. → Mitigation: version-guard or additively encode the new rule fields so an older paired client degrades gracefully (ignores unknown fields) rather than crashing; confirm the existing message envelope already supports additive fields before implementation.
- **[Risk]** Switching Moodles from presets to raw statuses breaks every existing Owner Moodles quick command (named proposal.md as **BREAKING**). → Mitigation: none needed beyond the documented breaking-change note; Owners re-import after the Sub rescans.
- **[Risk]** The real Moodles IPC may not expose a raw per-status apply call (only preset-level apply), only a status list for display. → Mitigation: this is the primary Open Question below; if apply-by-status truly doesn't exist upstream, this decision may need revisiting before task 5 can complete as specified.
- **[Trade-off]** Reusing `AnimationPickerWindow`'s grouping logic inline (rather than extracting a shared component) risks minor drift between the two views over time. Accepted for now to avoid over-engineering a two-caller abstraction.

## Open Questions

- What is the exact Moodles IPC call-gate name and payload shape for listing raw statuses and applying a single status by GUID/name (mirroring `Moodles.GetPresetsInfoListV2`/`Moodles.ApplyPresetByPlayerV2`)? This must be confirmed against the currently-supported Moodles plugin version before implementing task area 5; it doesn't change the spec (`collar/moodles` only requires "Moodles' supported local status-list interface") or the chosen approach (still a `MoodlesIpc.cs` call-gate addition), only the concrete gate name(s) used.
