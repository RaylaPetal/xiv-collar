## Purpose

Defines the command transport that carries Owner-issued commands to a paired Sub's client and returns acknowledgements, independent of what any individual command's payload means.

## ADDED Requirements

### Requirement: Command delivery channel
The system SHALL deliver commands from a paired Owner's client to the corresponding Sub's client over a dedicated relay channel (websocket or equivalent), not through in-game chat text.

#### Scenario: Owner sends a command
- **WHEN** an Owner's client sends a command for a paired Sub
- **THEN** the relay delivers the command to that Sub's client without transiting any in-game chat channel

#### Scenario: Relay does not use chat smuggling
- **WHEN** the transport layer is implemented
- **THEN** it SHALL NOT encode command payloads into tell/party/other in-game chat messages

### Requirement: Acknowledgement and current-state reply
The system SHALL have the Sub's client send an acknowledgement back to the Owner's client after processing each command, indicating whether the command was applied, rejected (e.g. permission disabled), or failed.

#### Scenario: Command applied successfully
- **WHEN** a Sub's client successfully applies a received command
- **THEN** it sends an acknowledgement to the Owner's client confirming the applied state

#### Scenario: Command rejected by permission
- **WHEN** a Sub's client receives a command for a category the Sub has not enabled
- **THEN** it sends back a rejection acknowledgement rather than silently dropping the command

### Requirement: Delivery only to paired, connected clients
The system SHALL only route a command to the Sub it was addressed to, and SHALL surface a delivery failure to the Owner when that Sub's client is not currently connected to the relay.

#### Scenario: Sub offline
- **WHEN** an Owner sends a command while the target Sub's client is not connected to the relay
- **THEN** the Owner's client is informed the command could not be delivered
