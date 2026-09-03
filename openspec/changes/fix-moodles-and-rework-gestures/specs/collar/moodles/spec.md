## MODIFIED Requirements

### Requirement: Local preset catalog from the Sub's own Moodles data
The system SHALL build its Moodles preset catalog by reading every preset saved in the Sub's locally installed Moodles plugin through Moodles' supported local preset-list interface, without passing a character target and without reading collar-owned or bundled preset data. The scan UI SHALL distinguish an unavailable or failed Moodles integration from a successful scan containing zero presets, and its name export SHALL contain the names from the latest successful local scan.

#### Scenario: Sub's saved presets become available
- **WHEN** the Sub has one or more presets saved in their own Moodles plugin and triggers a rescan
- **THEN** the catalog lists every returned preset by its Moodles identifier and display name, available to reference by name

#### Scenario: Sub has no saved presets
- **WHEN** the local Moodles integration responds successfully with no saved presets
- **THEN** the scan reports a successful zero-preset result and the local catalog is empty

#### Scenario: Moodle integration fails
- **WHEN** the local Moodles integration is unavailable or its preset-list call fails
- **THEN** the scan reports the failure visibly and does not present it as a successful zero-preset result

#### Scenario: Sub copies preset names
- **WHEN** a successful scan contains one or more saved presets and the Sub invokes “Copy names”
- **THEN** the copied text contains each scanned Moodles preset display name once and contains no collar-owned preset names
