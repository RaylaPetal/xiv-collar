## MODIFIED Requirements

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

## ADDED Requirements

### Requirement: One-way pairing handshake completes both sides
The system SHALL let either side initiate pairing by sending a single invite tell carrying their own code and declared role and trigger phrase, in place of requiring both sides to separately send their own invite. The receiving side's Pending request and explicit Accept action work exactly as described in "Configured-identity pairing consent." Accepting a pending pairing request SHALL, as part of that one action, automatically send a single confirmation tell back to the inviting sender, carrying the accepting side's own declared role, trigger phrase, and the code that was matched. Upon receiving a confirmation tell whose carried code matches this side's own currently-configured code, the inviting side SHALL automatically complete pairing using the confirmed sender's identity and declared role/trigger phrase, with no further explicit local action required.

#### Scenario: One send and one accept pairs both sides
- **WHEN** one side sends an invite tell and the receiving side clicks Accept
- **THEN** the receiving side is paired immediately, and the inviting side becomes paired automatically upon receiving the resulting confirmation tell, with no further action from either side

#### Scenario: A confirmation tell with a non-matching code is ignored
- **WHEN** an inviting side receives a confirmation-shaped tell whose carried code does not match its own currently-configured code
- **THEN** the inviting side's pairing state is unchanged

#### Scenario: Both sides send an invite at the same time
- **WHEN** each side independently sends the other an invite before either accepts
- **THEN** each side's Pending request and Accept action work independently and normally, and if both sides accept, both end up paired with no error, duplicate state, or conflicting outcome

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
