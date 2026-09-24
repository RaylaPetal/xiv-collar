-- collar/catalog-sync automatic sync: one mailbox per (pair_id_hash, pair_epoch) - scoped by epoch for the
-- same mutual-pair reason as 0005. The Owner publishes a signed receive key (stored verbatim so the Sub can
-- verify the Owner's own signature, not trust the relay); the Sub leaves at most one encrypted catalog-push
-- snapshot here, replacing any earlier one; the Owner consumes it and atomically rotates to a new key.
--
-- snapshot_envelope is the full catalog-push envelope as the Worker validated it (JSON), rebuilt from checked
-- fields rather than stored from the raw body. snapshot_r2_key embeds the snapshot id, so a consume racing an
-- upload can never pair one snapshot's envelope with another's ciphertext. last_upload_at survives a consume:
-- it drives the per-pair minimum upload interval and tells the Owner whether the Sub has ever published.
-- last_consumed_snapshot_id is the delivery receipt the Sub checks: if its last push is neither waiting nor
-- consumed (the Owner reset its key, or the snapshot expired unread), the Sub publishes it again.
CREATE TABLE catalog_mailboxes (
  pair_id_hash TEXT NOT NULL,
  pair_epoch INTEGER NOT NULL,
  receive_key_id TEXT NOT NULL,
  receive_key_envelope TEXT NOT NULL,
  key_published_at INTEGER NOT NULL,
  last_upload_at INTEGER,
  snapshot_id INTEGER,
  snapshot_r2_key TEXT,
  snapshot_envelope TEXT,
  snapshot_created_at INTEGER,
  snapshot_expires_at INTEGER,
  last_consumed_snapshot_id INTEGER,
  PRIMARY KEY (pair_id_hash, pair_epoch)
);
CREATE INDEX idx_catalog_mailboxes_snapshot_expires_at ON catalog_mailboxes (snapshot_expires_at);
