## ADDED Requirements

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
