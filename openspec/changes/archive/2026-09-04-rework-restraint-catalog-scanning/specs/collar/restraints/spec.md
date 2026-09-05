## ADDED Requirements

### Requirement: Sub shares a folder-scoped Penumbra restraint catalog
The system SHALL let the Sub select zero or more Penumbra sort folders as the restraint scan scope and
SHALL scan every mod under those folder prefixes into a structured restraint catalog with exactly one
entry per mod. An empty restraint folder selection SHALL expose no Penumbra restraint mods. Each entry
SHALL preserve a stable mod identity, display name, saved enabled state, and saved selections locally.
Option groups and toggles SHALL NOT be exported as separate restraint choices.

#### Scenario: Sub scans selected restraint folders
- **WHEN** the Sub selects two Penumbra restraint folders and runs the restraint scan
- **THEN** the catalog contains one entry for every mod below either selected folder and excludes every mod outside both folders

#### Scenario: No restraint folder is selected
- **WHEN** the Sub runs the restraint scan with no restraint folders selected
- **THEN** no installed Penumbra mod is exposed as remotely commandable restraint content

#### Scenario: Mod contains many toggles
- **WHEN** a selected restraint mod contains hundreds of option groups or toggles
- **THEN** the Owner sees one mod entry and cannot remotely alter any of those selections

### Requirement: Owner controls imported restraint mods
The system SHALL let a paired Owner browse and search the structured restraint catalog shared by the Sub,
select any restraint mod, attach the equipment item changed by that mod and one or more restriction rules, save or favorite the resulting command,
and force-apply it without prior knowledge of the Sub's mod folders or option names. The sent command SHALL
identify the shared entry unambiguously and SHALL NOT permit the Owner to address a mod absent from the
Sub's currently shared restraint catalog.

#### Scenario: Owner creates a restraint from the shared catalog
- **WHEN** the Owner selects a shared restraint mod, assigns Arms Cuffed and Gagged rules, and sends it
- **THEN** the Sub temporarily enables that mod without changing its saved toggles, equips and locks the chosen item, and activates both rules

#### Scenario: Sub creates and shares a mod restraint
- **WHEN** the Sub chooses a scanned restraint mod, assigns rules, and synchronizes the catalog
- **THEN** the Owner imports that configured restraint as a ready-made command in addition to browsing every raw shared mod

#### Scenario: Owner selects an unshared or stale identity
- **WHEN** an Owner command refers to a restraint identity that is missing from the Sub's current shared catalog
- **THEN** the Sub rejects the command without changing Penumbra, equipment, locks, or restriction state

#### Scenario: Owner releases all restraints
- **WHEN** the Owner sends the global restraint unlock command
- **THEN** every active restraint's equipment lock, temporary mod enable, and rule leases are released and saved Penumbra state is restored

#### Scenario: Owner lacks Restraints permission
- **WHEN** a valid catalog-backed restraint command arrives while the Sub's Restraints permission is disabled
- **THEN** the Sub rejects it without applying the mod option or any attached rule

### Requirement: Catalog-backed restraint activation is reversible
Applying a catalog-backed restraint SHALL temporarily enable its Penumbra mod using its existing saved selections
for the Sub's effective collection, redraw the Sub, and then activate its rules. Unlock, unpair cleanup,
panic, replacement, or failed atomic application SHALL revert every temporary setting owned by that
restraint and redraw the Sub. The plugin SHALL NOT overwrite the mod's saved Penumbra configuration.

#### Scenario: Catalog-backed restraint applies successfully
- **WHEN** the Sub accepts a valid permitted catalog-backed restraint command
- **THEN** the exact shared mod option becomes active temporarily, the Sub is redrawn, and the attached rules engage

#### Scenario: Unlock restores saved Penumbra state
- **WHEN** the Owner unlocks an active catalog-backed restraint
- **THEN** its temporary Penumbra settings and rules are released and the Sub is redrawn using saved settings

#### Scenario: Mod activation fails
- **WHEN** temporary Penumbra activation or redraw fails while applying a catalog-backed restraint
- **THEN** no partial restraint or restriction state remains active and the failure is reported

### Requirement: Direct slot-item restraints remain available
The system SHALL keep direct Owner-authored slot/item restraint controls as an advanced flow below the
detected and configured Penumbra mod restraints. Legacy name-based restraint creation and catalog import
SHALL be removed, while old serialized records SHALL remain non-fatal during upgrade.

#### Scenario: Existing configuration upgrades
- **WHEN** a configuration containing captured slot/item devices and saved restraint commands is loaded after the upgrade
- **THEN** loading succeeds without converting those records into catalog-backed mod restraints
