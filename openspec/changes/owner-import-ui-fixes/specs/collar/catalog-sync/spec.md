## MODIFIED Requirements

### Requirement: Importing one file fills every category's quick commands
The system SHALL let the Owner import a previously exported file in a single action, and SHALL populate each category's quick-command list from the corresponding section of that file, using the same per-category matching/deduplication behavior each category's own individual import already provides. An entry already present in a category's quick-command list SHALL NOT be duplicated by importing a file that contains it again. Restraints entries SHALL import from the file's raw scanned design names (tagged or not) and SHALL be added to the Owner's quick-command list without restriction rules pre-assigned; the Owner assigns rules per entry after import (see `collar/restraints`).

#### Scenario: Importing a file fills every category at once
- **WHEN** the Owner imports a file previously exported by a paired Sub
- **THEN** the Owner's Wardrobe, Gesture, Moodles, and Restraints quick-command lists are each populated from that file's corresponding section, in one action

#### Scenario: Re-importing the same file does not duplicate entries
- **WHEN** the Owner imports a file whose entries for a category are already present in that category's quick-command list
- **THEN** no duplicate entries are added for that category

#### Scenario: Importing a file with an empty category section changes nothing for that category
- **WHEN** an imported file identifies a category with zero entries
- **THEN** that category's existing quick-command list is left unchanged

#### Scenario: Importing a file with scanned but untagged restraints
- **WHEN** the Owner imports a file whose Restraints section lists designs the Sub scanned but never tagged as a device
- **THEN** each of those designs is added to the Owner's Restraints quick-command list without any restriction rules assigned

## ADDED Requirements

### Requirement: Owner can reset every import to a blank slate
The system SHALL let the Owner clear every import-populated quick-command list (Wardrobe/Outfit, Gesture, Moodles, Restraints) back to empty in a single action, distinct from importing a file and distinct from each category's individual "Clear all" control.

#### Scenario: Owner resets all imports
- **WHEN** the Owner triggers the reset-imports action
- **THEN** the Wardrobe/Outfit, Gesture, Moodles, and Restraints quick-command lists are all emptied, and categories not populated by import (Title, Follow, Aliases) are left unchanged

#### Scenario: Reset control is placed next to import
- **WHEN** the Owner views the import controls
- **THEN** the reset-imports control is visible alongside the "Import commands" control
