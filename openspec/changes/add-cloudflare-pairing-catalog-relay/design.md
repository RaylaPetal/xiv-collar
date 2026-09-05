## Context

See `proposal.md` for motivation. Today, `PairingCommand`, `ChatCommandListener`, and `ChatComposer` establish identity through server-verified FFXIV tells; `CatalogSyncService` serializes one text snapshot that users transfer manually. `ChatSender` is intentionally the only outbound chat boundary. The plugin runs under Dalamud/Wine on user machines, has no account system or trusted OS-wide installation identity, and must remain safe when HTTP is unavailable.

The new service must fit Cloudflare's free allowances initially, reveal no catalog plaintext to the operator, resist anonymous upload/poll abuse, and never become an alternate command channel. Character identity cannot be proven to an HTTP service, so FFXIV's verified tell sender remains part of pairing rather than being replaced by a web claim.

## Goals / Non-Goals

**Goals:**

- Remove manually exchanged codes and catalog files from the normal online workflow.
- Bind a locally generated cryptographic installation identity to the character identity observed through tells.
- Make unpair and panic locally immediate and remotely eventual even if the notification tell is missed.
- Keep steady-state relay traffic, storage, and operator exposure low enough for a free-tier-first deployment.
- Make every remote state transition authenticated, replay-resistant, versioned, observable, and recoverable.

**Non-Goals:**

- Transporting, queueing, or acknowledging gameplay commands through Cloudflare.
- Supporting offline gameplay commands or commands issued outside FFXIV.
- Proving a character identity to Cloudflare itself.
- Creating Oathbound user accounts, social discovery, presence, or permanent catalog hosting.
- Guaranteeing instant cross-client unpair when both FFXIV tells and Cloudflare are unavailable.

## Decisions

### Use a Worker, D1 metadata, and R2 ciphertext with hard lifecycle limits

A Cloudflare Worker exposes a small versioned JSON API. D1 holds invitation/request state, hashes of capability secrets, device public keys, pair epoch/cooldown metadata, one-use nonces, and revocation state. It never stores character names or worlds. R2 holds only compressed encrypted catalog blobs. Invitations and catalog objects expire after 15 minutes; consumed objects are deleted eagerly. Revoked pair metadata is retained only long enough to deliver revocation and prevent replay, then deleted; inactive pair records expire after a documented bounded retention period.

D1 transactions provide atomic consume/cooldown behavior that KV eventual consistency cannot. R2 avoids placing variable-size blobs in D1 and has no egress charge. Durable Objects were considered for one object per pair, but add lifecycle and billing complexity without a need for live sessions. A traditional VPS/database was rejected because it creates fixed cost and maintenance before demand exists.

### Bind device keys through a two-channel handshake

Each installation creates an ECDSA P-256 signing key. The initiator asks the Worker for a single-use invitation and explicitly sends its short reference through the existing tell sender. The receiver obtains the initiator's server-verified character identity from the tell, fetches the invitation, verifies its signature, and displays an explicit acceptance prompt. Acceptance publishes a signed proof and automatically sends one acknowledgement tell containing the invitation reference and proof digest. The initiator activates only when the acknowledgement's verified sender and fetched acceptance agree.

The initiating click is its explicit consent; the receiver's Accept is theirs. Relay state alone never establishes character identity. The prior manual shared-code handshake is removed entirely by this change, not kept as a fallback: relay-assisted pairing is the only pairing mechanism going forward, so there is no dual-mode state to keep coherent and no upgrade path to design for.

Alternatives considered: pairing solely through a URL/code would allow a copied link to claim an arbitrary character; keeping manually configured shared codes would retain the current friction without adding meaningful security once signed single-use invitations exist. Preserving the manual handshake as an offline fallback (the original plan) was rejected once relay-assisted pairing existed: it would have doubled the pairing state machine and UI surface indefinitely for a path this project no longer wants to support, and pairing (unlike catalog sync) has no legitimate offline-only use case the way manual catalog export/import still does.

### Use standard hybrid cryptography and canonical envelopes

Signing uses ECDSA P-256 with SHA-256 because it is available in supported .NET cryptography and Cloudflare WebCrypto without native libraries. Each catalog request includes an ephemeral Owner ECDH P-256 public key. The Sub generates its own ephemeral ECDH key, derives an AES-256-GCM key with HKDF-SHA-256, compresses then encrypts the complete snapshot, and signs a canonical envelope containing protocol/schema version, pair ID hash, pair epoch, sender/recipient device key IDs, request ID, monotonic snapshot ID, creation/expiry, algorithms, ciphertext digest, nonce, and ephemeral public key.

Canonical JSON encoding is specified and covered by cross-runtime vectors so C# and Worker verification cannot disagree on whitespace or property order. Private keys never enter relay requests. Local key storage uses the strongest supported OS protection available; where unavailable under Wine, the plugin stores the key only in its private configuration directory and visibly documents the limitation. Local-machine compromise is outside the relay threat boundary.

### Use capabilities plus signatures, not either one alone

Random 256-bit secrets authorize invitation, pair-mailbox, and catalog-request access; only their hashes are stored server-side. Every mutating request is also signed and binds method, normalized path, body hash, Unix time, and a 128-bit nonce. A short timestamp window and consumed-nonce table reject replay. Pair epoch and monotonic snapshot/revocation sequence prevent old valid envelopes affecting a new pairing.

A leaked URL alone is therefore insufficient to impersonate a device, while a copied signed request cannot be replayed. TLS remains required but is not the only confidentiality or authentication layer.

### Keep relay checks event-driven and low frequency

Pairing and catalog work begins with explicit UI actions and tells, so receivers fetch only after observing an authenticated lifecycle message. The inviter polls only its active invitation/request with server-directed exponential backoff and stops on completion, expiry, window close, logout, or cancellation. Revocations use the existing best-effort tell for immediate online delivery; the relay is checked at login and at most every six hours with randomized jitter to catch missed notices.

