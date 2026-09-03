## MODIFIED Requirements

### Requirement: Acknowledgement and current-state reply
The system SHALL have the Sub's client send an acknowledgement back to the Owner's client after processing each command, indicating whether the command was applied, rejected (e.g. permission disabled), or failed. The Owner's client SHALL visibly surface a rejected or failed acknowledgement to the Owner - not only record it in a diagnostic log - so command outcomes are always apparent from the plugin's own UI.

#### Scenario: Command applied successfully
- **WHEN** a Sub's client successfully applies a received command
- **THEN** it sends an acknowledgement to the Owner's client confirming the applied state

#### Scenario: Command rejected by permission
- **WHEN** a Sub's client receives a command for a category the Sub has not enabled
- **THEN** it sends back a rejection acknowledgement rather than silently dropping the command

#### Scenario: Rejection is visible to the Owner
- **WHEN** the Owner's client receives a rejected or failed acknowledgement
- **THEN** the Owner sees a visible in-plugin notification of the outcome, not only a log entry

### Requirement: Delivery only to paired, connected clients
The system SHALL only route a command to the Sub it was addressed to, and SHALL surface a delivery failure to the Owner when that Sub's client is not currently connected to the relay. This surfacing SHALL be visible in the Owner's plugin UI, not only recorded in a diagnostic log.

#### Scenario: Sub offline
- **WHEN** an Owner sends a command while the target Sub's client is not connected to the relay
- **THEN** the Owner's client shows a visible notification that the command could not be delivered

## ADDED Requirements

### Requirement: Automatic reconnection and visible connection status
The system SHALL automatically attempt to reconnect to the relay when an established connection is lost, and SHALL continuously display the current connection state (connected, reconnecting, or disconnected) to the user.

#### Scenario: Connection drops and recovers
- **WHEN** a client's relay connection is lost while a pairing is active
- **THEN** the client automatically attempts to reconnect without requiring the user to manually re-initiate the connection

#### Scenario: Connection status always visible
- **WHEN** a client's plugin window is open
- **THEN** the current relay connection state is visibly indicated, regardless of whether it is connected, reconnecting, or disconnected
