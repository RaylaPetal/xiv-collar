## MODIFIED Requirements

### Requirement: Sub can scope which mods are scanned
The system SHALL let a Sub select zero or more Penumbra sort folders from a searchable multi-select
dropdown and optionally select individual installed mods within that folder scope for gesture scanning.
When folders are selected and no individual mods are selected, every mod under any selected folder SHALL
participate. When individual mods are selected, only those selected mods SHALL participate. When neither
folders nor mods are selected, every installed mod SHALL participate for backward compatibility. Folder
and mod selections SHALL persist independently, and text search SHALL only filter the visible choices.

#### Scenario: Sub selects multiple animation folders
- **WHEN** a Sub selects two Penumbra sort folders and leaves the individual-mod selection empty
- **THEN** gesture scanning includes every installed mod below either folder and excludes mods outside both folders

#### Scenario: Sub scopes to an allowlisted folder
- **WHEN** a Sub selects one or more Penumbra sort folders and leaves individual mods unselected
- **THEN** every mod under the selected folder union participates while mods outside it do not

#### Scenario: Sub narrows selected folders to individual mods
- **WHEN** one or more animation folders are selected and the Sub explicitly selects individual mods shown within them
- **THEN** only those individual mods participate in the scan

#### Scenario: Sub selects mods to scan
- **WHEN** a Sub explicitly selects one or more visible Penumbra mods and triggers a rescan
- **THEN** the generated catalog contains animation metadata only from those selected mods

#### Scenario: Selected mod is disabled
- **WHEN** a selected installed mod is currently disabled in the Sub's effective Penumbra collection
- **THEN** its animations remain discoverable and a later command can enable the chosen configuration temporarily

#### Scenario: No folders or mods are selected
- **WHEN** a Sub triggers a scan with both selection sets empty
- **THEN** the generated gesture catalog contains animation metadata from every installed Penumbra mod

#### Scenario: No mods are selected
- **WHEN** no individual mods are selected but one or more folders are selected
- **THEN** all installed mods in the selected folder union participate in gesture scanning

#### Scenario: Search is changed or cleared
- **WHEN** the Sub changes the folder or mod search text
- **THEN** the visible dropdown choices change without mutating either persisted selection set

#### Scenario: Visual filters are empty
- **WHEN** folder and mod search text are empty
- **THEN** the dropdowns show every available folder and every mod in the current folder scope without mutating selections

#### Scenario: Legacy single folder filter is migrated
- **WHEN** an existing configuration contains the previous single animation folder filter
- **THEN** that folder becomes one entry in the new folder selection without losing explicit mod selections
