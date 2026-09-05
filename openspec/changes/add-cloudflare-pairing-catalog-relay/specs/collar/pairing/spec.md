## REMOVED Requirements

### Requirement: One-way pairing handshake completes both sides
**Reason**: The shared-code manual handshake (`collarpair`/`collarpairack` tells, `MyCode`/`PeerCode`) is removed entirely, not kept as a fallback. It is fully superseded by "Relay-assisted pairing binds device proof to verified game identity" below, which replaces the shared code with a signed, single-use relay invitation and a cryptographic acceptance proof.
**Migration**: There is no dual-mode period and nothing to migrate — an installation's prior manual pairing state (if any) is simply dropped; every pairing goes through the relay-assisted flow from this change onward.

## MODIFIED Requirements

### Requirement: Configured-identity pairing consent
The system SHALL NOT apply any Owner-issued command to a Sub's local state until the Sub has explicitly accepted a relay-assisted pairing (see "Relay-assisted pairing binds device proof to verified game identity"), and SHALL NOT enable this without an explicit action from that side. Peer identity SHALL be established by matching the verified sender of an incoming chat message against the character name and world captured during that acceptance - a value FFXIV's own server guarantees cannot be forged. The invitation and acceptance messages SHALL also carry the sending side's own currently-configured trigger phrase, captured as the peer's trigger phrase so the two sides never need to separately, manually agree on a matching trigger phrase. When the accepting Sub has both a configured collar item and the "Collar" permission enabled (see `collar/collaring`), accepting a pairing request SHALL also apply and lock that collar item as part of the same action.

#### Scenario: Sub configures and enables pairing
- **WHEN** a Sub receives and explicitly accepts a relay-assisted pairing request from an Owner
- **THEN** trigger messages sent by that character begin applying to the Sub's local state

#### Scenario: Unconfigured or unmatched sender cannot command
- **WHEN** a trigger message arrives from a character that does not match the Sub's paired peer, or while no pairing is active
- **THEN** the Sub's plugin discards the message and applies no state change

#### Scenario: Peer identity comes from the character name
- **WHEN** a Sub accepts a pairing request, or an Owner composes a trigger message
- **THEN** the identity used is the actual, server-verified character name of whoever accepted or sent it, with no free-text field offered as an alternative

#### Scenario: Accepting a pairing request applies a configured collar
- **WHEN** a Sub accepts a pending pairing request, and the Sub has a collar item configured with the "Collar" permission enabled
- **THEN** the Sub's client applies that item to the Neck slot and locks it, as part of accepting

#### Scenario: Accepting a pairing request with no collar configured
- **WHEN** a Sub accepts a pending pairing request, and the Sub has no collar item configured, or the "Collar" permission is disabled
- **THEN** accepting completes exactly as it did before, with no equipment change

#### Scenario: Accepting a pairing request captures the peer's trigger phrase
- **WHEN** a Sub accepts a pairing request whose invitation declared the sender's trigger phrase
- **THEN** that trigger phrase is stored as the peer's trigger phrase, to be used when composing future outgoing commands to that peer

#### Scenario: Handshake from an un-updated peer omits a trigger phrase
- **WHEN** a pairing invitation or acceptance does not declare a trigger phrase (an older or malformed relay envelope)
- **THEN** accepting the request completes exactly as it did before, with no peer trigger phrase captured, falling back to this side's own configured trigger phrase

### Requirement: Sub's pairing identity configuration locks while paired
While a Sub's own pairing is active, the system SHALL prevent changing Role and the trigger phrase through the Settings UI. These SHALL only become editable again once the Sub's pairing ends via the panic action - never through a Settings control. This restriction applies only to the side currently configured as Sub; the Owner side's identity configuration remains editable at any time, consistent with the Owner already being able to release a pairing locally without restriction.

#### Scenario: Paired Sub cannot change Role, code, or trigger phrase
- **WHEN** a Sub's pairing is active
- **THEN** the Role selector and the trigger phrase field are read-only in Settings (there is no separate manual code control to lock, since pairing has no manual mechanism)