This avoids constant presence traffic. Background retries use bounded exponential backoff, respect `Retry-After`, and never retry permanent authentication/permission failures. No HTTP request occurs on the gameplay-command dispatch path.

### Enforce the four-hour catalog cooldown in both clients and service

The Owner UI disables refresh until four hours after the last server-accepted sync. The Worker atomically enforces the same rule per pair epoch and also limits device keys, network origin, payload bytes, active capabilities, and global daily work. The stricter server decision wins; changing the local clock or configuration cannot bypass it. Manual file export/import does not use or reset the relay cooldown.

The cooldown starts when the Sub accepts and uploads a valid snapshot, rather than on a malformed request, so transient failures do not impose a four-hour lockout. Concurrent requests collapse to the existing active request. A global circuit breaker rejects new pairing/catalog work before configured operational thresholds while still prioritizing panic/unpair publication and retrieval.

### Import snapshots transactionally and track their source pair

`CatalogSyncService` gains a parse-to-staging operation that validates the entire schema and produces a replacement plan without mutating configuration. Imported commands record a stable source pair ID and snapshot ID in addition to existing stable target/provenance metadata. Commit replaces only imported entries belonging to that pair, preserves manual entries, and carries forward favorites and presentation-only edits by stable identity. Configuration is saved once; failure keeps the prior snapshot.

The same staging validator is reused for manual imports where possible. Legacy imported entries without a source pair remain untouched until the user explicitly chooses to associate or reset them, avoiding an implicit destructive migration.

### Make safety teardown independent of delivery

Panic and unpair first snapshot the peer notification details, perform every existing local release/unlock/unpair step, rotate or invalidate local pair capability state, and only then attempt notification. A notification tell remains the fast path. Signed relay revocation publication is best effort and persisted in a small retry outbox that contains no commands or catalog. Retry expiry produces a visible warning but never restores pairing.

An incoming revocation is accepted only for the current pair ID/key set and a newer pair epoch or sequence. It ends local pairing and outgoing send availability but does not execute arbitrary cleanup commands supplied by the peer; local panic/unpair logic owns teardown.

### Separate protocol packages and test the trust boundaries

The repository adds a Worker package containing API schema, migrations, scheduled cleanup, local development configuration, and deployment documentation. Shared protocol constants and canonical test vectors are checked into language-neutral fixtures consumed by Worker and plugin tests. Plugin relay code is separated into transport, crypto, state machine, outbox, and UI-facing status components so HTTP responses cannot directly mutate gameplay state.

CI runs Worker unit/integration tests against local D1/R2 emulation, C# envelope vectors, pairing state-machine tests, catalog atomicity tests, rate-limit tests, and a static/contract assertion that no relay envelope or route represents gameplay commands.

## Risks / Trade-offs

- [Cloudflare or the public endpoint is unavailable] -> Pairing is unavailable until it returns (there is no manual pairing fallback); catalog sync falls back to manual file transfer; make panic local-first, show failures, and bound retries.
- [A public free endpoint attracts abuse] -> Require capabilities and signatures, layer quotas, cap bodies/active objects, configure bot/firewall rules, and trip a global circuit breaker before cost thresholds.
- [Relay metadata can correlate pseudonymous devices] -> Store no character identity, rotate pair capabilities/epochs, minimize logs, document retention, and delete inactive/revoked state.
- [Private key theft impersonates an installation] -> Use available OS protection, allow identity reset/revocation, expose key fingerprints for diagnostics, and treat local compromise as requiring re-pairing.
- [Tell acknowledgement is lost] -> Keep the invitation pending and allow a bounded resend/recheck; never activate from relay claims alone.
- [Six-hour revocation polling is not instant] -> Preserve immediate notification tells; clearly describe relay detection as eventual and always stop locally at once.
- [Schema or crypto implementation mismatch corrupts imports] -> Version envelopes, use canonical cross-runtime vectors, stage imports atomically, and retain the prior snapshot on any failure.
- [Legacy and relay imports duplicate entries] -> Track source pair and stable targets, provide an explicit association/reset path, and never silently delete legacy manual/imported data.
- [Cloudflare free limits or pricing change] -> Keep the internal API versioned, ship endpoint changes in signed plugin releases, measure aggregate quota use, and keep a hard service ceiling; users cannot redirect the plugin to an untrusted relay.

## Migration Plan

Pairing has no dual-mode period: the prior manual shared-code handshake is deleted in the same change that introduces relay-assisted pairing, not phased out later. There is no existing pairing state to preserve or auto-convert across the cutover; a config written by a pre-relay build simply has no paired peer, the same as a fresh install.

1. Add and test the Worker package locally with D1/R2 schemas, cleanup, limits, protocol vectors, and no production endpoint enabled.
2. Add plugin device identity and relay client pinned to `https://oathbound-relay-staging.oathbound.workers.dev`; catalog sync's opt-in permission and manual export/import remain available independently.
3. Deploy a staging Worker, publish its pinned base URL and server key, and run adversarial/replay/expiry/cost tests with synthetic clients.
4. Enable relay-assisted pairing (the only pairing path) and catalog sync (opt-in, with manual file transfer remaining available regardless of relay permission/availability) once staging testing passes.
5. Monitor aggregate error, throttle, request, and storage metrics; tune only within the specified four-hour user cooldown and hard service ceilings.
6. Rollback by disabling new invitation/catalog creation at the circuit breaker, allowing temporary retrieval/revocation through expiry. Pairing is unavailable for the duration (no manual fallback); catalog sync falls back to manual file transfer. Existing gameplay tells remain unaffected throughout.
