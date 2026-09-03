## Purpose

Lets a paired Owner set or clear a Sub's in-game nameplate title, applied locally on the Sub's own client so existing sync tooling can propagate it.

## ADDED Requirements

### Requirement: Owner sets Sub's title
The system SHALL let an Owner send a title command (text, and optionally color/glow/prefix-suffix) to a paired Sub who has the "title" permission enabled. The Sub's client SHALL apply the title to its own local player character.

#### Scenario: Owner sets a title
- **WHEN** an Owner sends a title command to a paired Sub with "title" permission enabled
- **THEN** the Sub's client applies the specified title to the Sub's own character

#### Scenario: Title command without permission
- **WHEN** an Owner sends a title command to a Sub who has not enabled the "title" permission
- **THEN** the Sub's client rejects the command and the title is unchanged

### Requirement: Owner clears Sub's title
The system SHALL let an Owner send a command to clear a paired Sub's currently applied title.

#### Scenario: Owner clears a title
- **WHEN** an Owner sends a clear-title command to a paired Sub with "title" permission enabled
- **THEN** the Sub's client removes the currently applied title

### Requirement: Title reverts on panic or unpair
The system SHALL clear any Owner-applied title when the Sub triggers the panic action or when the pairing ends.

#### Scenario: Panic clears title
- **WHEN** a Sub with an Owner-applied title triggers the panic action
- **THEN** the title is cleared as part of the panic sequence
