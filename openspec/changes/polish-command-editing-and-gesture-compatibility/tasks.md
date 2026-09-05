## 1. Gesture Compatibility and Diagnostics

- [x] 1.1 Trace incoming `gesture` payload extraction through quoted and unquoted forms, implement one normalized selector decode result, and verify parser tests cover legacy ID `ce7d75cb813f295c`, readable selectors, malformed quotes, missing entries, and ambiguous labels.
- [x] 1.2 Refactor gesture resolution and execution to return stage-specific outcomes for lookup, temporary Penumbra settings, redraw, and trigger playback; verify real-listener and local-test paths report the same outcome and roll back partial activation.
- [x] 1.3 Add focused gesture execution tests for slash-emote and pose entries using both legacy IDs and readable selectors, and verify each format selects the identical catalog entry and playback path.

## 2. Editable Local Command Test

- [x] 2.1 Audit the Settings test input wrapper, buffer lifecycle, focus handling, and flags; make it a normal editable text control and verify typing, cursor movement, selection, select-all, copy, cut, paste, replacement, and deletion work without sending chat.
- [x] 2.2 Verify pasted commands still execute only when Run is explicitly chosen and pass through the existing no-pairing, no-sender, permission/acknowledgement, and dispatch checks unchanged.
- [x] 2.3 Add a complete configured-trigger dropdown to the local Owner-command tester, show the composed command, and execute the selection through the existing local dispatch path without sending chat.

## 3. Shared Saved-Entry Editing Framework

- [x] 3.1 Add reusable focused editor/modal state with cloned drafts, stable source-entry identity, atomic Save, Cancel, validation messaging, and stale-source detection; verify cancelling and invalid saves never mutate persisted config.
- [x] 3.3 Implement simple-category Owner quick-command editors for Title, Outfit, Gesture, Follow, and Moodles; verify rename-only edits preserve source provenance, favorite state, stable target metadata, and command behavior while target changes update identity and command together.

## 4. Custom Trigger Editing

- [x] 4.1 Refactor the existing action-creation controls into reusable draft controls supporting add, edit, remove, and reorder for every Custom Trigger action kind; verify action-specific validation and picker behavior match creation.
- [x] 4.2 Add in-place saved-entry editing for Sub Custom Triggers and Owner custom bundles through the focused editor; verify Save updates the existing entry, Cancel is lossless, and execution observes the revised ordered actions.
- [x] 4.3 Preserve alias/reserved-word rules, imported/manual provenance, stable action target identities, and per-action permission gates through edits; verify legacy saved bundles load and execute unchanged before their first edit.

## 5. Restraint Editing

- [x] 5.1 Add a focused editor for Sub-captured restraint devices that preserves device ID while allowing friendly name, equipment item, rules, pose, and bound animations to change; verify existing aliases continue to target the edited device.
- [x] 5.2 Add focused editing for Owner saved restraint quick commands and ad-hoc restraint definitions, reusing the imported-Sub animation picker and rule validation; verify revised rules and animation metadata regenerate the exact displayed and sent payload.
- [x] 5.3 Validate missing/stale bound animations, empty rule sets, duplicate names, and unsafe command lengths before Save; verify invalid drafts cannot replace a working persisted restraint entry.
- [x] 5.4 Add a restraint-specific animation-picker mode that includes valid triggerless idle entries while leaving Gesture selection limited to playable triggers; verify an idle with no pose/emote can be selected for Arms, Legs, and Full Body rules.
- [x] 5.5 Carry triggered-versus-enable-only behavior through saved rules, readable/legacy command resolution, and receiver validation; verify legacy selections remain compatible and no fake ground-sit trigger is introduced.
- [x] 5.6 Apply triggerless bound animations by enabling their complete temporary Penumbra settings and redrawing without issuing a gesture command; verify triggered selections retain delayed playback and both paths roll back atomically on apply failure.
- [x] 5.7 Centralize bound-animation release so per-device unlock, Owner force-unlock, panic, and rollback remove temporary settings before one coalesced redraw; verify redraw failure is diagnosed but never blocks other restriction or slot teardown.

## 6. Unified Friendly Presentation

- [x] 6.1 Introduce shared presentation descriptors for command categories, Custom Trigger action kinds, restraint rules, readable targets, warnings, and optional technical detail; verify serializers and stable IDs are not derived from display strings.
- [x] 6.2 Replace comma-joined Custom Trigger draft/saved summaries with structured, consistently capitalized action rows or badges; verify a Body Cuffed + Gagged + Exhibitionists bundle stays readable at default and minimum window widths.
- [x] 6.3 Apply the shared row hierarchy, friendly rule names, readable animation detail, active/stale warnings, and consistent action placement across Sub restraints and every Owner quick-command category; verify raw enum names, opaque IDs, and `rules:` syntax do not appear in ordinary views.
- [x] 6.4 Verify long labels wrap or expose full detail without clipping, keyboard/mouse navigation remains usable, and Send/Copy/Favorite/Remove semantics are unchanged by the presentation refactor.
- [x] 6.5 Make Wardrobe `unlock` and Owner `restraint unlock` fixed, documented release commands while retaining per-device restraint alias toggles.

## 7. Integration and Regression Verification

- [x] 7.1 Add configuration round-trip tests for edited legacy/imported/manual quick commands, restraint devices, and Custom Triggers; verify stable identities, provenance, favorites, alias links, and action order survive reload.
- [ ] 7.2 Run end-to-end local command tests for legacy/readable gestures and edited commands, plus UI interaction checks for command-test clipboard behavior and editor Save/Cancel; verify pairing, consent, permissions, and direct-send gates have no regressions.
- [ ] 7.3 Add triggered and triggerless restraint fixtures covering picker eligibility, stable identity round-trip, enable-only apply, triggered playback, multi-rule coalesced unlock redraw, panic, and redraw failure diagnostics.
- [ ] 7.4 Build Debug and Release, inspect the packaged manifest/artifacts, run the full available automated suite, and record a manual in-game verification matrix for gesture playback, triggerless restraint idles, unlock redraw, and the polished editing flows.
