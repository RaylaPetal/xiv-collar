## MODIFIED Requirements

### Requirement: Exporting every catalog to one file
The system SHALL let the Sub export the current Title, Wardrobe, Gesture, Moodles, Restraints, and Custom Trigger Bundle catalogs together as a single text file written to a location the Sub chooses, in a format that preserves each category's own identity guarantees (the same information that category's individual export already provides). A category with an empty catalog SHALL be included in the export as empty, not omitted in a way that would prevent re-import from recognizing that category. Each single-action alias defined in the Sub's Title, Outfit, Gesture, Restraint, or Moodle alias lists, and each Custom Trigger that bundles exactly one action, SHALL be exported under that action's own category section (Title/Outfit/Gesture/Restraint/Moodle), carrying the same human-readable summary of what it does that the export already provides for that category's other entries. The Custom Trigger Bundle section SHALL contain only Custom Triggers that bundle two or more actions, each carrying a human-readable summary of every action in the bundle. Each exported Gesture entry SHALL carry only the fields the Owner's import needs to identify and group it (its id, mod name, animation name, group name, group order, option order, and its playable trigger) - it SHALL NOT carry fields meaningful only to the Sub's own local playback (such as per-entry mod-option selection state), so the exported file's size scales with the number of catalog entries, not with how many configurable option groups their source mods happen to have.

#### Scenario: Export produces one file covering every category
- **WHEN** the Sub triggers the export action after scanning
- **THEN** a single file is written containing the current Title, Wardrobe, Gesture, Moodles, Restraints, and Custom Trigger Bundle catalogs, each identifiable as belonging to its own category

#### Scenario: An empty category is still represented
- **WHEN** one of the six categories has no scanned, tagged, or defined entries at export time
- **THEN** the exported file still identifies that category with zero entries, rather than omitting it entirely

#### Scenario: A single-action alias exports under its own category
- **WHEN** the Sub has defined a Title, Outfit, Gesture, Restraint, or Moodle alias, or a Custom Trigger that bundles exactly one action
- **THEN** that entry appears in the export file's section for its own action category, not in a separate generic aliases section

#### Scenario: A multi-action bundle exports separately from single-action entries
- **WHEN** the Sub has defined a Custom Trigger that bundles two or more actions
- **THEN** that entry appears only in the Custom Trigger Bundle section, alongside a summary of every action it bundles

#### Scenario: Gesture export omits local-only playback data
- **WHEN** the Sub exports a Gesture catalog whose source mods have multiple configurable option groups
- **THEN** each exported entry's size reflects only its own identifying fields, not the full set of every other option group's current selection state

#### Scenario: Aliases export contains only names
- **WHEN** the Sub has defined Custom Triggers that bundle two or more actions
- **THEN** the Custom Trigger Bundle section of the exported file lists each bundle's alias word once, deduplicated

#### Scenario: Aliases export reveals what each alias does
- **WHEN** the Sub has defined one or more Custom Triggers that bundle two or more actions
- **THEN** each bundle's alias word in the exported file's Custom Trigger Bundle section is paired with a human-readable summary of every action it bundles

#### Scenario: Moodles and Custom Trigger alias words are included in the export
- **WHEN** the Sub has defined one or more Moodles aliases and one or more Custom Trigger bundles that include a Moodle action
- **THEN** the Moodles alias appears in the export's Moodle section and each multi-action bundle appears in the Custom Trigger Bundle section, each with its own description

### Requirement: Importing one file fills every category's quick commands
The system SHALL let the Owner import a previously exported file in a single action, and SHALL populate each category's quick-command list - Title, Outfit, Gesture, Moodles, Restraints, and Custom Trigger Bundles - from the corresponding section of that file, using the same per-category matching/deduplication behavior each category's own individual import already provides. An entry already present in a category's quick-command list SHALL NOT be duplicated by importing a file that contains it again. Restraints entries SHALL import from the file's raw scanned design names (tagged or not) and SHALL be added to the Owner's quick-command list without restriction rules pre-assigned; the Owner assigns rules per entry after import (see `collar/restraints`). Single-action aliases and single-action Custom Triggers SHALL import as described in "Owner imports single-action aliases into their matching category."

