-- Optional for backward compatibility with invitations already created by a v1 client.
ALTER TABLE invitations ADD COLUMN trigger_phrase TEXT;
ALTER TABLE invitations ADD COLUMN accepter_role TEXT CHECK (accepter_role IN ('owner', 'sub'));
ALTER TABLE invitations ADD COLUMN accepter_trigger_phrase TEXT;
