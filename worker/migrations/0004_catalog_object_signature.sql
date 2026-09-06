-- Same bug as 0003, one layer deeper: catalog_objects never stored the *response* envelope's own signature
-- either, so consumeCatalogResponse could never return one and the Owner's self-verification of the Sub's
-- uploaded snapshot always failed ("Snapshot signature did not verify against the paired peer's key -
-- ignored."). Nullable/backward-compatible for rows created before this column existed.
ALTER TABLE catalog_objects ADD COLUMN signature TEXT;