#### Scenario: Panic unlocks pairing identity configuration
- **WHEN** a paired Sub triggers the panic action
- **THEN** Role and trigger phrase become editable again in Settings

#### Scenario: Owner's identity configuration is never locked by pairing
- **WHEN** the local Role is configured as Owner and pairing is active
- **THEN** Role and trigger phrase remain editable in Settings the same as while unpaired

## ADDED Requirements

### Requirement: Relay-assisted pairing binds device proof to verified game identity
The system SHALL let either role create a single-use relay invitation and explicitly send its short invitation reference in an FFXIV tell. The receiver SHALL derive the inviter's character name and world only from the verified tell sender, fetch and authenticate the invitation, display both roles and identity for explicit acceptance, and return a cryptographic acceptance proof in the existing acknowledgement tell. Pairing SHALL activate only after each side has performed its explicit initiating or accepting action and has observed the other device proof bound to the expected server-verified character sender.

#### Scenario: Secure relay-assisted pairing succeeds
- **WHEN** an inviter explicitly sends a live invitation tell, the receiver explicitly accepts it, and the acknowledgement tell arrives from the expected character with the matching device proof
- **THEN** both clients persist the peer device public key, pair capability, verified character name/world, role, and trigger phrase as one active pairing

#### Scenario: Relay acceptance lacks matching game identity
- **WHEN** relay state claims acceptance but no matching acknowledgement arrives from the expected server-verified character
- **THEN** the inviter remains pending and cannot send addressed gameplay commands as an active pairing

#### Scenario: Invitation is reused or expired
- **WHEN** a receiver attempts to accept an already consumed or expired invitation
- **THEN** pairing fails closed and the UI offers creation of a fresh invitation

### Requirement: Pairing has no manual fallback and never silently weakens
Relay-assisted pairing SHALL be the only pairing mechanism; there SHALL be no manual code-and-tell handshake to fall back to. Relay failure SHALL NOT silently downgrade a relay invitation into a weaker identity check, activate partial relay state, or prevent local unpair and panic; an existing active pairing SHALL be unaffected by the relay being unavailable.

#### Scenario: Cloudflare is unavailable during pairing
- **WHEN** a relay-assisted invitation cannot be created or fetched
- **THEN** the UI reports the outage, creates no pairing, and activates no partial relay state; the user can only retry once the relay is reachable again

#### Scenario: Cloudflare is unavailable after pairing is already active
- **WHEN** an existing pairing is active and the relay later becomes unreachable
- **THEN** the active pairing, its trigger-tell dispatch, and local panic/unpair all continue to function using only locally cached state

### Requirement: Unpair and panic publish authenticated revocation
Local unpair and panic SHALL immediately perform their existing local teardown before any network work, invalidate the local pair capability, best-effort send the existing peer notification tell, and best-effort publish a signed relay revocation. An online peer SHALL process a matching notification tell immediately; clients SHALL also check for missed revocation at startup and on a low-frequency bounded schedule. Receiving a valid newer revocation SHALL end pairing locally and SHALL NOT execute gameplay commands or restore removed restrictions.

#### Scenario: Panic while relay is unavailable
- **WHEN** a user invokes panic and Cloudflare cannot be reached
- **THEN** all local safety teardown and local unpairing complete immediately, with relay notification reported as pending or failed

#### Scenario: Peer missed the notification tell
- **WHEN** a client later observes a valid signed revocation newer than its stored pair epoch
- **THEN** it ends the stale pairing locally and disables outgoing command controls for that peer

#### Scenario: Old revocation is replayed after re-pairing
- **WHEN** a revocation from an older pair epoch is delivered after the same characters have established a new pairing
- **THEN** the client ignores it without changing the newer pairing

### Requirement: Device-key lifecycle is recoverable and explicit
Each installation SHALL create and protect a persistent device signing identity locally. Resetting that identity SHALL warn that all relay-assisted pairings become invalid, locally end those pairings, and require fresh pairing; device private keys SHALL never be exported through catalogs, tells, logs, or relay payloads.

#### Scenario: User resets the device identity
- **WHEN** the user confirms a device-identity reset
- **THEN** the plugin generates a new identity, invalidates relay pair state, and requires new invitations before relay features resume
