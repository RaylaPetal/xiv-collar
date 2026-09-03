## 1. Empty-scope scanning

- [x] 1.1 Change wardrobe scanning so an empty folder allowlist includes every saved Glamourer design while non-empty allowlists retain prefix filtering; verify empty, one-folder, and multiple-folder cases.
- [x] 1.2 Change animation scanning to derive all installed Penumbra directories when no mods are selected and only explicit directories otherwise, without persisting the derived all-mod list; verify new mods are included automatically in empty-selection mode.
- [x] 1.3 Update wardrobe and animation selection/scan feedback to explain “empty means all,” and verify clearing visual folder/text filters shows every available row without mutating scope selections.

## 2. Configuration relocation and defaults

- [x] 2.1 Remove the safeword input/card from Settings while retaining the main-header editor and appropriate `/collarpanic` help; verify Settings contains no duplicate editable safeword state.
- [x] 2.2 Move leash engage/release trigger inputs from Settings into the Collar module with immediate validation/save behavior; verify edits affect subsequent alias resolution and no duplicate fields remain.
- [x] 2.3 Change new leash defaults to `leash`/`unleash` and add an idempotent migration that rewrites only the exact `leash-on`/`leash-off` legacy pair; verify partially or fully customized values are preserved.

## 3. Owner information architecture

- [x] 3.1 Wrap every Owner command category in an independently collapsible labeled section while preserving its existing add/import/compose/copy/send controls and saved rows; verify collapsing never mutates command data.
- [x] 3.2 Add concise category status/count information to collapsed Owner headers where practical; verify empty and populated categories remain distinguishable without expansion.
- [x] 3.3 Extend the navigation layout to group Sub modules on the left and align Owner at the far right; verify selection, hover tooltips, and click targets still work at normal and minimum widths.

## 4. Local pre-pair action testing

- [x] 4.1 Add a local-test coordinator that bypasses pairing and chat transport while enforcing each action's category permission plus the Gesture/Leash acknowledgement; verify disabled gates execute nothing and return visible reasons.
- [x] 4.2 Expose Test controls for configured title apply/clear, outfit apply/unlock, gesture playback, collar lock/unlock, Moodles apply/clear, and leash/unleash actions; verify each delegates to the same command-service behavior used by accepted Owner commands.
- [x] 4.3 Add transient per-action success/failure feedback without persisting test state or changing pairing; verify failed integrations identify the attempted action and no test composes or sends chat.
- [x] 4.4 Verify local tests work with no active or pending Owner pairing and that gesture/leash tests remain blocked until the automation-risk acknowledgement is enabled.

## 5. Documentation and verification

- [x] 5.1 Update README and inline help for empty-means-all scan scope, header-only safeword editing, Collar-module leash triggers, new defaults, collapsible Owner sections, and separated navigation.
- [x] 5.2 Extend README and inline help with local Test behavior, permission/acknowledgement prerequisites, and the guarantee that testing sends no chat; verify every supported action family is documented.
- [x] 5.3 Build the solution and run targeted scan-scope, migration, alias-resolution, local-test gating/dispatch, and UI-state tests; resolve all warnings/failures and validate the OpenSpec change strictly.
- [x] 5.4 Perform an in-game smoke test at minimum and normal widths covering empty/restricted scans, leash migration/editing, every Owner section, navigation separation, retained safeword behavior, and every local action Test while unpaired.
