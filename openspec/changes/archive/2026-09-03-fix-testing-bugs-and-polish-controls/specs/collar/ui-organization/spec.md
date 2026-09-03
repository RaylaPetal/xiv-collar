## MODIFIED Requirements

### Requirement: Every Sub action can be tested locally before pairing
The Sub-facing interface SHALL provide an explicit local Test control for every configurable action: title apply and clear, outfit apply and unlock, gesture playback, collar lock and unlock, Moodles apply and clear, and leash and unleash. A local test SHALL execute through the same local action path used for an accepted Owner command, SHALL NOT require an active or pending pairing, and SHALL NOT compose or transmit a chat message. The action's normal category permission SHALL remain required, and gesture or leash testing SHALL additionally require the existing automation-risk acknowledgement. Every test SHALL report a visible success or failure result. Each Test control SHALL identify the specific action it performs without requiring a tooltip, and its reported result SHALL be transient, clearing itself automatically a short time after being shown.

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

#### Scenario: User identifies a Test control without hovering
- **WHEN** a Test control is shown next to a configured action
- **THEN** its label identifies the specific action it performs, without requiring the user to hover for a tooltip

#### Scenario: Test result clears automatically
- **WHEN** a Test control reports a success or failure result
- **THEN** that result is shown next to the control and then automatically clears itself a short time later, rather than persisting indefinitely

## ADDED Requirements

### Requirement: Sub can hide local Test controls
The system SHALL let a Sub disable the visibility of every local Test control via a dedicated setting, hiding them from the Sub-facing interface without disabling the underlying local-test capability or affecting any other control.

#### Scenario: Sub hides Test controls
- **WHEN** a Sub disables the local Test visibility setting
- **THEN** no local Test control is rendered anywhere in the Sub-facing interface

#### Scenario: Sub re-enables Test controls
- **WHEN** a Sub re-enables the local Test visibility setting
- **THEN** local Test controls are rendered again in their normal locations
