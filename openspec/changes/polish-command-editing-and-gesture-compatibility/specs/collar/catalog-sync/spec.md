## ADDED Requirements

### Requirement: Saved Owner quick commands are editable without losing provenance
The system SHALL let an Owner edit any saved quick command's user-editable label and category-specific command values in place. Imported entries SHALL retain their import provenance and stable catalog identity after presentation-only edits; edits that deliberately select a different target SHALL update that identity consistently. Saving SHALL revalidate duplicates and command length, while cancelling SHALL make no persisted change.

#### Scenario: Owner renames an imported command label
- **WHEN** the Owner edits only the visible label of an imported quick command
- **THEN** the label changes while its imported source, stable target identity, favorite state, and command behavior remain intact

#### Scenario: Owner changes a command target
- **WHEN** the Owner selects a different valid category target and saves
- **THEN** the command text, readable metadata, and stable identity are updated together so the row and sent command cannot disagree

#### Scenario: Edit would create a duplicate or overlong command
- **WHEN** the edited result duplicates an existing command under that category's matching rules or exceeds the safe chat length
- **THEN** Save is refused with an explanation and the original entry remains unchanged
