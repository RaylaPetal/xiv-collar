## 1. Protocol and Threat-Boundary Foundation

- [x] 1.1 Define versioned invitation, acceptance, pair, revocation, catalog-request, catalog-response, error, and retry schemas in language-neutral fixtures; verify schemas contain no gameplay-command envelope and reject unknown types.
- [x] 1.2 Specify canonical JSON, request-signing input, capability hashing, timestamp/nonce rules, pair epochs, monotonic sequence rules, ECDSA/ECDH/HKDF/AES-GCM parameters, and size/expiry constants; verify published cross-runtime vectors pass in both C# and Worker tests.
- [x] 1.3 Document the threat model, retained metadata, deletion windows, local-key-storage limitation under Wine, abuse assumptions, and explicit command-transport exclusion; verify every relay field has a purpose and retention classification.

## 2. Cloudflare Relay Service

- [x] 2.1 Scaffold a TypeScript Cloudflare Worker with Wrangler configuration, local development environment, lint/typecheck/test commands, pinned runtime compatibility date, and separate local/staging/production bindings; verify it runs locally without production credentials.
- [x] 2.2 Add D1 migrations for hashed device/pair capabilities, invitation state, catalog request/cooldown state, nonce replay records, and revocation state with indexes and expiry fields; verify migrations apply cleanly to an empty local database and upgrade idempotently.
- [x] 2.3 Add R2 ciphertext storage with server-enforced content type, maximum compressed/encrypted size, 15-minute expiry, one-use retrieval, eager deletion, and scheduled orphan cleanup; verify plaintext and caller-controlled retention cannot be stored.
- [x] 2.4 Implement signed request authentication, canonical path/body validation, clock skew, nonce consumption, capability verification, device-key binding, pair epoch checks, and uniform non-enumerating errors; verify tampering, substitution, replay, expiry, and guessed identifiers fail closed.
- [x] 2.5 Implement single-use invitation creation/fetch/accept/consume endpoints and pair-record activation without character names/worlds; verify concurrent acceptance produces exactly one successful pairing result.
- [x] 2.6 Implement authenticated revocation publish/check endpoints with sequence/epoch ordering and safety-priority availability; verify stale revocations cannot affect a newer pair epoch.
- [x] 2.7 Implement catalog request/status/upload/consume endpoints with transactional active-request collapse and one accepted upload per pair per four hours; verify concurrent requests, clock changes, and client retries cannot bypass cooldown.
- [x] 2.8 Apply layered per-device, pair, origin, endpoint, byte, active-object, and global quotas plus `Retry-After` and a configurable circuit breaker; verify new non-safety work stops at limits while accepted revocation retrieval remains available.
- [x] 2.9 Add scheduled cleanup, redacted structured logs, aggregate metrics, health reporting, and quota alarms without payloads, secrets, keys, or character identity; verify a log-capture test finds no prohibited fields.

## 3. Plugin Cryptography and Relay Client

- [x] 3.1 Add versioned plugin configuration for device public/private identity, the release-pinned HTTPS relay origin, pair capability/epoch, sequence counters, cooldown/snapshot metadata, pending operations, and revocation outbox; verify legacy configurations load unchanged and cannot redirect traffic to another relay.
- [x] 3.2 Implement device identity generation, best-available local key protection, fingerprinting, confirmed reset, and private-key redaction from serialization paths that leave the configuration store; verify reset invalidates relay state and test exports/logs contain no private material.
- [x] 3.3 Implement canonical envelope signing/verification and ECDH-HKDF-AES-GCM catalog encryption/decryption; verify Worker-generated and plugin-generated vectors interoperate and altered metadata/ciphertext fails authentication.
- [x] 3.4 Implement a bounded asynchronous relay HTTP client with HTTPS validation, a hardcoded origin and disabled redirects, payload limits, cancellation, timeouts, `Retry-After`, jittered exponential backoff, structured errors, and no retry of permanent failures; verify requests stop on logout/disposal/expiry and never block the framework thread.
- [x] 3.5 Implement explicit pairing/catalog state machines and a small revocation retry outbox isolated from gameplay dispatch; verify duplicate callbacks, restarts, late responses, and cancellation are idempotent.

## 4. Relay-Assisted Pairing

