## ADDED Requirements

### Requirement: Relay identity augments but never replaces verified tell identity
For every gameplay command, the system SHALL continue requiring an active pairing and an incoming FFXIV tell whose server-verified sender name and world match the paired character. A matching relay device key or pair capability alone SHALL never authorize, carry, queue, acknowledge, or execute a gameplay command.

#### Scenario: Relay peer lacks matching tell sender
- **WHEN** a device associated with the active relay pair submits data but no qualifying FFXIV tell arrives from the paired character
- **THEN** no gameplay command is dispatched

#### Scenario: Valid paired tell arrives
- **WHEN** an operational command arrives through the existing tell path from the paired character
- **THEN** existing trigger, permission, consent, and dispatch checks apply without a relay round trip

### Requirement: Pairing lifecycle tells remain narrowly bounded
Relay-assisted invitation and acknowledgement tells SHALL carry only a version, short-lived invitation reference, role/trigger metadata, and cryptographic proof needed to bind device identity. Panic and unpair notices SHALL carry only revocation identity and proof. These lifecycle messages SHALL NOT contain catalogs, private keys, capability secrets, or gameplay command batches.

#### Scenario: Lifecycle message contains prohibited data
- **WHEN** a pairing or revocation tell is malformed or attempts to embed unsupported payload data
- **THEN** the client rejects it without activating pairing, importing a catalog, or dispatching a command
