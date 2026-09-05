## ADDED Requirements

### Requirement: Relay pairing is understandable and consent-driven
Settings SHALL present relay-assisted pairing as the only pairing path (there is no manual fallback). The surface SHALL show invitation expiry, expected role, verified character identity once a tell is received, relay connectivity, and the exact action that constitutes consent. It SHALL hide capability secrets and raw keys from ordinary display.

#### Scenario: User receives a relay invitation
- **WHEN** a valid invitation tell is received
- **THEN** Settings shows who sent it, both declared roles, its expiry, and explicit Accept and Reject actions

#### Scenario: Relay is unavailable
- **WHEN** connectivity or service limits prevent relay pairing
- **THEN** Settings shows a non-destructive failure explaining that pairing cannot proceed until the relay is reachable again, with no alternative pairing route offered

### Requirement: Catalog synchronization communicates permission and cooldown
The Sub surface SHALL provide an opt-in permission for paired-Owner catalog requests. The Owner surface SHALL provide Request refresh only while actively paired and SHALL show last success, current phase, next permitted request time, snapshot age, and actionable failure text. Controls SHALL prevent repeated submission during an active request or four-hour cooldown.

#### Scenario: Refresh is cooling down
- **WHEN** the Owner views catalog controls before the next permitted request time
- **THEN** Request refresh is disabled and the remaining wait is visible

#### Scenario: Synchronization completes
- **WHEN** a catalog snapshot imports successfully
- **THEN** the Owner sees completion time and updated command counts without selecting a file

### Requirement: Safety revocation state is visible but never blocking
After unpair or panic, the UI SHALL report local teardown as complete independently of relay notification status and SHALL distinguish delivered, pending retry, expired, and failed peer notification. No retry control or status state SHALL re-enable pairing or restrictions.

#### Scenario: Panic notification cannot upload
- **WHEN** panic succeeds locally but its relay notification fails
- **THEN** the UI clearly says local safety actions completed and that remote synchronization may wait until retry or peer tell receipt
