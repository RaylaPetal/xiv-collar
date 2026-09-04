# collar/pairing Specification

## Purpose

Establishes the consent boundary the whole collar system depends on: no command from an Owner reaches a Sub's game state until the Sub has explicitly paired, and the Sub can always immediately unpair and revert everything.

## Requirements

### Requirement: Configured-identity pairing consent
The system SHALL NOT apply any Owner-issued command to a Sub's local state until the Sub has explicitly configured that Owner's exact character name and world, and enabled an explicit "Paired" setting. The system SHALL NOT auto-enable this setting under any configuration. Peer identity SHALL be established by matching the configured character name and world against the verified sender of an incoming chat message - a value FFXIV's own server guarantees cannot be forged - rather than by a shared code or free-text entry. The handshake message that establishes a pairing request SHALL also carry the sending side's own currently-configured trigger phrase, and accepting that request SHALL capture it as the peer's trigger phrase alongside their name and world, so the two sides never need to separately, manually agree on a matching trigger phrase. When the accepting Sub has both a configured collar item and the "Collar" permission enabled (see `collar/collaring`), accepting a pairing request SHALL also apply and lock that collar item as part of the same action.

#### Scenario: Sub configures and enables pairing
- **WHEN** a Sub enters an Owner's exact character name and world in Settings and explicitly enables the "Paired" setting
- **THEN** trigger messages sent by that character begin applying to the Sub's local state

#### Scenario: Unconfigured or unmatched sender cannot command
- **WHEN** a trigger message arrives from a character that does not match the Sub's configured Owner, or while the "Paired" setting is disabled
- **THEN** the Sub's plugin discards the message and applies no state change

#### Scenario: Peer identity comes from the character name
- **WHEN** a Sub enables pairing, or an Owner composes a trigger message
- **THEN** the identity used is the actual, server-verified character name of whoever configured or sent it, with no free-text field offered as an alternative

#### Scenario: Accepting a pairing request applies a configured collar
- **WHEN** a Sub accepts a pending pairing request, and the Sub has a collar item configured with the "Collar" permission enabled
- **THEN** the Sub's client applies that item to the Neck slot and locks it, as part of accepting

#### Scenario: Accepting a pairing request with no collar configured
- **WHEN** a Sub accepts a pending pairing request, and the Sub has no collar item configured, or the "Collar" permission is disabled
- **THEN** accepting completes exactly as it did before, with no equipment change

#### Scenario: Accepting a pairing request captures the peer's trigger phrase
- **WHEN** a Sub accepts a pending pairing request whose handshake message declared the sender's trigger phrase
- **THEN** that trigger phrase is stored as the peer's trigger phrase, to be used when composing future outgoing commands to that peer

#### Scenario: Handshake from an un-updated peer omits a trigger phrase
- **WHEN** a pairing request's handshake message does not declare a trigger phrase (an older paired client)
- **THEN** accepting the request completes exactly as it did before, with no peer trigger phrase captured

### Requirement: Scoped, revocable permissions
The system SHALL let a paired Sub independently enable or disable each command category (title, outfit, gesture, follow) at any time. The system SHALL reject a command in a category the Sub has not enabled, even if the pairing itself remains active.

#### Scenario: Sub disables one category
- **WHEN** a Sub disables the "follow" permission while "outfit" and "title" remain enabled
- **THEN** subsequent follow commands from the paired Owner are rejected while outfit and title commands continue to apply

#### Scenario: Permission change takes effect immediately
- **WHEN** a Sub toggles a permission category
- **THEN** the new permission state applies to the next command received, without requiring re-pairing

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

### Requirement: Uninstall as ultimate safeword
The system's documentation SHALL state plainly that uninstalling the plugin ends all collar control immediately, since no command can apply without the Sub's plugin running.

#### Scenario: Sub uninstalls the plugin
- **WHEN** a Sub uninstalls or disables the plugin
- **THEN** no further Owner commands can be applied to that Sub's client
