## MODIFIED Requirements

### Requirement: Explicit pairing handshake
The system SHALL NOT apply any Owner-issued command to a Sub's local state until the Sub has explicitly accepted a pairing request. The system SHALL NOT auto-accept a first-time pairing under any configuration. The peer name shown during the handshake and while paired SHALL be derived from the peer's own local character name, not from free-text entry, so neither party can misrepresent who they are during pairing.

#### Scenario: Sub accepts a pairing code
- **WHEN** an Owner shares a one-time pairing code and the Sub enters and confirms it in their own client
- **THEN** the two clients become paired and the Owner may begin sending commands

#### Scenario: Unpaired Owner cannot command
- **WHEN** a command arrives from a client that is not currently paired with the receiving Sub
- **THEN** the Sub's plugin discards the command and applies no state change

#### Scenario: Peer identity comes from the character name
- **WHEN** an Owner requests pairing, or a Sub accepts a pending pairing request
- **THEN** the name shown to the other party is the requester's own currently logged-in character name, with no free-text field offered as an alternative
