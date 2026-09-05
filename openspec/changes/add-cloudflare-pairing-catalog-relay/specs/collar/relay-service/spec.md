## Purpose

Provides a minimal, privacy-preserving serverless rendezvous service for pairing lifecycle events and temporary encrypted catalog transfer without carrying gameplay commands.

## ADDED Requirements

### Requirement: Released clients use the official relay origin
The plugin SHALL send relay traffic only to the release-pinned HTTPS origin
`https://oathbound-relay-staging.oathbound.workers.dev`, SHALL disable HTTP redirects, and SHALL NOT expose
a user-editable relay endpoint.

#### Scenario: Configuration cannot redirect relay traffic
- **WHEN** a legacy or hand-edited plugin configuration is loaded
- **THEN** relay requests still target the release-pinned HTTPS origin and redirects are not followed

### Requirement: Relay stores only opaque bounded envelopes
The relay SHALL accept only versioned, authenticated envelopes with random capability identifiers, enforce strict per-type size limits, and store no character name, world, command content, plaintext catalog, private key, or permanent user account. Pairing invitations and catalog payloads SHALL expire within 15 minutes, while revocation notices SHALL expire within seven days.

#### Scenario: Valid temporary envelope
- **WHEN** an authenticated client submits a supported envelope within its size and expiry limits
- **THEN** the relay stores it under an unguessable capability until it is consumed or expires

#### Scenario: Oversized or unsupported envelope
- **WHEN** a client submits an oversized payload, unsupported schema version, invalid authentication, or client-selected expiry beyond the service maximum
- **THEN** the relay rejects it without retaining the payload

#### Scenario: Expired data
- **WHEN** temporary data reaches its server-controlled expiry
- **THEN** it becomes unavailable and is deleted automatically without user action

### Requirement: Relay is capability-secured and replay-resistant
Every mutation SHALL require a signed request bound to the request method, path, body digest, timestamp, nonce, and installation public key. Read and consume operations SHALL additionally require the corresponding high-entropy capability. The relay SHALL reject expired timestamps, reused nonces, consumed capabilities, invalid signatures, and attempts to substitute another device key.

#### Scenario: Captured request is replayed
- **WHEN** an attacker resubmits a previously accepted signed request or capability
- **THEN** the relay rejects it and performs no second mutation or disclosure

#### Scenario: Capability identifier is guessed without proof
- **WHEN** a caller presents an identifier without its capability secret and valid device signature
- **THEN** the relay discloses neither payload nor metadata

### Requirement: Relay enforces layered abuse and cost controls
The relay SHALL enforce client-device, pairing-capability, network-origin, endpoint, payload-size, and global service limits. Catalog request creation and upload SHALL be limited to one accepted synchronization per pair in any four-hour window. Polling responses SHALL advertise bounded retry timing, and the service SHALL support a global circuit breaker that refuses new invitations and catalog work before configured free-tier or spending ceilings are exceeded while preserving retrieval of already accepted revocations where possible.

#### Scenario: Catalog request exceeds cooldown
- **WHEN** either peer attempts another catalog synchronization for the same pair less than four hours after the last accepted synchronization
- **THEN** the relay returns the remaining cooldown and creates no request or payload

#### Scenario: Client polls too rapidly
- **WHEN** a client polls before the server-provided retry time or exceeds its endpoint quota
- **THEN** the relay throttles the request without extending stored-data lifetime

#### Scenario: Operational ceiling is reached
- **WHEN** configured daily request, operation, or storage guardrails are reached
- **THEN** the circuit breaker rejects new non-safety work with a retryable unavailable result rather than allowing unbounded cost

### Requirement: Gameplay commands are forbidden from relay transport
The relay contract SHALL have no endpoint or envelope type for restraint, gesture, outfit, title, Moodle, follow, collar, custom-trigger, arbitrary chat, or future gameplay command delivery. Unknown envelope types SHALL fail closed.

#### Scenario: Client submits command-shaped data
- **WHEN** a caller attempts to submit a gameplay command or an unknown envelope type
- **THEN** the relay rejects it and does not queue or forward it

### Requirement: Relay deployment is reproducible and observable without sensitive logging
The service SHALL include reproducible Cloudflare deployment configuration, schema/storage lifecycle configuration, local emulation, automated contract tests, and documented free-tier quotas and alarms. Logs and metrics SHALL record aggregate outcomes, latency, size buckets, throttling, and failures but SHALL NOT record capabilities, cryptographic material, character identity, or payload bodies.

#### Scenario: Operator investigates abuse or failure
- **WHEN** the operator reviews service telemetry
- **THEN** they can identify endpoint health, quota pressure, and rejection categories without access to user catalog content or pairing secrets
