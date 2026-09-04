# collar/catalog-sync Specification

## Purpose

Lets a Sub scan every commandable catalog (Wardrobe, Gesture, Moodles, Restraints) in one action and hand the whole result to their Owner as a single file, and lets the Owner turn that one file back into every category's quick-command list in one action, instead of running and sharing each category separately.

## Requirements

### Requirement: Scanning the scannable catalogs together
The system SHALL let the Sub trigger a scan of Wardrobe, Gesture, and Moodles catalogs from a single action. Each category's own scan scope (Wardrobe's folder allowlist, Gesture's selected-mod set) SHALL remain independently configurable and SHALL apply exactly as it does for that category's individual scan. Restraints has no scan step and SHALL NOT be part of this unified scan action - a restraint device is captured individually (see `collar/restraints`), independent of scanning.

#### Scenario: One action scans every scannable category
- **WHEN** the Sub triggers the unified scan action
- **THEN** Wardrobe, Gesture, and Moodles are each rescanned using their own currently-configured scope, and the results are exactly what triggering each category's own scan individually would have produced

#### Scenario: A per-category scope still restricts that category's results
- **WHEN** the Sub has configured a Wardrobe folder allowlist or a Gesture mod selection before running the unified scan
- **THEN** the scanned results for that category are restricted to the configured scope, the same as scanning that category alone

#### Scenario: Restraints is not part of the unified scan
- **WHEN** the Sub triggers the unified scan action
- **THEN** no Restraints scan runs as part of it, and any already-captured restraint devices are left exactly as they were

### Requirement: Exporting every catalog to one file
The system SHALL let the Sub export the current Wardrobe, Gesture, Moodles, Restraints, and Aliases catalogs together as a single text file written to a location the Sub chooses, in a format that preserves each category's own identity guarantees (the same information that category's individual export already provides). A category with an empty catalog SHALL be included in the export as empty, not omitted in a way that would prevent re-import from recognizing that category. The Aliases section SHALL contain one entry per deduplicated alias word defined in the Sub's Title, Outfit, Gesture, Restraint, Moodles, and Custom Trigger alias lists, each carrying a human-readable summary of what that alias does alongside the bare word - this is a deliberate exception scoped to this out-of-band export file, made at the user's explicit request; the live wire tell used during real commanding still carries only the bare alias word, resolved entirely on the Sub's own client (see `collar/chat-transport`). Each exported Gesture entry SHALL carry only the fields the Owner's import needs to identify and group it (its id, mod name, animation name, group name, group order, option order, and its playable trigger) - it SHALL NOT carry fields meaningful only to the Sub's own local playback (such as per-entry mod-option selection state), so the exported file's size scales with the number of catalog entries, not with how many configurable option groups their source mods happen to have.

#### Scenario: Export produces one file covering every category
- **WHEN** the Sub triggers the export action after scanning
- **THEN** a single file is written containing the current Wardrobe, Gesture, Moodles, Restraints, and Aliases catalogs, each identifiable as belonging to its own category

#### Scenario: An empty category is still represented
- **WHEN** one of the five categories has no scanned, tagged, or defined entries at export time
- **THEN** the exported file still identifies that category with zero entries, rather than omitting it entirely

#### Scenario: Aliases export contains only names
- **WHEN** the Sub has defined aliases in the Title, Outfit, Gesture, Restraint, Moodles, or Custom Trigger sections
- **THEN** the Aliases section of the exported file lists each alias word once, deduplicated

#### Scenario: Aliases export reveals what each alias does
- **WHEN** the Sub has defined aliases in the Title, Outfit, Gesture, Restraint, Moodles, or Custom Trigger sections
- **THEN** each alias word in the exported file's Aliases section is paired with a human-readable summary of what that alias applies (e.g. its title text and prefix/suffix, the design/animation/status/device name it references, or - for a Custom Trigger - a summary of every bundled action)

#### Scenario: Gesture export omits local-only playback data
- **WHEN** the Sub exports a Gesture catalog whose source mods have multiple configurable option groups
- **THEN** each exported entry's size reflects only its own identifying fields, not the full set of every other option group's current selection state

#### Scenario: Moodles and Custom Trigger alias words are included in the export
- **WHEN** the Sub has defined one or more Moodles aliases or Custom Trigger aliases
- **THEN** each of those aliases also appears in the exported file's Aliases section, deduplicated alongside every other category's aliases, each with its own description

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

### Requirement: Owner imports alias names as one-off quick commands
The system SHALL let the Owner import a Sub-exported file's Aliases section into the same quick-command list the Owner's freeform "Alias / one-off" control populates, adding one entry per alias with the raw alias word as its command - exactly as if the Owner had typed that word into the freeform control themselves - and its label showing the alias word together with the human-readable description exported alongside it, so the Owner can see what the entry does without that description affecting what is actually sent. An alias already present in that list (matched by its command, the bare alias word - whether previously imported or manually typed by the Owner) SHALL NOT be duplicated.

#### Scenario: Importing alias names populates the one-off list
- **WHEN** the Owner imports a file whose Aliases section lists one or more alias entries
- **THEN** each is added to the Owner's "Alias/one-off" quick-command list as a ready-to-send entry whose command is that exact alias word

#### Scenario: Re-importing the same aliases does not duplicate entries
- **WHEN** the Owner imports a file whose Aliases section contains an alias word already present (by command) in the one-off list
- **THEN** no duplicate entry is added for that word

#### Scenario: Imported alias entries are labeled with what they do
- **WHEN** the Owner imports a file whose Aliases section contains alias entries with descriptions
- **THEN** each is added to the Owner's "Alias/one-off" quick-command list labeled with its alias word and description, while the command actually sent (Send/Copy) remains only the bare alias word, unaffected by the label
