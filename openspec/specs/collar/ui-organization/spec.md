# collar/ui-organization Specification

## Purpose

Keeps Sub configuration and Owner command controls visually distinct, compact, and discoverable as the collar system gains more modules and settings.

## Requirements

### Requirement: Owner command categories are independently collapsible
The Owner command surface SHALL present each supported command category in its own labeled collapsible section, preserving the category's existing add, import, compose, copy, and send operations. Expanding or collapsing one category SHALL NOT change the saved commands or the expanded state of other categories during the same window session.

#### Scenario: Owner opens command surface
- **WHEN** the Owner command surface contains title, outfit, gesture, leash, Moodles, and general alias controls
- **THEN** each category is identifiable and can be expanded or collapsed independently

#### Scenario: Owner collapses a category
- **WHEN** the user collapses a category containing saved commands
- **THEN** its detailed controls and rows are hidden without deleting or changing any saved command

### Requirement: Owner navigation is separated from Sub modules
The main module navigation SHALL place the Owner command entry at the far-right edge, with visible separation from the cluster of Sub-facing configuration modules, while retaining keyboard/mouse accessibility and the current selected-module behavior.

#### Scenario: Main navigation is rendered
- **WHEN** the collar window draws its module navigation
- **THEN** Sub-facing modules remain grouped together and the Owner entry appears at the far right as a distinct destination

#### Scenario: Narrow navigation width
- **WHEN** the main window is at its supported minimum width
- **THEN** the separated Owner entry remains visible, selectable, and non-overlapping

### Requirement: Safeword has one canonical configuration surface
The main character header SHALL be the sole visible safeword configuration surface. Settings SHALL continue to explain how `/collarpanic` works when relevant but SHALL NOT display a second safeword input.

#### Scenario: User opens Settings
- **WHEN** the safeword editor is available in the main character header
- **THEN** Settings does not render a duplicate safeword input or a conflicting editable value

#### Scenario: User needs to configure safety
- **WHEN** the user views the main character header in any pairing state
- **THEN** the existing safeword editor remains available there

### Requirement: Every Sub action can be tested locally before pairing
The Sub-facing interface SHALL provide an explicit local Test control for every configurable action: title apply and clear, outfit apply and unlock, gesture playback, collar lock and unlock, Moodles apply and clear, and leash and unleash. A local test SHALL execute through the same local action path used for an accepted Owner command, SHALL NOT require an active or pending pairing, and SHALL NOT compose or transmit a chat message. The action's normal category permission SHALL remain required, and gesture or leash testing SHALL additionally require the existing automation-risk acknowledgement. Every test SHALL report a visible success or failure result.

#### Scenario: Unpaired user tests a permitted action
- **WHEN** an unpaired user invokes Test for a configured action whose category permission and any required acknowledgement are enabled
- **THEN** the action executes locally through its normal action path and reports its result without creating pairing state or sending chat

#### Scenario: User tests without category permission
- **WHEN** the user invokes Test for an action whose category permission is disabled
- **THEN** the action does not execute and the interface explains that its permission must be enabled

#### Scenario: User tests gesture or leash without acknowledgement
- **WHEN** the user invokes a gesture or leash Test without completing the automation-risk acknowledgement
- **THEN** the action does not execute and the interface explains that acknowledgement is required

#### Scenario: User tests an unavailable or invalid action
- **WHEN** the selected action lacks required local configuration or its integration fails
- **THEN** no unrelated state changes and the interface displays a failure result specific enough to identify the action that failed

#### Scenario: User tests every action family
- **WHEN** the user configures title, outfit, gesture, collar, Moodles, and leash actions
- **THEN** the interface exposes local tests for apply/play/engage and their corresponding clear/unlock/release operations where applicable
