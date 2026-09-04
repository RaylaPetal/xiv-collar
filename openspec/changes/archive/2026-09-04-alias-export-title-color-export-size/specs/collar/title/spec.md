## MODIFIED Requirements

### Requirement: Owner sets Sub's title
The system SHALL let an Owner send a title command (text, and optionally color/glow/prefix-suffix) to a paired Sub who has the "title" permission enabled. The Sub's client SHALL apply the title to its own local player character. The Owner's quick-command UI for creating a title command SHALL itself offer prefix and color selection, matching the same controls the Sub's own Title alias UI already provides - the underlying capability to carry a prefix/color SHALL NOT exist only in the wire format or the applying code with no way for the Owner to actually set them.

#### Scenario: Owner sets a title
- **WHEN** an Owner sends a title command to a paired Sub with "title" permission enabled
- **THEN** the Sub's client applies the specified title to the Sub's own character

#### Scenario: Title command without permission
- **WHEN** an Owner sends a title command to a Sub who has not enabled the "title" permission
- **THEN** the Sub's client rejects the command and the title is unchanged

#### Scenario: Owner selects a prefix and color when creating a title quick command
- **WHEN** the Owner creates a new title quick command
- **THEN** the Owner can choose whether it applies as a prefix or suffix and pick a color for it, the same as the Sub's own Title alias creation form

#### Scenario: A title quick command with no color chosen still applies
- **WHEN** the Owner creates a title quick command without changing the default color
- **THEN** the title applies in that default color, exactly as today's plain titles already do
