## MODIFIED Requirements

### Requirement: No automated sending
The system SHALL NOT itself invoke any function that transmits a trigger message on a user's behalf. A trigger message SHALL only be sent by the sending player's own direct action. The system MAY compose trigger text or place it on the clipboard for convenience, but SHALL NOT call any chat-send API itself. Two narrow, explicit exceptions exist, each a direct, singular, synchronous consequence of one specific local action - never a reaction to incoming chat or other background state: accepting a pending pairing request (see `collar/pairing`'s "One-way pairing handshake completes both sides") MAY automatically send a single confirmation tell back to the inviting peer; and triggering the panic action (see `collar/pairing`'s "Panic notifies the peer, best-effort") MAY automatically send a single notification tell to the cached peer identity. No other automatic send exists anywhere in the system.

#### Scenario: Composing a trigger message does not send it
- **WHEN** an Owner's client builds trigger text for a command
- **THEN** the text is made available to copy, and no chat-send function is invoked by the plugin

#### Scenario: Accepting a pairing request sends exactly one confirmation tell
- **WHEN** a user clicks Accept on a pending pairing request
- **THEN** exactly one confirmation tell is sent automatically as a direct result of that click, and no other automatic send occurs anywhere else in the plugin

#### Scenario: Panic sends exactly one notification tell
- **WHEN** a user triggers the panic action while a peer identity is cached
- **THEN** exactly one notification tell is sent automatically as a direct result of that action, and no other automatic send occurs as a result of it

## ADDED Requirements

### Requirement: Composing and sending require active pairing, not just a remembered peer
The system SHALL only compose an addressed `/tell` and only allow sending one when pairing is currently active (`Pairing.IsPaired`), not merely when a peer name and world were captured at some point in the past. A side whose own pairing has ended (via panic or an Owner's release) SHALL NOT be able to compose or send an addressed command to that former peer until pairing is established again.

#### Scenario: A side that panicked cannot still send
- **WHEN** a side has triggered panic, ending its own pairing, and a peer name/world remain cached from before
- **THEN** that side's Send controls are disabled and any composed text carries no `/tell` target, the same as before any pairing ever existed

#### Scenario: An actively paired side can still compose and send
- **WHEN** a side's pairing is currently active
- **THEN** composing and sending work exactly as they did before this requirement was added
