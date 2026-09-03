## MODIFIED Requirements

### Requirement: Configured-identity pairing consent
The system SHALL NOT apply any Owner-issued command to a Sub's local state until the Sub has explicitly configured that Owner's exact character name and world, and enabled an explicit "Paired" setting. The system SHALL NOT auto-enable this setting under any configuration. Peer identity SHALL be established by matching the configured character name and world against the verified sender of an incoming chat message - a value FFXIV's own server guarantees cannot be forged - rather than by a shared code or free-text entry. When the accepting Sub has both a configured collar item and the "Collar" permission enabled (see `collar/collaring`), accepting a pairing request SHALL also apply and lock that collar item as part of the same action.

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
