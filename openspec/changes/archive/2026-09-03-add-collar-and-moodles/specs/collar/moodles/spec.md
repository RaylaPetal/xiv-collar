## Purpose

Lets a paired Owner apply or clear a status-effect ("Moodle") on a Sub, chosen from the Sub's own locally-saved Moodles presets, without the Owner ever needing access to the Sub's Moodles configuration directly.

## ADDED Requirements

### Requirement: Local preset catalog from the Sub's own Moodles data
The system SHALL build its Moodles preset catalog by reading the Sub's own saved presets from the locally-installed Moodles plugin, without requiring the Sub to redefine them inside this plugin.

#### Scenario: Sub's saved presets become available
- **WHEN** the Sub has one or more presets saved in their own Moodles plugin and triggers a rescan
- **THEN** the catalog lists each preset's name, available to reference by name

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
