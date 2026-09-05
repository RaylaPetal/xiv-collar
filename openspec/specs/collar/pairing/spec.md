# collar/pairing Specification

## Purpose

Establishes the consent boundary the whole collar system depends on: no command from an Owner reaches a Sub's game state until the Sub has explicitly paired, and the Sub can always immediately unpair and revert everything.

## Requirements

### Requirement: Configured-identity pairing consent
The system SHALL NOT apply any Owner-issued command to a Sub's local state until the Sub has explicitly configured that Owner's exact character name and world, and enabled an explicit "Paired" setting. The system SHALL NOT enable this setting on either side without an explicit action from that side - either accepting a pending pairing request, or having deliberately sent the invite that request originated from and later received its matching confirmation (see "One-way pairing handshake completes both sides"). Peer identity SHALL be established by matching the configured character name and world against the verified sender of an incoming chat message - a value FFXIV's own server guarantees cannot be forged - rather than by a shared code or free-text entry. The handshake message that establishes a pairing request SHALL also carry the sending side's own currently-configured trigger phrase, and accepting that request SHALL capture it as the peer's trigger phrase alongside their name and world, so the two sides never need to separately, manually agree on a matching trigger phrase. When the accepting Sub has both a configured collar item and the "Collar" permission enabled (see `collar/collaring`), accepting a pairing request SHALL also apply and lock that collar item as part of the same action.

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

### Requirement: One-way pairing handshake completes both sides
The system SHALL let either side initiate pairing by sending a single invite tell carrying their own code and declared role and trigger phrase, in place of requiring both sides to separately send their own invite. The receiving side's Pending request and explicit Accept action work exactly as described in "Configured-identity pairing consent." Accepting a pending pairing request SHALL, as part of that one action, automatically send a single confirmation tell back to the inviting sender, carrying the accepting side's own declared role, trigger phrase, and the code that was matched. Upon receiving a confirmation tell whose carried code matches this side's own currently-configured code, the inviting side SHALL automatically complete pairing using the confirmed sender's identity and declared role/trigger phrase, with no further explicit local action required.

