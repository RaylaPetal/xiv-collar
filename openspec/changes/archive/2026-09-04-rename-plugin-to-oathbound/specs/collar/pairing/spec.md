## MODIFIED Requirements

### Requirement: Local panic/safeword
The system SHALL provide the Sub with an always-available local action (hotkey or command) that immediately unpairs from the Owner, reverts all Glamourer state, clears any Honorific title, and cancels any active follow/movement lock. This action SHALL function even when the relay/network connection is unavailable. The main collar header SHALL always expose the current safeword configuration and allow it to be set, changed, or cleared regardless of role, login-data availability, or pairing state; this configuration control SHALL NOT itself trigger panic. The panic command SHALL be invocable as `/oathboundpanic`, its primary name; `/collarpanic` SHALL continue to work as a backward-compatible alias with identical behavior.

#### Scenario: Sub triggers panic while connected
- **WHEN** a paired Sub triggers the panic action
- **THEN** the pairing ends, the Sub's Glamourer state is reverted, the Sub's title is cleared, and any active movement lock is cancelled

#### Scenario: Sub triggers panic while relay is unreachable
- **WHEN** a Sub triggers the panic action and the relay/network connection is down
- **THEN** all local state (Glamourer, Honorific, movement lock) is still reverted using only local state, and the pairing is marked ended locally

#### Scenario: User configures safeword before pairing
- **WHEN** a user who is not paired sets or clears the safeword from the main header
- **THEN** the new local safeword configuration is saved immediately and governs the next `/oathboundpanic` invocation

#### Scenario: User configures safeword while paired
- **WHEN** a user with an active pairing sets or clears the safeword from the main header
- **THEN** the new local safeword configuration is saved immediately without changing the pairing or triggering panic

#### Scenario: Legacy panic command alias still works
- **WHEN** a Sub invokes `/collarpanic` instead of `/oathboundpanic`
- **THEN** panic triggers with exactly the same behavior as invoking `/oathboundpanic`

#### Scenario: Main window opens without character data
- **WHEN** the main window opens before a local character is available
- **THEN** the safeword configuration remains visible and editable
