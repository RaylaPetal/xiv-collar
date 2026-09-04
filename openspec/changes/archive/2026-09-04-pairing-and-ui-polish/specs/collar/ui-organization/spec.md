## REMOVED Requirements

### Requirement: Every Sub action can be tested locally before pairing
**Reason**: Superseded by `collar/chat-transport`'s existing "An Owner-style command can be tested entirely locally" control, which exercises the same underlying actions through the real dispatch path (trigger-phrase matching and permission/ToS gates included) rather than bypassing them - a strictly more faithful test with one control instead of one per action. The per-action buttons this requirement mandated were pure clutter once that control existed.
**Migration**: Use Settings' "Test an Owner command" control, typing the trigger phrase and alias/reserved-word text a real incoming tell would carry, for every category previously covered by a per-action button.

### Requirement: Sub can hide local Test controls
**Reason**: This setting existed only to manage clutter from the many per-action Test buttons. With those buttons removed (see "Every Sub action can be tested locally before pairing"), there is nothing left for it to hide.
**Migration**: No replacement setting is needed - the one remaining Test control (`collar/chat-transport`'s "An Owner-style command can be tested entirely locally") is always visible in Settings, the same as every other Settings control.

## ADDED Requirements

### Requirement: Settings' top cards never scroll internally
Settings' Identity & Pairing card, Automation risk acknowledgement card, and "Test an Owner command" card SHALL render directly into the window's own content flow rather than as fixed-height scrolling regions, so their content is never clipped or hidden behind an internal scrollbar regardless of pairing state or window size - the same layout already used for the Scan & Export section.

#### Scenario: Identity & Pairing shows a pending request without scrolling
- **WHEN** a pairing request is pending and the same-role warning is also showing
- **THEN** every line of the Identity & Pairing card, including the Accept/Reject buttons, is visible without an internal scrollbar

#### Scenario: Automation risk acknowledgement is never clipped
- **WHEN** the Settings window is at its minimum supported size
- **THEN** the Automation risk acknowledgement checkbox and its explanatory text are fully visible without an internal scrollbar

#### Scenario: Test-an-Owner-command card is never clipped
- **WHEN** the Settings window is at its minimum supported size
- **THEN** the test input, run button, and result are fully visible without an internal scrollbar

### Requirement: Automation risk acknowledgement is visible near the top of Settings
The Automation risk acknowledgement card SHALL render immediately after the Identity & Pairing card, before Scan & Export, so it is visible without scrolling to the bottom of the window in the common case.

#### Scenario: Settings opens at its default size
- **WHEN** a user opens Settings at its default window size
- **THEN** the Automation risk acknowledgement card is visible without scrolling past Scan & Export

### Requirement: Restraint rule checkboxes are laid out two per row
Every restraint restriction-rule checkbox editor (the Sub's device-capture editor, the Owner's per-quick-command editor, and the Owner's ad-hoc device editor) SHALL arrange its checkboxes (forced pose, walk-only, action block, Gagged, Arms Cuffed, Legs Cuffed, Full Body Cuffed) two per row instead of one per row, reducing the editor's vertical footprint. Each bound-animation rule's "Choose..." control and chosen-animation label SHALL remain attached to its own checkbox regardless of row position.

#### Scenario: Rule editor renders two checkboxes per row
- **WHEN** any restraint rule checkbox editor is drawn
- **THEN** its seven rule checkboxes appear across four rows of two (the last row holding one), rather than seven separate rows

#### Scenario: Bound-animation controls stay attached to their own checkbox
- **WHEN** Arms Cuffed, Legs Cuffed, or Full Body Cuffed is checked in the two-per-row layout
- **THEN** that rule's own "Choose..." button and chosen-animation label appear associated with that checkbox, not the one sharing its row
