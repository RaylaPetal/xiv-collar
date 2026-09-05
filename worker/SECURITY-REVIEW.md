# Relay security review

Reviewed scope: Worker API, plugin relay client, pairing/revocation lifecycle, and encrypted catalog path.

## Findings and controls

- Capability leakage: database stores capability hashes; logs redact URLs, bodies, headers, keys, and capabilities. Capabilities expire and invitation/catalog capabilities are single-use.
- Signature confusion: every signed envelope has a fixed `type` and `schemaVersion`; request signatures bind method, normalized path, canonical body digest, timestamp, and nonce. Unknown routes/types fail.
- Replay: D1 enforces unique device nonces; invitations and catalog responses consume once; pair epochs and monotonic snapshot/revocation sequences reject delayed valid messages.
- Enumeration: capability identifiers contain 256 random bits and missing/unauthorized resources return a uniform error surface without identity or existence metadata.
- Decompression bombs: ciphertext and compressed payload sizes are bounded before allocation; decompression stops at the plugin plaintext ceiling and atomic import retains the prior snapshot on failure.
- Log leakage: structured logs are aggregate allowlisted fields only; automated log tests reject sensitive key names and sample secrets.
- Origin spoofing: network-origin quotas are only an abuse layer, never authentication. Device signatures and pair membership remain authoritative when proxy headers are absent or attacker-controlled.
- Service compromise: the relay sees ciphertext, public keys, random pair identifiers, expiry, and size but has no catalog decryption key. FFXIV verified tell identity remains required before pairing activates.
- Redirect/endpoint substitution: released clients use one hardcoded HTTPS origin and reject HTTP redirects; configuration cannot redirect traffic to an attacker-controlled relay.
- Command smuggling: the API has no operational-command route or schema. Gameplay actions continue to require an active pairing and a matching server-verified FFXIV tell.
- Cost exhaustion: every `/v1` request enters a 25k/day normal or isolated 10k/day safety pool and a
  hashed-origin minute pool before authentication/body parsing. Counters saturate after rejection, and
  signed bodies are streamed through a hard byte ceiling before JSON parsing and canonicalization.

No unresolved high-severity finding was identified. Remaining operational risks are Cloudflare/account availability and local device-key compromise; the circuit breaker, manual catalog fallback, local-first panic, device reset, and fresh pairing procedures address recovery.
