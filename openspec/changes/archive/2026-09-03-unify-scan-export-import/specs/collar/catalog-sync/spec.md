## Purpose

Lets a Sub scan every commandable catalog (Wardrobe, Gesture, Moodles, Restraints) in one action and hand the whole result to their Owner as a single file, and lets the Owner turn that one file back into every category's quick-command list in one action, instead of running and sharing each category separately.

## ADDED Requirements

### Requirement: Scanning every catalog together
The system SHALL let the Sub trigger a scan of Wardrobe, Gesture, Moodles, and Restraints catalogs from a single action. Each category's own scan scope (Wardrobe's folder allowlist, Gesture's selected-mod set, Restraints' own folder allowlist) SHALL remain independently configurable and SHALL apply exactly as it does for that category's individual scan. Restraints' scan scope SHALL be independent of Wardrobe's - a folder allowlisted for one SHALL NOT need to be allowlisted for the other.

#### Scenario: One action scans every scannable category
- **WHEN** the Sub triggers the unified scan action
- **THEN** Wardrobe, Gesture, Moodles, and Restraints are each rescanned using their own currently-configured scope, and the results are exactly what triggering each category's own scan individually would have produced

#### Scenario: A per-category scope still restricts that category's results
- **WHEN** the Sub has configured a Wardrobe folder allowlist, a Restraints folder allowlist, or a Gesture mod selection before running the unified scan
- **THEN** the scanned results for that category are restricted to the configured scope, the same as scanning that category alone

#### Scenario: Wardrobe and Restraints scopes don't interfere
- **WHEN** the Sub has configured different folder allowlists for Wardrobe and Restraints
- **THEN** the unified scan action still produces the same independent per-category results as scanning each one alone

### Requirement: Exporting every catalog to one file
The system SHALL let the Sub export the current Wardrobe, Gesture, Moodles, and Restraints catalogs together as a single text file written to a location the Sub chooses, in a format that preserves each category's own identity guarantees (the same information that category's individual export already provides). A category with an empty catalog SHALL be included in the export as empty, not omitted in a way that would prevent re-import from recognizing that category.

#### Scenario: Export produces one file covering every category
- **WHEN** the Sub triggers the export action after scanning
- **THEN** a single file is written containing the current Wardrobe, Gesture, Moodles, and Restraints catalogs, each identifiable as belonging to its own category

#### Scenario: An empty category is still represented
- **WHEN** one of the four categories has no scanned or tagged entries at export time
- **THEN** the exported file still identifies that category with zero entries, rather than omitting it entirely

### Requirement: Importing one file fills every category's quick commands
The system SHALL let the Owner import a previously exported file in a single action, and SHALL populate each category's quick-command list from the corresponding section of that file, using the same per-category matching/deduplication behavior each category's own individual import already provides. An entry already present in a category's quick-command list SHALL NOT be duplicated by importing a file that contains it again.

#### Scenario: Importing a file fills every category at once
- **WHEN** the Owner imports a file previously exported by a paired Sub
- **THEN** the Owner's Wardrobe, Gesture, Moodles, and Restraints quick-command lists are each populated from that file's corresponding section, in one action

#### Scenario: Re-importing the same file does not duplicate entries
- **WHEN** the Owner imports a file whose entries for a category are already present in that category's quick-command list
- **THEN** no duplicate entries are added for that category

#### Scenario: Importing a file with an empty category section changes nothing for that category
- **WHEN** an imported file identifies a category with zero entries
- **THEN** that category's existing quick-command list is left unchanged