If the inviting side already has an invite outstanding (sent and not yet confirmed or expired) when it starts sending another one, the system SHALL require an explicit confirmation naming the pending invite being replaced before proceeding, rather than silently discarding it - a confirmation tell later arriving for the replaced invite SHALL still be ignored (it no longer matches the inviting side's current code), but the user SHALL have been told this would happen. After receiving a confirmation tell, the inviting side SHALL retry completing pairing (fetching and verifying the confirmed acceptance) a bounded number of times with backoff before giving up, rather than abandoning the attempt on the first failure; if every retry fails, the system SHALL surface an error to the inviting side rather than failing silently.

#### Scenario: One send and one accept pairs both sides
- **WHEN** one side sends an invite tell and the receiving side clicks Accept
- **THEN** the receiving side is paired immediately, and the inviting side becomes paired automatically upon receiving the resulting confirmation tell, with no further action from either side

#### Scenario: A confirmation tell with a non-matching code is ignored
- **WHEN** an inviting side receives a confirmation-shaped tell whose carried code does not match its own currently-configured code
- **THEN** the inviting side's pairing state is unchanged

#### Scenario: Both sides send an invite at the same time
- **WHEN** each side independently sends the other an invite before either accepts
- **THEN** each side's Pending request and Accept action work independently and normally, and if both sides accept, both end up paired with no error, duplicate state, or conflicting outcome

#### Scenario: Sending a new invite while one is already outstanding
- **WHEN** the inviting side already has an unconfirmed, unexpired invite outstanding and starts sending another invite
- **THEN** the system asks for explicit confirmation naming the outstanding invite's target before sending the new one, and does not send it until confirmed

#### Scenario: A confirmation arrives for a replaced invite
- **WHEN** the recipient of a replaced invite later accepts it and its confirmation tell reaches the inviting side
- **THEN** the inviting side ignores it, since it no longer matches the inviting side's current outstanding code, and its own pairing state is unchanged

#### Scenario: Completing pairing after a transient failure
- **WHEN** the inviting side receives a valid confirmation tell but the first attempt to fetch and verify the confirmed acceptance fails transiently
- **THEN** the system retries a bounded number of times with backoff before giving up

#### Scenario: Completing pairing fails after every retry
- **WHEN** every retry to fetch and verify a confirmed acceptance fails
- **THEN** the inviting side surfaces an error explaining that pairing could not be completed, instead of leaving the invite outstanding with no explanation

### Requirement: Invite target is validated before sending
The system SHALL validate that a typed invite target matches the expected "Name Surname@World" shape before composing and sending the invite tell, and SHALL reject an invalid target with a visible explanation instead of silently sending a malformed `/tell` that the game itself will reject with no plugin-visible feedback.

#### Scenario: Target is missing a world
- **WHEN** a user triggers Send Invite with a target that has no `@World` portion
- **THEN** the system rejects the attempt and explains that a world is required, without sending anything

#### Scenario: Well-formed target is sent normally
- **WHEN** a user triggers Send Invite with a target matching "Name Surname@World"
- **THEN** the system composes and sends the invite tell exactly as it does today

### Requirement: Sub's pairing identity configuration locks while paired
While a Sub's own "Paired" setting is enabled, the system SHALL prevent changing Role, the Sub's own code, "their code," and the trigger phrase through the Settings UI. These SHALL only become editable again once the Sub's pairing ends via the panic action - never through a Settings control, consistent with how the Sub's own "Paired" flag itself already only unlocks via panic. This restriction applies only to the side currently configured as Sub; the Owner side's identity configuration remains editable at any time, consistent with the Owner already being able to `ReleasePeer()` locally without restriction.

#### Scenario: Paired Sub cannot change Role, code, or trigger phrase
- **WHEN** a Sub's "Paired" setting is enabled
- **THEN** the Role selector, the Sub's own code controls, "their code," and the trigger phrase field are all read-only in Settings

#### Scenario: Panic unlocks pairing identity configuration
- **WHEN** a paired Sub triggers the panic action
- **THEN** Role, code, and trigger phrase become editable again in Settings

#### Scenario: Owner's identity configuration is never locked by pairing
- **WHEN** the local Role is configured as Owner and pairing is active
- **THEN** Role, code, and trigger phrase remain editable in Settings the same as while unpaired

### Requirement: Panic notifies the peer, best-effort
When the panic action runs, the system SHALL, as a direct and synchronous consequence of that one local action, attempt to send a single notification tell to the peer identity that was cached at the moment panic ran, carrying the panicking side's role. This notification SHALL NOT delay, gate, or otherwise affect any of panic's own local, unconditional effects (see "Local panic/safeword") - it is a best-effort addition on top, never a precondition. Delivery is not guaranteed and is never verified; if the peer is offline, has blocked the sender, or no longer has the plugin installed, the notification silently fails exactly as any other `/tell` would, and the peer never learns what happened through this mechanism.

#### Scenario: Panic sends one notification tell
- **WHEN** a Sub or Owner triggers the panic action while a peer identity is cached
- **THEN** exactly one `collarunpair` tell is sent to that cached peer identity, carrying the panicking side's role, and every other panic step still runs regardless of whether that send succeeds

#### Scenario: Panic's local effects never wait on the notification
- **WHEN** the panic action runs and the notification tell cannot be sent (relay/network unavailable, no peer cached, or the send throws)
- **THEN** every other panic step still completes exactly as it would if the notification had succeeded

#### Scenario: An unreachable peer never learns via this mechanism
- **WHEN** the notified peer is offline, has blocked the sender, or no longer has the plugin installed
- **THEN** the notification is not delivered, and this system has no way to detect or report that failure to the panicking side

### Requirement: Receiving a panic notification updates the header
When a `collarunpair` notification tell arrives from the character currently configured as this client's peer, the system SHALL record that the peer's pairing ended via panic and SHALL reflect it in the main character header. For an Owner, the header SHALL surface the existing "Release pairing" action alongside an explanation that the Sub's side panicked - this SHALL NOT introduce any new unpairing capability, only surface the existing one with context. For a Sub, the header SHALL show an informational note that the Owner's side panicked, without offering any new way to end the Sub's own pairing - the Sub's pairing SHALL remain governed entirely by their own panic action, unchanged by this notice.

#### Scenario: Owner sees their Sub panicked
- **WHEN** an Owner's client receives a `collarunpair` notification from its currently-configured peer
- **THEN** the header explains that the Sub's side ended via panic and shows the existing "Release pairing" action

#### Scenario: Sub sees their Owner panicked
- **WHEN** a Sub's client receives a `collarunpair` notification from its currently-configured peer
- **THEN** the header shows an informational note that the Owner's side ended via panic, and the Sub's own pairing state and available actions are otherwise unchanged

#### Scenario: A notification from an unrecognized sender is ignored
- **WHEN** a `collarunpair` tell arrives from a sender that does not match the currently-configured peer name and world
- **THEN** the system takes no action and records no notice

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

### Requirement: Uninstall as ultimate safeword
The system's documentation SHALL state plainly that uninstalling the plugin ends all collar control immediately, since no command can apply without the Sub's plugin running.

#### Scenario: Sub uninstalls the plugin
- **WHEN** a Sub uninstalls or disables the plugin
- **THEN** no further Owner commands can be applied to that Sub's client