- [x] 4.1 Add invitation creation and explicit send flow using the existing `ChatSender` boundary and a short versioned lifecycle tell; verify one click creates one expiring invitation and sends at most one tell.
- [x] 4.2 Parse relay invitation tells separately from gameplay commands, bind the inviter device proof to the server-verified sender, validate declared roles/trigger metadata, and create a non-active pending request; verify malformed or copied references cannot activate pairing.
- [x] 4.3 Add receiver Accept/Reject behavior that revalidates invitation state, persists no active pair before success, publishes signed acceptance, and sends one bounded acknowledgement tell; verify Reject and failed acceptance leave pairing unchanged.
- [x] 4.4 Complete inviter activation only after the acknowledgement's verified sender matches the fetched signed acceptance; verify relay acceptance without the matching tell remains pending and cannot enable command composition.
- [x] 4.5 Remove the manual code/tell pairing mechanism entirely (`PairingCommand`'s code fields, `CodeGenerator`, the `collarpair`/`collarpairack` tell handlers, and their Settings/CollarWindow UI) now that relay-assisted pairing is the only pairing path; verify no code path can activate a pairing without a completed relay invitation/acceptance, and that Cloudflare being unavailable never alters an already-active pairing.

## 5. Unpair and Panic Synchronization

- [x] 5.1 Refactor unpair and panic orchestration to snapshot notification data, finish all local teardown first, invalidate local relay capability state, then attempt tell and relay notifications independently; verify injected network failures never skip a local safety step.
- [x] 5.2 Publish signed epoch/sequence revocations and persist bounded retry state containing no command or catalog content; verify retry survives restart, expires visibly, and never restores pairing.
- [x] 5.3 Process matching revocation tells immediately and check the relay at login plus no more frequently than every six hours with jitter; verify a missed tell eventually ends the stale peer pairing without constant polling.
- [x] 5.4 Reject wrong-device, wrong-pair, stale-sequence, expired, and old-epoch revocations; verify re-pairing the same characters is not broken by delayed old notices.

## 6. Encrypted Automatic Catalog Synchronization

- [x] 6.1 Add a Sub opt-in permission for paired-Owner relay catalog requests, defaulting off for existing and new installs; verify a denied request generates/uploads no snapshot and returns only a permission status.
- [x] 6.2 Add Owner request creation and explicit lifecycle-tell send with client and server four-hour cooldown enforcement and active-request deduplication; verify rapid clicks, restarts, concurrency, and local-clock changes cannot create extra accepted syncs.
- [x] 6.3 Handle valid catalog-request tells on the paired Sub by authenticating Owner device/character binding, building the current export, compressing/encrypting it, uploading once, and clearing sensitive buffers; verify requests from unpaired/mismatched devices and oversized snapshots fail closed.
- [x] 6.4 Retrieve, authenticate, decrypt, decompress, and schema-validate snapshots on the Owner with bounded allocation and newer-snapshot checks; verify corrupt, expired, cross-pair, decompression-bomb, and replayed snapshots leave existing imports untouched.
- [x] 6.5 Refactor catalog parsing into a mutation-free staging plan and atomic commit tagged by source pair/snapshot; verify additions/changes/removals apply together while manual entries, stable identities, favorites, and presentation-only edits are preserved.
- [x] 6.6 Reuse staging validation for manual file import where compatible and add an explicit legacy-import associate/reset path; verify relay adoption never silently deletes unscoped legacy imports.
- [x] 6.7 Preserve manual export/import independently of relay permission, availability, and cooldown; verify the complete offline workflow still succeeds with HTTP disabled.

## 7. Consent and Status UI

- [x] 7.1 Rework Settings pairing presentation for relay-assisted pairing (the only pairing path) showing expiry, roles, verified sender, connectivity, pending phase, explicit consent, and rejection without displaying secrets/raw keys; verify minimum-size and keyboard interaction remain usable.
- [x] 7.2 Add device identity status/fingerprint and a destructive reset confirmation explaining that relay pairings will end; verify reset cannot occur from a single accidental click.
- [x] 7.3 Add Sub catalog-sync permission text and Owner refresh controls showing current phase, last successful snapshot/counts, snapshot age, next allowed time, and actionable failures; verify active/cooldown states disable duplicate requests.
- [x] 7.4 Add unpair/panic notification status that distinguishes local completion from tell/relay delivered, pending, expired, and failed states; verify no status or retry action can re-enable pairing or restrictions.

## 8. Integration, Deployment, and Cost Verification

- [x] 8.1 Add Worker contract, storage-lifecycle, concurrency, replay, quota, circuit-breaker, and redacted-logging suites using local D1/R2 emulation; verify the complete Worker suite passes without Cloudflare credentials.
- [x] 8.2 Add plugin pairing, revocation, crypto-vector, relay-client, restart/idempotency, catalog-atomicity, legacy migration, and offline-fallback tests with deterministic clocks and mocked HTTP/chat; verify Debug and Release builds plus all automated suites pass.
- [x] 8.3 Run end-to-end two-client staging tests for invitation/acceptance, wrong sender, expiry, restart, unpair, panic with each transport unavailable, permitted/denied catalog sync, cooldown, stale snapshot, and catalog's manual file-transfer fallback; record results and verify no operational command reaches the Worker.
- [x] 8.4 Add Cloudflare deployment/runbook documentation covering account setup, D1/R2 creation, secrets, migrations, staging promotion, pinned-origin plugin releases, rollback/circuit breaker, retention cleanup, alarms, free-tier dashboards, and incident/key rotation; verify a clean staging deployment from the documented commands.
- [x] 8.5 Load-test invitation, six-hour revocation checks, and four-hour catalog sync at projected 1k/10k/100k active-client profiles; record request/storage/CPU projections and configure hard quotas so unexpected traffic fails closed before the chosen budget ceiling.
- [x] 8.6 Perform a privacy/security review against the documented threat model, including capability leakage, signature confusion, replay, enumeration, decompression bombs, log leakage, origin spoofing, and service compromise; resolve all high-severity findings before enabling production relay defaults.
