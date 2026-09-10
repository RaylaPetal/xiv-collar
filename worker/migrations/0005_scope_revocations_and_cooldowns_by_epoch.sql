-- pairIdHash is computed from just the two device keys (sorted, symmetric), so a mutual pair - two people
-- paired in both directions - shares one hash across two different pair_epoch rows. Two tables assumed
-- "one hash = one relationship" and need to be scoped by (pair_id_hash, pair_epoch) instead, or one
-- direction's activity can corrupt or silently block the other's:
--
-- revocations: sequence numbers are only unique within one epoch's own independent counter, not across
-- every epoch sharing a hash - the old PRIMARY KEY (pair_id_hash, sequence) let two unrelated directions'
-- otherwise-valid sequence numbers collide as a UNIQUE constraint failure. pair_epoch already exists as a
-- plain column; this only changes the primary key, so existing rows carry over unchanged.
CREATE TABLE revocations_new (
  pair_id_hash TEXT NOT NULL,
  sequence INTEGER NOT NULL,
  pair_epoch INTEGER NOT NULL,
  reason TEXT NOT NULL CHECK (reason IN ('unpair', 'panic')),
  issued_by_device_key_id TEXT NOT NULL,
  signature TEXT NOT NULL,
  created_at INTEGER NOT NULL,
  expires_at INTEGER NOT NULL,
  PRIMARY KEY (pair_id_hash, pair_epoch, sequence)
);
INSERT INTO revocations_new (pair_id_hash, sequence, pair_epoch, reason, issued_by_device_key_id, signature, created_at, expires_at)
  SELECT pair_id_hash, sequence, pair_epoch, reason, issued_by_device_key_id, signature, created_at, expires_at FROM revocations;
DROP TABLE revocations;
ALTER TABLE revocations_new RENAME TO revocations;
CREATE INDEX idx_revocations_expires_at ON revocations (expires_at);

-- pair_cooldowns: catalog-sync cooldown and single-active-request tracking must be per epoch too, so a
-- Sub's Owner-side pairing and Sub-side pairing with the same peer (same pair_id_hash) never share one
-- cooldown slot or block each other's catalog requests. No pair_epoch column existed before, so there is
-- no meaningful existing row to preserve per-epoch (every prior row implicitly meant "whatever pairing
-- happened to exist for this hash" - the exact ambiguity this migration removes); recreated empty.
DROP TABLE pair_cooldowns;
CREATE TABLE pair_cooldowns (
  pair_id_hash TEXT NOT NULL,
  pair_epoch INTEGER NOT NULL,
  last_accepted_sync_at INTEGER NOT NULL DEFAULT 0,
  last_snapshot_id INTEGER NOT NULL DEFAULT 0,
  active_request_id_hash TEXT,
  PRIMARY KEY (pair_id_hash, pair_epoch)
);
