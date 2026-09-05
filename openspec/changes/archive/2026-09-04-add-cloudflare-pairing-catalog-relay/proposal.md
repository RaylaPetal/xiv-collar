## Why

Pairing and catalog exchange currently depend on manually copied codes, FFXIV tells, downloaded text files, and an out-of-band transfer such as Discord. A small Cloudflare relay can remove the file handoff and keep both peers' pairing lifecycle synchronized while preserving tells as the only transport for gameplay commands.

## What Changes

- Add a Cloudflare Workers-based relay with temporary storage, strict expiry, payload limits, abuse controls, and a free-tier-first operating profile.
- Give each installation a persistent cryptographic device identity and use signed, single-use invitations to establish a pairing without relay accounts or permanent character records.
- Require explicit acceptance on both clients before a pairing becomes active, bind the accepted peer device key to the paired character identity, and retain the existing in-game sender checks for commands.
- Synchronize unpair and panic revocation through the relay so the other online client releases the relationship promptly, while panic remains locally effective even when Cloudflare is unavailable.
- Let an Owner request a fresh catalog snapshot no more than once per configurable multi-hour cooldown; the paired Sub validates the request, exports locally, encrypts the snapshot end-to-end, and uploads it for automatic Owner retrieval and atomic import.
- Replace the existing manual shared-code pairing handshake entirely with relay-assisted pairing (no manual pairing fallback); preserve manual catalog file export/import as an offline fallback for catalog sync specifically.
- Add visible consent, sync status, expiry, failure, and last-success information without exposing opaque tokens or cryptographic material in normal UI.
- Explicitly exclude restraint, gesture, outfit, title, Moodle, follow, collar, and custom-trigger command delivery from the relay; those commands continue through authenticated FFXIV tells.

## Capabilities

### New Capabilities

- `collar/relay-service`: Defines the temporary Cloudflare relay API, cryptographic envelopes, lifecycle, quotas, abuse prevention, privacy guarantees, and operational limits.

### Modified Capabilities

- `collar/pairing`: Replaces the manual shared-code handshake with relay-assisted invitations, device-key binding, two-sided acceptance, and synchronized unpair/revocation; pairing has no offline fallback.
- `collar/catalog-sync`: Adds permission-gated, end-to-end encrypted catalog requests, automatic snapshot import, cooldowns, and manual fallback.
- `collar/chat-transport`: Keeps operational commands on FFXIV tells and defines the narrow relationship between verified character senders and relay-bound device identities.
- `collar/ui-organization`: Adds pairing and catalog-relay status, consent, cooldown, failure, and recovery controls to the existing surfaces.

## Impact

The plugin gains an HTTP relay client, local device-key and relay-state persistence, cryptographic envelope handling, background polling with bounded backoff, atomic catalog replacement, and new pairing/sync UI. A separately deployable Cloudflare Worker and infrastructure configuration are added, backed by expiring serverless storage and deployment/monitoring documentation. Existing tell command parsing and enforcement remain authoritative. Manual catalog file export/import remains compatible; manual pairing does not - it is removed in this same change.
