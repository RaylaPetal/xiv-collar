# collar/moodles Specification

## Purpose

Lets a paired Owner apply or clear a status-effect ("Moodle") on a Sub, chosen from the Sub's own locally-saved Moodles presets, without the Owner ever needing access to the Sub's Moodles configuration directly.

## Requirements

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

### Requirement: Moodles permission gates apply and clear
The system SHALL NOT apply or clear any Moodle on a Sub's character unless the Sub has separately enabled a "Moodles" permission, independent of Title/Outfit/Gesture/Follow/Collar.

#### Scenario: Moodle command without permission
- **WHEN** an Owner sends a Moodles apply or clear command to a Sub who has not enabled the "Moodles" permission
- **THEN** the Sub's client rejects the command and no status effect changes

### Requirement: Owner applies or clears a Moodle by name
The system SHALL let a paired Owner with the Sub's "Moodles" permission enabled apply a preset by its name, matched against the Sub's own local catalog, or clear the Sub's active Moodles status. A Moodle command SHALL apply immediately, without a Sub-confirmation step.

#### Scenario: Owner applies a known preset
- **WHEN** an Owner sends an apply command naming a preset that matches the Sub's local catalog
- **THEN** the Sub's client applies that preset to the Sub's own character immediately

#### Scenario: Owner names an unrecognized preset
- **WHEN** an Owner sends an apply command naming a preset that does not match anything in the Sub's local catalog
- **THEN** the Sub's client takes no action

#### Scenario: Owner clears the Sub's Moodles
- **WHEN** an Owner sends a clear command to a Sub with an active Moodles status
- **THEN** the Sub's client removes the applied status
