## MODIFIED Requirements

### Requirement: Sub can scope which mods are scanned
The system SHALL let a Sub explicitly select which installed Penumbra mods participate in gesture scanning. An empty selected-mod set SHALL include every installed mod, while one or more explicit selections SHALL restrict scanning to those mods. The selection UI SHALL show mod display names and MAY be narrowed visually by Penumbra sort folder or text search; changing or clearing a visual filter SHALL NOT itself change the persisted selected set.

#### Scenario: Sub scopes to an allowlisted folder
- **WHEN** a Sub uses a Penumbra sort-folder filter and explicitly selects mods from the filtered results
- **THEN** only those explicitly selected mods participate in scanning after at least one selection exists, while the folder filter itself neither selects nor excludes additional mods

#### Scenario: Sub selects mods to scan
- **WHEN** a Sub explicitly selects one or more installed Penumbra mods and triggers a rescan
- **THEN** the generated catalog contains animation metadata only from those selected mods

#### Scenario: Selected mod is disabled
- **WHEN** a selected installed mod is currently disabled in the Sub's effective Penumbra collection
- **THEN** its animations remain discoverable and a later command can enable the chosen configuration temporarily

#### Scenario: No mods are selected
- **WHEN** a Sub triggers a scan with an empty selected-mod set
- **THEN** the generated catalog contains animation metadata from every installed Penumbra mod

#### Scenario: Visual filters are empty
- **WHEN** the folder and text filters are empty
- **THEN** the selection UI shows every installed mod without mutating the selected-mod set
