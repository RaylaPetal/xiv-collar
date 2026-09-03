## 1. Restraints: export/import raw scanned designs

- [x] 1.1 Change `RestraintCommand.ExportNames()` to export every entry in `config.RestraintMapping.ScannedDesigns` by display name (tagged or not), and verify a scan with untagged designs produces a non-empty export
- [x] 1.2 Update `CatalogSyncService.ParseImport`'s restraints section handling so imported names populate `QuickCommands.Restraints` with no rules pre-assigned, and verify importing a file with untagged Restraint names yields matching quick-command entries with an empty rule set
- [x] 1.3 Verify re-importing the same file does not duplicate existing Restraints quick-command entries (existing dedup behavior preserved)

## 2. Restraints: Owner-assigned rules per quick command

- [x] 2.1 Extend the `QuickCommand` data model (or a Restraints-specific wrapper) to carry an optional rule assignment (forced pose + target, walk-only, action block, gag), and verify the config round-trips through save/load
- [x] 2.2 Add a per-entry rule-assignment UI to the Owner's Restraints quick-command section, reusing `DrawRestraintsModule`'s "Tag a new device" checkbox/pose-picker pattern, and verify the Owner can open it, select rules, and see them persist after closing the window
- [x] 2.3 Disable/hide the send action for a quick command with no rules assigned yet, and verify attempting to send an unconfigured entry is blocked with a visible reason
- [x] 2.4 Extend the `restraint lock <name>` command payload to carry the Owner-assigned rules, and verify the Sub's client parses the new payload fields
- [x] 2.5 Update the Sub's force-apply handling to activate exactly the rules carried in an Owner-forced command rather than consulting the Sub's own local device tag, and verify via a manual test: Sub tags a design with rule A, Owner force-applies the same design name with rule B, and rule B (not A) becomes active
- [x] 2.6 Verify existing conflict-refusal and panic-release behavior (`collar/restraints`) still applies unchanged to Owner-forced, rule-carrying commands

## 3. Gesture quick-command list rework

- [x] 3.1 Extract or adapt `AnimationPickerWindow`'s grouping (mod → group → animation) and search-filter logic into a routine usable by `DrawGestureQuickSection`, and verify it compiles and renders against a sample catalog
- [x] 3.2 Replace `DrawGestureQuickSection`'s flat scrolling list with the grouped/collapsible presentation, preserving existing per-row Send/Copy/Remove actions, and verify all three actions still work on an entry found via the new grouped view
- [x] 3.3 Add a search input above the list filtering visible entries by mod/group/animation/trigger name, and verify typing a search term narrows the visible entries and clearing it restores the full grouped list
- [ ] 3.4 Manually verify usability with a large catalog (1000+ entries): grouped view renders without a fixed-height flat scroll bottleneck and search narrows results promptly — **not run**: requires a live game session with a 1000+ entry gesture catalog, unavailable in this environment

## 4. Clear all placement and reset-imports control

- [x] 4.1 Add a right-aligned title-row helper (title text + `SameLine()` + right-aligned button) and verify it renders correctly at both a wide and the minimum supported window width
- [x] 4.2 Apply the helper to move "Clear all" onto the title row (far right) for Outfit, Gesture, Moodles, and Restraints quick-command sections, and verify each section's title row shows the button without overlapping the title text
- [x] 4.3 Add a "Reset imports" control next to "Import commands" in the Owner tab that clears `QuickCommands.Outfits`, `.Gestures`, `.Moodles`, and `.Restraints`, and verify triggering it empties all four lists while leaving `Titles`, `Follow`, and `Aliases` untouched
- [x] 4.4 Verify "Reset imports" persists the cleared state via `config.Save()` (lists remain empty after reopening the window)

## 5. Moodles: raw statuses instead of presets

- [x] 5.1 Confirm the exact Moodles IPC call-gate name(s) and payload shape for listing raw statuses and applying a single status, against the currently-integrated Moodles plugin version (see design.md Open Questions) — resolved via the vendored Moodles source (`/tmp/xiv-collar-moodles-source/Moodles/IPCProcessor.cs`): `GetRegisteredMoodlesV2` (list) and `AddOrUpdateMoodleByPlayerV2` (apply-by-GUID)
- [x] 5.2 Add the new `ICallGateSubscriber`(s) for the raw status-list and apply-by-status endpoints in `MoodlesIpc.cs`, and verify a manual scan against a running Moodles instance returns individual statuses rather than presets — subscriber wiring implemented and matches the confirmed contract; live-instance scan **not run** (no running game/Moodles session in this environment)
- [x] 5.3 Update `MoodlesCommand.Rescan()` to populate `config.MoodlesMapping.LocalCatalog` from raw statuses instead of `GetOwnPresets()`, and verify the scan UI shows the new statuses with correct success/zero-result/failure states
- [x] 5.4 Update `MoodlesCommand.ForceApply()`/clear to call the apply-by-status IPC endpoint, and verify an Owner apply-by-name command activates the named status on a test Sub client — code path implemented; live client-to-client test **not run**
- [x] 5.5 Update `MoodlesCommand.ExportNames()` to export raw status names, and verify the exported/imported names match `collar/catalog-sync`'s per-category expectations
- [x] 5.6 Document the breaking-change impact (old preset-based Owner quick commands stop resolving) in release notes/changelog — documented in `README.md` (no separate changelog file exists in this repo)

## 6. Cross-cutting validation

- [ ] 6.1 Run through every affected `collar/*` spec's scenarios (restraints, catalog-sync, moodles, ui-organization) manually or via existing test suite, and confirm each scenario's WHEN/THEN holds — **not run**: repo has no automated test suite for these scenarios and a live game session is unavailable here; verified statically against the code instead
- [ ] 6.2 Full manual pass: Sub scans Restraints (untagged) → exports → Owner imports → assigns rules → force-applies → Sub sees correct rules active; confirms the original "5 available / 0 imported" symptom is resolved end-to-end — **not run**: requires a live paired Owner/Sub game session, unavailable in this environment
