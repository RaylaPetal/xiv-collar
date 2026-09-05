## Why

Owner gesture commands can still arrive in the legacy opaque-ID form yet fail silently, and several authoring surfaces become dead ends after creation: the local command tester is awkward to paste into, saved commands cannot be edited, and dense plain-text summaries expose internal category wording rather than presenting a readable scene overview. These gaps make working features feel unreliable and make ordinary correction require deleting and rebuilding entries.

## What Changes

- Restore deterministic execution of both legacy opaque gesture IDs and the newer quoted readable gesture selectors, with diagnostics that distinguish missing, ambiguous, and failed playback.
- Make the Settings local command-test field behave like a normal editable text field with keyboard selection, copy, cut, paste, and replacement.
- Add editing flows for saved Sub aliases/custom triggers and Owner quick commands/custom bundles, using a focused modal or popup editor where inline editing would be too crowded.
- Let editors rename labels, replace command targets, add/remove/reorder bundled actions, and revise restraint rules without forcing delete-and-recreate workflows.
- Let Arms Cuffed, Legs Cuffed, and Full Body Cuffed select triggerless idle animations that only require enabling their Penumbra mod/options, instead of forcing an unrelated pose or gesture selection.
- Redraw the Sub after bound restraint settings are removed so unlocking visibly restores the prior Penumbra animation state immediately.
- Replace raw comma-joined summaries such as `restraint body cuffed, restraint gagged, moodle exhibitionists` with consistent, friendly labels, grouping, icons/chips, capitalization, and compact detail views across Restraints, Custom Triggers, and Owner quick commands.
- Preserve stable machine identities and legacy command compatibility underneath the presentation layer; editing or visual cleanup must not weaken permissions, pairing, consent, or direct-send gates.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `collar/gesture`: Legacy IDs and readable selectors must both resolve and execute reliably with useful local failure diagnostics.
- `collar/chat-transport`: The local Owner-command test input must support normal clipboard/text-editing interactions.
- `collar/custom-triggers`: Saved bundles become editable and their action lists receive structured, human-friendly presentation.
- `collar/catalog-sync`: Saved/imported Owner quick commands become editable without losing source identity or creating duplicate/migration regressions.
- `collar/restraints`: Saved restraint commands and rule assignments become editable and render with friendly names and structured summaries; bound rules support triggerless idle activation and redraw after release.
- `collar/ui-organization`: Command-authoring and saved-entry presentation follows one consistent visual/editing pattern across modules.

## Impact

Affected areas include gesture parsing/resolution and playback diagnostics, the Settings command-test control, `QuickCommand` and `CustomTriggerDefinition` editing state, restraint animation eligibility and lifecycle, restraint rule editors, list-row/action-summary rendering, configuration persistence, imported-entry provenance, and the Owner/Sub command authoring UI. Existing saved configuration and legacy wire commands remain supported without a destructive migration.
