## ADDED Requirements

### Requirement: Always-visible local character and relationship header
The main collar window SHALL present a polished, responsive header for the current local character in every pairing state. When available, the header SHALL show the character's name and home world and MAY show supplemental local character information such as Free Company affiliation. It SHALL always state whether the character is not paired, has a pending pairing request, owns a named Sub, or is owned by a named Owner. Optional or temporarily unavailable character information SHALL be omitted or represented as loading without displaying stale data or hiding pairing status.

#### Scenario: Local character is available while not paired
- **WHEN** the main window opens with a logged-in local character and no active or pending pairing
- **THEN** the header shows the character name and home world together with an explicit “Not paired” status

#### Scenario: Local character is paired as Owner
- **WHEN** the configured role is Owner and the pairing identifies a Sub
- **THEN** the header identifies the local character and states that they own the named Sub and world

#### Scenario: Local character is paired as Sub
- **WHEN** the configured role is Sub and the pairing identifies an Owner
- **THEN** the header identifies the local character and states that they are owned by the named Owner and world

#### Scenario: Pairing request is pending
- **WHEN** a valid pairing request is awaiting acceptance
- **THEN** the header prominently identifies the requesting character and exposes the existing accept and reject actions without obscuring local identity or safety controls

#### Scenario: Optional character details are unavailable
- **WHEN** the local player, home world, or optional Free Company information is not currently available
- **THEN** the header remains usable, does not reuse details from a previously logged-in character, and continues to show pairing and safeword state

#### Scenario: Header is narrow
- **WHEN** the main window is at its minimum supported width or localized values are long
- **THEN** character, relationship, and safety content wraps or reorganizes without clipping controls or overlapping text

## MODIFIED Requirements

### Requirement: Local panic/safeword
The system SHALL provide the Sub with an always-available local action (hotkey or command) that immediately unpairs from the Owner, reverts all Glamourer state, clears any Honorific title, and cancels any active follow/movement lock. This action SHALL function even when the relay/network connection is unavailable. The main collar header SHALL always expose the current safeword configuration and allow it to be set, changed, or cleared regardless of role, login-data availability, or pairing state; this configuration control SHALL NOT itself trigger panic.

#### Scenario: Sub triggers panic while connected
- **WHEN** a paired Sub triggers the panic action
- **THEN** the pairing ends, the Sub's Glamourer state is reverted, the Sub's title is cleared, and any active movement lock is cancelled

#### Scenario: Sub triggers panic while relay is unreachable
- **WHEN** a Sub triggers the panic action and the relay/network connection is down
- **THEN** all local state (Glamourer, Honorific, movement lock) is still reverted using only local state, and the pairing is marked ended locally

#### Scenario: User configures safeword before pairing
- **WHEN** a user who is not paired sets or clears the safeword from the main header
- **THEN** the new local safeword configuration is saved immediately and governs the next `/collarpanic` invocation

#### Scenario: User configures safeword while paired
- **WHEN** a user with an active pairing sets or clears the safeword from the main header
- **THEN** the new local safeword configuration is saved immediately without changing the pairing or triggering panic

#### Scenario: Main window opens without character data
- **WHEN** the main window opens before a local character is available
- **THEN** the safeword configuration remains visible and editable
