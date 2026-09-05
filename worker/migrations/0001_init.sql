-- Initial relay schema. No column here stores character name, world, plaintext
-- catalog content, or private key material -- see
-- protocol/docs/threat-model.md for the field-by-field retention rationale.
-- `wrangler d1 migrations apply` tracks applied migrations in its own
-- `d1_migrations` table, so re-running this command against an already
-- migrated database is a no-op (idempotent) rather than re-executing this file.

-- Persists a device's signing public key the first time it proves possession
-- of the matching private key (self-asserted request signature), so later
-- requests referencing that device key by id alone can be verified without
-- the caller re-sending the key. No private key material is ever stored.
CREATE TABLE device_keys (
  device_key_id TEXT PRIMARY KEY,
  public_key_jwk TEXT NOT NULL,
  first_seen_at INTEGER NOT NULL
);

CREATE TABLE invitations (
  invitation_id_hash TEXT PRIMARY KEY,
  inviter_device_key_id TEXT NOT NULL,
  inviter_public_key_jwk TEXT NOT NULL,
  role TEXT NOT NULL CHECK (role IN ('owner', 'sub')),
  created_at INTEGER NOT NULL,
  expires_at INTEGER NOT NULL,
  -- Signature over the invitation's own content (everything above plus type/schemaVersion/invitationId),
  -- signed by the inviter and independently verifiable by anyone who later fetches this row -- never trust
  -- the relay operator alone for invitation authenticity (design.md: "fetches the invitation, verifies its
  -- signature").
  signature TEXT NOT NULL,
  status TEXT NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'accepted', 'consumed', 'expired')),
  accepter_device_key_id TEXT,
  accepter_public_key_jwk TEXT,
  proof_digest TEXT,
  accepter_created_at INTEGER,
  accepter_expires_at INTEGER,
  -- Signature over the acceptance's own content (accepterDeviceKeyId/PublicKey/proofDigest/created/expires),
  -- signed by the accepter -- same independent-verifiability rationale as the invitation's own signature
  -- above (design.md: "Acceptance publishes a signed proof").
  accepter_signature TEXT,
  accepted_at INTEGER,
  consumed_at INTEGER
);
CREATE INDEX idx_invitations_expires_at ON invitations (expires_at);

CREATE TABLE pairs (
  pair_id_hash TEXT NOT NULL,
  pair_epoch INTEGER NOT NULL,
  owner_device_key_id TEXT NOT NULL,
  sub_device_key_id TEXT NOT NULL,
  created_at INTEGER NOT NULL,
  revoked_at INTEGER,
  PRIMARY KEY (pair_id_hash, pair_epoch)
);
CREATE INDEX idx_pairs_pair_id_hash ON pairs (pair_id_hash);

CREATE TABLE revocations (
  pair_id_hash TEXT NOT NULL,
  sequence INTEGER NOT NULL,
  pair_epoch INTEGER NOT NULL,
  reason TEXT NOT NULL CHECK (reason IN ('unpair', 'panic')),
  issued_by_device_key_id TEXT NOT NULL,
  signature TEXT NOT NULL,
  created_at INTEGER NOT NULL,
  expires_at INTEGER NOT NULL,
  PRIMARY KEY (pair_id_hash, sequence)
);
CREATE INDEX idx_revocations_expires_at ON revocations (expires_at);

CREATE TABLE catalog_requests (
  request_id_hash TEXT PRIMARY KEY,
  pair_id_hash TEXT NOT NULL,
  pair_epoch INTEGER NOT NULL,
  requester_device_key_id TEXT NOT NULL,
  owner_ephemeral_public_key_jwk TEXT NOT NULL,
  created_at INTEGER NOT NULL,
  expires_at INTEGER NOT NULL,
  status TEXT NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'uploaded', 'consumed', 'expired'))
);
CREATE INDEX idx_catalog_requests_pair_status ON catalog_requests (pair_id_hash, status);
CREATE INDEX idx_catalog_requests_expires_at ON catalog_requests (expires_at);

CREATE TABLE catalog_objects (
  request_id_hash TEXT PRIMARY KEY REFERENCES catalog_requests (request_id_hash),
  r2_key TEXT NOT NULL,
  sender_device_key_id TEXT NOT NULL,
  recipient_device_key_id TEXT NOT NULL,
  snapshot_id INTEGER NOT NULL,
  ciphertext_digest TEXT NOT NULL,
  ciphertext_size_bytes INTEGER NOT NULL,
  nonce TEXT NOT NULL,
  sender_ephemeral_public_key_jwk TEXT NOT NULL,
  algorithm TEXT NOT NULL,
  created_at INTEGER NOT NULL,
  expires_at INTEGER NOT NULL,
  consumed_at INTEGER
);
CREATE INDEX idx_catalog_objects_expires_at ON catalog_objects (expires_at);

CREATE TABLE pair_cooldowns (
  pair_id_hash TEXT PRIMARY KEY,
  last_accepted_sync_at INTEGER NOT NULL DEFAULT 0,
  last_snapshot_id INTEGER NOT NULL DEFAULT 0,
  active_request_id_hash TEXT
);

CREATE TABLE nonces (
  device_key_id TEXT NOT NULL,
  nonce TEXT NOT NULL,
  seen_at INTEGER NOT NULL,
  PRIMARY KEY (device_key_id, nonce)
);
CREATE INDEX idx_nonces_seen_at ON nonces (seen_at);

CREATE TABLE quota_counters (
  scope TEXT NOT NULL,
  window_start INTEGER NOT NULL,
  count INTEGER NOT NULL DEFAULT 0,
  bytes INTEGER NOT NULL DEFAULT 0,
  PRIMARY KEY (scope, window_start)
);
CREATE INDEX idx_quota_counters_window_start ON quota_counters (window_start);
