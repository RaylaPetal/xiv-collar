## MODIFIED Requirements

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
