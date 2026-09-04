## MODIFIED Requirements

### Requirement: Exporting every catalog to one file
The system SHALL let the Sub export the current Title, Wardrobe, Gesture, Moodles, Restraints, and Custom Trigger Bundle catalogs together as a single text file written to a location the Sub chooses, in a format that preserves each category's own identity guarantees (the same information that category's individual export already provides). A category with an empty catalog SHALL be included in the export as empty, not omitted in a way that would prevent re-import from recognizing that category. Each single-action alias defined in the Sub's Title, Outfit, Gesture, Restraint, or Moodle alias lists, and each Custom Trigger that bundles exactly one action, SHALL be exported under that action's own category section (Title/Outfit/Gesture/Restraint/Moodle), carrying the same human-readable summary of what it does that the export already provides for that category's other entries. For an Outfit, Gesture, or Moodle single-action alias, the exported entry SHALL additionally carry that action's target identity (the design id, gesture id, or Moodles status id it applies) alongside its human-readable summary, so import-time duplicate detection can match on identity rather than parsing display text. The Custom Trigger Bundle section SHALL contain only Custom Triggers that bundle two or more actions, each carrying a human-readable summary of every action in the bundle. Each exported Gesture entry SHALL carry only the fields the Owner's import needs to identify and group it (its id, mod name, animation name, group name, group order, option order, and its playable trigger) - it SHALL NOT carry fields meaningful only to the Sub's own local playback (such as per-entry mod-option selection state), so the exported file's size scales with the number of catalog entries, not with how many configurable option groups their source mods happen to have.

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

#### Scenario: An Outfit, Gesture, or Moodle alias carries its target identity
- **WHEN** the Sub has defined a single-action Outfit, Gesture, or Moodle alias
- **THEN** its exported entry carries the design id, gesture id, or Moodles status id that alias applies, alongside its human-readable summary

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
The system SHALL let the Owner import a previously exported file in a single action, and SHALL populate each category's quick-command list - Title, Outfit, Gesture, Moodles, Restraints, and Custom Trigger Bundles - from the corresponding section of that file, using the same per-category matching/deduplication behavior each category's own individual import already provides. An entry already present in a category's quick-command list SHALL NOT be duplicated by importing a file that contains it again, and an entry SHALL also be skipped where "Import skips commands that duplicate an existing quick command" applies. Restraints entries SHALL import from the file's raw scanned design names (tagged or not) and SHALL be added to the Owner's quick-command list without restriction rules pre-assigned; the Owner assigns rules per entry after import (see `collar/restraints`). Single-action aliases and single-action Custom Triggers SHALL import as described in "Owner imports single-action aliases into their matching category."

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

### Requirement: Owner imports alias names as one-off quick commands
The system SHALL let the Owner import a Sub-exported file's Title, Outfit, Gesture, Restraint, or Moodle section entries - covering both that category's own alias definitions and any Custom Trigger that bundles exactly one action of that category - into that same category's quick-command list, adding one entry per alias with the raw alias word as its command - exactly as if the Owner had added that entry through that category's own individual import or freeform control themselves - and its label showing the alias word together with the human-readable description exported alongside it, so the Owner can see what the entry does without that description affecting what is actually sent. An alias already present in that category's list (matched by its command, the bare alias word - whether previously imported or manually typed by the Owner) SHALL NOT be duplicated, and it SHALL also be skipped where "Import skips commands that duplicate an existing quick command" applies. A Custom Trigger that bundles two or more actions SHALL NOT be imported into any single category's list; it imports only into the Custom Trigger Bundle list, the same one-off list the Owner's freeform control already populates manually.

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

## ADDED Requirements

### Requirement: Import skips commands that duplicate an existing quick command
The system SHALL skip adding an Outfit, Gesture, or Moodle entry (whether a plain scanned name or a single-action alias) during import if the Owner's existing quick-command list for that category already contains an entry whose target identity (design id, gesture id, or Moodles status id) matches, regardless of whether the existing entry's command is the alias word or the plain-name override form - two entries that apply the identical design/animation/status are a duplicate even when their command text differs. Independently of category, the system SHALL skip adding any entry during import if its exact command text already exists anywhere else among the Owner's quick commands, not only within its own category's list, since two entries sharing one command always send byte-identical text over the wire. This requirement SHALL NOT apply to Title (free text with no shared target identity), Restraints (devices are captured and named individually by the Sub, not scan-derived), or Custom Trigger Bundles (a bundle's actions are expected to be able to overlap with other entries). Every import SHALL report how many entries were skipped as duplicates, in addition to the existing per-category added counts.

#### Scenario: An alias and a plain scanned entry target the same design
- **WHEN** the Owner imports a file whose Outfit section lists both a single-action alias and a plain scanned name that both target the same Glamourer design
- **THEN** only one Outfit quick command for that design is added, and the other is skipped as a duplicate

#### Scenario: A same-target duplicate is skipped regardless of import order
- **WHEN** the Owner's Outfit, Gesture, or Moodle quick-command list already contains an entry targeting a given design/animation/status, and the Owner imports a file with a different-looking entry (different alias word or plain name) targeting that same design/animation/status
- **THEN** the new entry is skipped, and the existing entry is left unchanged

#### Scenario: The same command text is rejected across categories
- **WHEN** the Owner's Title quick-command list already has a command whose text exactly matches an entry the Owner is importing into a different category
- **THEN** the imported entry is skipped, since sending either one would send identical text

#### Scenario: Title, Restraints, and Custom Trigger Bundle entries are not subject to same-target matching
- **WHEN** the Owner imports Title, Restraints, or Custom Trigger Bundle entries
- **THEN** none of them are skipped on the basis of sharing a target with another entry - only the cross-category same-command-text check (and each category's own existing exact-duplicate check) applies

#### Scenario: Import summary reports duplicates skipped
- **WHEN** an import skips one or more entries as duplicates
- **THEN** the import result summary includes how many were skipped as duplicates, alongside the per-category added counts
