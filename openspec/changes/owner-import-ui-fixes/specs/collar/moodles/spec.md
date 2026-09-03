## MODIFIED Requirements

### Requirement: Local preset catalog from the Sub's own Moodles data
The system SHALL build its Moodles catalog by reading every individual status (buff/debuff) registered in the Sub's locally installed Moodles plugin through Moodles' supported local status-list interface, without passing a character target and without reading collar-owned or bundled preset data. The scan UI SHALL distinguish an unavailable or failed Moodles integration from a successful scan containing zero statuses, and its name export SHALL contain the names from the latest successful local scan.

#### Scenario: Sub's saved presets become available
- **WHEN** the Sub has one or more statuses registered in their own Moodles plugin and triggers a rescan
- **THEN** the catalog lists every returned status by its Moodles identifier and display name, available to reference by name

#### Scenario: Sub has no saved presets
- **WHEN** the local Moodles integration responds successfully with no registered statuses
- **THEN** the scan reports a successful zero-status result and the local catalog is empty

#### Scenario: Moodle integration fails
- **WHEN** the local Moodles integration is unavailable or its status-list call fails
- **THEN** the scan reports the failure visibly and does not present it as a successful zero-status result

#### Scenario: Sub copies preset names
- **WHEN** a successful scan contains one or more registered statuses and the Sub invokes "Copy names"
- **THEN** the copied text contains each scanned status display name once and contains no preset or collar-owned names

### Requirement: Owner applies or clears a Moodle by name
The system SHALL let a paired Owner with the Sub's "Moodles" permission enabled apply an individual status by its name, matched against the Sub's own local catalog, or clear the Sub's active Moodles status. A Moodle command SHALL apply immediately, without a Sub-confirmation step.

#### Scenario: Owner applies a known preset
- **WHEN** an Owner sends an apply command naming a status that matches the Sub's local catalog
- **THEN** the Sub's client applies that individual status to the Sub's own character immediately

#### Scenario: Owner names an unrecognized preset
- **WHEN** an Owner sends an apply command naming a status that does not match anything in the Sub's local catalog
- **THEN** the Sub's client takes no action

#### Scenario: Owner clears the Sub's Moodles
- **WHEN** an Owner sends a clear command to a Sub with an active Moodles status
- **THEN** the Sub's client removes the applied status
