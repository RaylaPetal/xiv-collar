# collar/pairing Specification

## Purpose

Establishes the consent boundary the whole collar system depends on: no command from an Owner reaches a Sub's game state until the Sub has explicitly paired, and the Sub can always immediately unpair and revert everything.

## Requirements

### Requirement: Explicit pairing handshake
The system SHALL NOT apply any Owner-issued command to a Sub's local state until the Sub has explicitly accepted a pairing request. The system SHALL NOT auto-accept a first-time pairing under any configuration.

#### Scenario: Sub accepts a pairing code
- **WHEN** an Owner shares a one-time pairing code and the Sub enters and confirms it in their own client
- **THEN** the two clients become paired and the Owner may begin sending commands

#### Scenario: Unpaired Owner cannot command
- **WHEN** a command arrives from a client that is not currently paired with the receiving Sub
- **THEN** the Sub's plugin discards the command and applies no state change

### Requirement: Scoped, revocable permissions
The system SHALL let a paired Sub independently enable or disable each command category (title, outfit, gesture, follow) at any time. The system SHALL reject a command in a category the Sub has not enabled, even if the pairing itself remains active.

#### Scenario: Sub disables one category
- **WHEN** a Sub disables the "follow" permission while "outfit" and "title" remain enabled
- **THEN** subsequent follow commands from the paired Owner are rejected while outfit and title commands continue to apply

#### Scenario: Permission change takes effect immediately
- **WHEN** a Sub toggles a permission category
- **THEN** the new permission state applies to the next command received, without requiring re-pairing

### Requirement: Local panic/safeword
The system SHALL provide the Sub with an always-available local action (hotkey or command) that immediately unpairs from the Owner, reverts all Glamourer state, clears any Honorific title, and cancels any active follow/movement lock. This action SHALL function even when the relay/network connection is unavailable.

#### Scenario: Sub triggers panic while connected
- **WHEN** a paired Sub triggers the panic action
- **THEN** the pairing ends, the Sub's Glamourer state is reverted, the Sub's title is cleared, and any active movement lock is cancelled

#### Scenario: Sub triggers panic while relay is unreachable
- **WHEN** a Sub triggers the panic action and the relay/network connection is down
- **THEN** all local state (Glamourer, Honorific, movement lock) is still reverted using only local state, and the pairing is marked ended locally

### Requirement: Uninstall as ultimate safeword
The system's documentation SHALL state plainly that uninstalling the plugin ends all collar control immediately, since no command can apply without the Sub's plugin running.

#### Scenario: Sub uninstalls the plugin
- **WHEN** a Sub uninstalls or disables the plugin
- **THEN** no further Owner commands can be applied to that Sub's client
