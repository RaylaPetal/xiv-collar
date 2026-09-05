## ADDED Requirements

### Requirement: Paired Owner can request an encrypted catalog snapshot
An actively paired Owner SHALL be able to explicitly request a catalog refresh through a one-use relay request no more than once per pair every four hours. The Sub SHALL process only a request signed by its currently paired Owner device, SHALL require a local opt-in permission for remote catalog synchronization, and SHALL expose request, upload, retrieval, and import status to both sides.

#### Scenario: Permitted refresh succeeds
- **WHEN** the Owner requests a refresh after cooldown and the paired Sub has enabled catalog synchronization
- **THEN** the Sub builds a current snapshot locally, uploads an encrypted envelope, and the Owner automatically retrieves, validates, and imports it

#### Scenario: Sub has not opted in
- **WHEN** a valid paired Owner requests a refresh but the Sub has disabled catalog synchronization
- **THEN** no catalog is generated or uploaded and the Owner receives a permission-denied status without learning catalog contents

#### Scenario: Request is inside cooldown
- **WHEN** the Owner requests another refresh less than four hours after the last accepted synchronization
- **THEN** no request is created and the UI shows when the next refresh becomes available

### Requirement: Catalog content is end-to-end encrypted and authenticated
The Sub SHALL compress and encrypt the catalog to the paired Owner device key before upload and SHALL authenticate its schema, pair epoch, sender device, recipient device, creation time, expiry, monotonic snapshot identifier, plaintext digest, and ciphertext. The relay SHALL never receive the plaintext or decryption key.

#### Scenario: Relay data is inspected
- **WHEN** stored catalog data is read outside the intended Owner client
- **THEN** it reveals only bounded ciphertext and non-sensitive routing metadata

#### Scenario: Catalog envelope is altered or addressed to another device
- **WHEN** authentication, sender, recipient, pair epoch, digest, or expiry validation fails
- **THEN** the Owner rejects the entire snapshot and leaves existing imports unchanged

### Requirement: Automatic import replaces one peer snapshot atomically
After successful validation, the Owner SHALL atomically replace entries previously imported from that paired Sub while preserving manual entries and favorites or local presentation edits that can be matched by stable identity. Entries removed from the new Sub snapshot SHALL be removed from that Sub's imported set. Parsing or persistence failure SHALL leave the prior snapshot intact.

#### Scenario: Updated snapshot changes the catalog
- **WHEN** a valid newer snapshot adds, changes, and removes Sub entries
- **THEN** the Owner sees the new imported set without duplicates while matching favorites and manual entries remain intact

#### Scenario: Import fails halfway
- **WHEN** any category in a retrieved snapshot fails validation or durable save
- **THEN** none of the new snapshot becomes active and the prior imported commands remain usable

#### Scenario: Older snapshot arrives late
- **WHEN** a snapshot identifier is not newer than the last successfully imported snapshot for that pair epoch
- **THEN** the Owner ignores it as stale

### Requirement: Manual catalog transfer remains supported
Manual file export and import SHALL remain available and SHALL use the same schema validation and atomic import rules where applicable. Relay outage, cooldown, or disabled synchronization SHALL NOT prevent manual export or import.

#### Scenario: User chooses offline transfer
- **WHEN** either peer cannot or does not want to use Cloudflare synchronization
- **THEN** they can continue using the existing file workflow without enabling relay catalog permission
