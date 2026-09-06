-- catalog_requests never stored the request envelope's own signature, so fetchCatalogRequest could never
-- return one. The Sub's self-verification of that signature (protocol parity with invitations, so the Sub
-- doesn't have to trust the relay's word alone about who requested the sync) always failed as a result,
-- silently blocking every catalog upload. Nullable/backward-compatible for rows created before this column
-- existed, same pattern as 0002's trigger_phrase columns.
ALTER TABLE catalog_requests ADD COLUMN signature TEXT;
