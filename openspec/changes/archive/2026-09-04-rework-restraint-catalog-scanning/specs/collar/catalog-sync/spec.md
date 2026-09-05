## MODIFIED Requirements

### Requirement: Scanning the scannable catalogs together
The system SHALL let the Sub trigger a scan of Wardrobe, Gesture, Moodles, and Penumbra Restraints catalogs
from a single action. Each category's own scan scope SHALL remain independently configurable and SHALL
apply exactly as it does for that category's individual scan. Existing manually captured restraint devices
SHALL remain unchanged by scanning.

#### Scenario: One action scans every scannable category
- **WHEN** the Sub triggers the unified scan action
- **THEN** Wardrobe, Gesture, Moodles, and Penumbra Restraints are each rescanned using their own configured scope

#### Scenario: A per-category scope restricts only that category
- **WHEN** the Sub configures different Wardrobe, Gesture, and Restraint folder scopes before the unified scan
- **THEN** each result is restricted by its own scope without changing either of the other scopes

#### Scenario: A per-category scope still restricts that category's results
- **WHEN** the Sub configures a category's folder or mod scope before running the unified scan
- **THEN** only that category's results are restricted by its scope, exactly as in its individual scan

#### Scenario: Scan preserves manually captured devices
- **WHEN** the Sub rescans Penumbra restraints while captured slot/item restraint devices already exist
- **THEN** the captured devices remain unchanged and separate from the refreshed scanned catalog

#### Scenario: Restraints is not part of the unified scan
- **WHEN** a legacy configuration has no selected restraint folders and the Sub triggers the unified scan
- **THEN** the Penumbra restraint result stays empty and manually captured restraint devices remain unchanged

### Requirement: Importing one file fills every category's quick commands
The system SHALL let the Owner import a complete catalog file in one atomic action and SHALL populate each
category from its corresponding section without duplicating existing stable identities. The Restraints
section SHALL carry one structured entry per Penumbra mod and SHALL NOT export legacy name-only restraint entries.
Importing these entries SHALL populate the Owner's browseable restraint catalog without automatically
creating quick commands. Separately encoded Sub-configured restraints SHALL import as ready-made commands
with their chosen rules. A malformed entry SHALL reject the complete relay import atomically.

#### Scenario: Import includes structured restraints
- **WHEN** the Owner imports a valid Sub export containing Penumbra restraint entries
- **THEN** those mods appear in the searchable Owner restraint browser and no quick command is created until the Owner chooses one

#### Scenario: Import includes Sub-configured restraints
- **WHEN** the Sub export contains a configured restraint referencing a shared mod
- **THEN** the Owner receives it as a ready-made command with its name and rules while the raw mod remains available for other Owner-authored restraints

#### Scenario: Importing a file fills every category at once
- **WHEN** the Owner imports a valid complete catalog file
- **THEN** Title, Outfit, Gesture, Moodles, Restraints, and Custom Trigger Bundle data are staged and committed together

#### Scenario: Re-importing the same file does not duplicate entries
- **WHEN** the Owner imports a file whose stable identities and commands are already present
- **THEN** no duplicate entries are added in any category

#### Scenario: Importing a file with an empty category section changes nothing for that category
- **WHEN** a manual import identifies a category with zero entries
- **THEN** that category's existing quick-command list is left unchanged

#### Scenario: Importing a file with scanned but untagged restraints
- **WHEN** the Owner imports a legacy file containing plain restraint names
- **THEN** those names are ignored and no legacy restraint command is created

#### Scenario: Re-import preserves Owner presentation state
- **WHEN** a newer snapshot contains the same stable restraint identity as a favorited saved command
- **THEN** the catalog metadata updates without losing the Owner's favorite or attached rule choices

#### Scenario: Malformed restraint data is atomic
- **WHEN** a file or relay snapshot contains a malformed structured restraint entry
- **THEN** no category or previously imported restraint catalog is changed

### Requirement: Export includes structured restraint identities
The unified manual and relay catalog snapshot SHALL include each folder-scoped Penumbra restraint entry's
stable identity and human-readable mod name while excluding Sub-local filesystem paths, option selections, and
unselected mods. Legacy captured-device names SHALL NOT be included in new exports.

#### Scenario: Sub exports a restraint catalog
- **WHEN** the Sub exports after scanning selected restraint folders
- **THEN** the export contains only entries in the selected scope and enough metadata for the Owner to choose them unambiguously

#### Scenario: Relay and manual export agree
- **WHEN** the same current catalogs are exported manually and supplied to encrypted relay sync
- **THEN** both paths describe the same restraint identities and neither exposes local filesystem paths
