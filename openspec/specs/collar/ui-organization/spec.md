# collar/ui-organization Specification

## Purpose

Keeps Sub configuration and Owner command controls visually distinct, compact, and discoverable as the collar system gains more modules and settings.

## Requirements

### Requirement: Owner command categories are independently collapsible
The Owner command surface SHALL present each supported command category in its own labeled collapsible section, preserving the category's existing add, import, compose, copy, and send operations. Expanding or collapsing one category SHALL NOT change the saved commands or the expanded state of other categories during the same window session.

#### Scenario: Owner opens command surface
- **WHEN** the Owner command surface contains title, outfit, gesture, leash, Moodles, and general alias controls
- **THEN** each category is identifiable and can be expanded or collapsed independently

#### Scenario: Owner collapses a category
- **WHEN** the user collapses a category containing saved commands
- **THEN** its detailed controls and rows are hidden without deleting or changing any saved command

### Requirement: Owner navigation is separated from Sub modules
The main module navigation SHALL place the Owner command entry at the far-right edge, with visible separation from the cluster of Sub-facing configuration modules, while retaining keyboard/mouse accessibility and the current selected-module behavior.

#### Scenario: Main navigation is rendered
- **WHEN** the collar window draws its module navigation
- **THEN** Sub-facing modules remain grouped together and the Owner entry appears at the far right as a distinct destination

#### Scenario: Narrow navigation width
- **WHEN** the main window is at its supported minimum width
- **THEN** the separated Owner entry remains visible, selectable, and non-overlapping

### Requirement: Safeword has one canonical configuration surface
The main character header SHALL be the sole visible safeword configuration surface. Settings SHALL continue to explain how `/collarpanic` works when relevant but SHALL NOT display a second safeword input.

#### Scenario: User opens Settings
- **WHEN** the safeword editor is available in the main character header
- **THEN** Settings does not render a duplicate safeword input or a conflicting editable value

#### Scenario: User needs to configure safety
- **WHEN** the user views the main character header in any pairing state
- **THEN** the existing safeword editor remains available there

### Requirement: Owner Gesture quick-command list is grouped and searchable
The Owner's Gesture quick-command list SHALL present its entries grouped by their source mod and option group, with a text search control that filters visible entries by mod, group, animation, or trigger name, the same grouped/searchable presentation the Sub's animation picker window already provides. A flat, ungrouped scroll of every entry SHALL NOT be the list's only presentation once the list exceeds a small number of entries.

#### Scenario: Owner browses a large gesture quick-command list
- **WHEN** the Owner's Gesture quick-command list contains hundreds or thousands of entries
- **THEN** entries are shown grouped by mod and option group rather than as one flat scrolling list

#### Scenario: Owner searches the gesture quick-command list
- **WHEN** the Owner types text into the gesture list's search control
- **THEN** only entries whose mod, group, animation, or trigger name matches the search text remain visible

#### Scenario: Owner clears the search
- **WHEN** the Owner clears the search text
- **THEN** every gesture quick-command entry becomes visible again, grouped as before

### Requirement: Clear all sits at the far right of its section's title row
Each quick-command section that offers a "Clear all" control (Outfit, Gesture, Moodles, Restraints) SHALL place that control on the same row as the section's title, aligned to the far right, matching the placement already used elsewhere for a section title row.

#### Scenario: Owner views a quick-command section title row
- **WHEN** the Owner views the Outfit, Gesture, Moodles, or Restraints quick-command section
- **THEN** "Clear all" appears on the title row, aligned to the far right, rather than as a separate line below the title

### Requirement: Reset-imports control sits next to Import
The Owner command surface SHALL show a "Reset imports" control next to the existing "Import commands" control, distinct in label and effect from any single category's "Clear all" control.

#### Scenario: Owner locates the reset-imports control
- **WHEN** the Owner views the Owner command surface's import controls
- **THEN** "Reset imports" is visible immediately next to "Import commands"

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
