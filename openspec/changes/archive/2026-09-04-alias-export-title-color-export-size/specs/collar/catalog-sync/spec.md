## MODIFIED Requirements

### Requirement: Exporting every catalog to one file
The system SHALL let the Sub export the current Wardrobe, Gesture, Moodles, Restraints, and Aliases catalogs together as a single text file written to a location the Sub chooses, in a format that preserves each category's own identity guarantees (the same information that category's individual export already provides). A category with an empty catalog SHALL be included in the export as empty, not omitted in a way that would prevent re-import from recognizing that category. The Aliases section SHALL contain only the deduplicated alias words defined in the Sub's Title, Outfit, Gesture, and Restraint alias lists - never what each alias maps to. Each exported Gesture entry SHALL carry only the fields the Owner's import needs to identify and group it (its id, mod name, animation name, group name, group order, option order, and its playable trigger) - it SHALL NOT carry fields meaningful only to the Sub's own local playback (such as per-entry mod-option selection state), so the exported file's size scales with the number of catalog entries, not with how many configurable option groups their source mods happen to have.

#### Scenario: Export produces one file covering every category
- **WHEN** the Sub triggers the export action after scanning
- **THEN** a single file is written containing the current Wardrobe, Gesture, Moodles, Restraints, and Aliases catalogs, each identifiable as belonging to its own category

#### Scenario: An empty category is still represented
- **WHEN** one of the five categories has no scanned, tagged, or defined entries at export time
- **THEN** the exported file still identifies that category with zero entries, rather than omitting it entirely

#### Scenario: Aliases export contains only names
- **WHEN** the Sub has defined aliases in the Title, Outfit, Gesture, or Restraint sections
- **THEN** the Aliases section of the exported file lists each alias word once, deduplicated, with no indication of what any alias applies or maps to

#### Scenario: Gesture export omits local-only playback data
- **WHEN** the Sub exports a Gesture catalog whose source mods have multiple configurable option groups
- **THEN** each exported entry's size reflects only its own identifying fields, not the full set of every other option group's current selection state

### Requirement: Importing one file fills every category's quick commands
The system SHALL let the Owner import a previously exported file in a single action, and SHALL populate each category's quick-command list from the corresponding section of that file, using the same per-category matching/deduplication behavior each category's own individual import already provides. An entry already present in a category's quick-command list SHALL NOT be duplicated by importing a file that contains it again. Restraints entries SHALL import from the file's raw scanned design names (tagged or not) and SHALL be added to the Owner's quick-command list without restriction rules pre-assigned; the Owner assigns rules per entry after import (see `collar/restraints`). Aliases entries SHALL import as described in "Owner imports alias names as one-off quick commands."

#### Scenario: Importing a file fills every category at once
- **WHEN** the Owner imports a file previously exported by a paired Sub
- **THEN** the Owner's Wardrobe, Gesture, Moodles, Restraints, and Aliases quick-command lists are each populated from that file's corresponding section, in one action

#### Scenario: Re-importing the same file does not duplicate entries
- **WHEN** the Owner imports a file whose entries for a category are already present in that category's quick-command list
- **THEN** no duplicate entries are added for that category

#### Scenario: Importing a file with an empty category section changes nothing for that category
- **WHEN** an imported file identifies a category with zero entries
- **THEN** that category's existing quick-command list is left unchanged

#### Scenario: Importing a file with scanned but untagged restraints
- **WHEN** the Owner imports a file whose Restraints section lists designs the Sub scanned but never tagged as a device
- **THEN** each of those designs is added to the Owner's Restraints quick-command list without any restriction rules assigned

### Requirement: Owner can reset every import to a blank slate
The system SHALL let the Owner clear every import-populated quick-command list (Wardrobe/Outfit, Gesture, Moodles, Restraints, Aliases) back to empty in a single action, distinct from importing a file and distinct from each category's individual "Clear all" control. Since Aliases shares one list between imported entries and any the Owner typed manually into the freeform "Alias / one-off" control, resetting SHALL clear that entire list, the same coarse whole-list reset already accepted for Restraints' manually-added entries.

#### Scenario: Owner resets all imports
- **WHEN** the Owner triggers the reset-imports action
- **THEN** the Wardrobe/Outfit, Gesture, Moodles, Restraints, and Aliases quick-command lists are all emptied, and categories not populated by import (Title, Follow) are left unchanged

#### Scenario: Reset control is placed next to import
- **WHEN** the Owner views the import controls
- **THEN** the reset-imports control is visible alongside the "Import commands" control

#### Scenario: Resetting clears manually-typed one-off aliases too
- **WHEN** the Owner has both imported alias words and manually typed one-off commands into the same "Alias / one-off" list, and triggers reset-imports
- **THEN** the entire list is emptied, the same as Restraints' existing manually-added entries are already cleared by reset-imports today

## ADDED Requirements

### Requirement: Owner imports alias names as one-off quick commands
The system SHALL let the Owner import a Sub-exported file's Aliases section into the same quick-command list the Owner's freeform "Alias / one-off" control populates, adding one entry per alias word with the raw alias text as its command - exactly as if the Owner had typed that word into the freeform control themselves. An alias word already present in that list (whether previously imported or manually typed by the Owner) SHALL NOT be duplicated.

#### Scenario: Importing alias names populates the one-off list
- **WHEN** the Owner imports a file whose Aliases section lists one or more alias words
- **THEN** each of those words is added to the Owner's "Alias / one-off" quick-command list as a ready-to-send entry carrying that exact word

#### Scenario: Re-importing the same aliases does not duplicate entries
- **WHEN** the Owner imports a file whose Aliases section contains a word already present in the one-off list
- **THEN** no duplicate entry is added for that word