#### Scenario: Importing a file fills every category at once
- **WHEN** the Owner imports a file previously exported by a paired Sub
- **THEN** the Owner's Title, Outfit, Gesture, Moodles, Restraints, and Custom Trigger Bundle quick-command lists are each populated from that file's corresponding section, in one action

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
The system SHALL let the Owner clear every import-populated entry across every category - Title, Outfit, Gesture, Moodles, Restraints, and Custom Trigger Bundles - back to empty in a single action, distinct from importing a file and distinct from each category's individual "Clear all" control. Because single-action aliases now share each category's own quick-command list with scanned entries and entries the Owner added manually in that category, resetting imports SHALL remove only the entries that came from an import, leaving that category's scanned and manually-added entries untouched. Custom Trigger Bundles SHALL continue to share one list between imported entries and any the Owner typed manually into the freeform bundle/one-off control, so resetting SHALL clear that entire list, the same coarse whole-list reset already accepted for Restraints' manually-added entries.

#### Scenario: Owner resets all imports
- **WHEN** the Owner triggers the reset-imports action
- **THEN** import-sourced entries in Title, Outfit, Gesture, Moodles, Restraints, and Custom Trigger Bundles are all removed, and categories not populated by import (Follow) are left unchanged

#### Scenario: Reset does not remove scanned or manually-added entries sharing a category list
- **WHEN** a category's quick-command list contains both import-sourced entries and entries the Owner scanned or manually added
- **THEN** triggering reset-imports removes only the import-sourced entries in that category, leaving the scanned or manually-added entries in place

#### Scenario: Reset control is placed next to import
- **WHEN** the Owner views the import controls
- **THEN** the reset-imports control is visible alongside the "Import commands" control

#### Scenario: Resetting clears manually-typed one-off aliases too
- **WHEN** the Owner has both imported multi-action bundles and manually typed one-off commands into the same Custom Trigger Bundle list, and triggers reset-imports
- **THEN** the entire list is emptied, the same as Restraints' existing manually-added entries are already cleared by reset-imports today

### Requirement: Owner imports alias names as one-off quick commands
The system SHALL let the Owner import a Sub-exported file's Title, Outfit, Gesture, Restraint, or Moodle section entries - covering both that category's own alias definitions and any Custom Trigger that bundles exactly one action of that category - into that same category's quick-command list, adding one entry per alias with the raw alias word as its command - exactly as if the Owner had added that entry through that category's own individual import or freeform control themselves - and its label showing the alias word together with the human-readable description exported alongside it, so the Owner can see what the entry does without that description affecting what is actually sent. An alias already present in that category's list (matched by its command, the bare alias word - whether previously imported or manually typed by the Owner) SHALL NOT be duplicated. A Custom Trigger that bundles two or more actions SHALL NOT be imported into any single category's list; it imports only into the Custom Trigger Bundle list, the same one-off list the Owner's freeform control already populates manually.

#### Scenario: Importing alias names populates the one-off list
- **WHEN** the Owner imports a file whose Title, Outfit, Gesture, Restraint, or Moodle section lists one or more single-action alias entries
- **THEN** each is added to that category's own quick-command list as a ready-to-send entry whose command is that exact alias word

#### Scenario: Re-importing the same aliases does not duplicate entries
- **WHEN** the Owner imports a file whose category section contains an alias word already present (by command) in that category's list
- **THEN** no duplicate entry is added for that word

#### Scenario: Imported alias entries are labeled with what they do
- **WHEN** the Owner imports a file whose category sections contain alias entries with descriptions
- **THEN** each is added to that category's quick-command list labeled with its alias word and description, while the command actually sent (Send/Copy) remains only the bare alias word, unaffected by the label

#### Scenario: A multi-action bundle never lands in a single category's list
- **WHEN** the Owner imports a file whose Custom Trigger Bundle section contains a trigger bundling two or more actions
- **THEN** that entry is added only to the Custom Trigger Bundle list, never to any of Title/Outfit/Gesture/Restraint/Moodle
