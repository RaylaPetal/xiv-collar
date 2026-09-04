## ADDED Requirements

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
