## ADDED Requirements

### Requirement: Saved-entry controls follow one editing pattern
Every module that displays saved aliases, quick commands, restraint definitions, or Custom Triggers SHALL expose consistent Edit and Remove controls, use a focused editor when the full form cannot fit cleanly inline, and retain Send, Copy, favorite, and expansion behavior appropriate to that entry. Editor save/cancel behavior and validation feedback SHALL be consistent across Owner and Sub surfaces.

#### Scenario: User scans saved entries across modules
- **WHEN** the user views saved entries in different command categories
- **THEN** Edit and Remove are discoverable in consistent positions and use consistent labels or icons

#### Scenario: Complex entry opens for editing
- **WHEN** an entry has multiple fields or nested actions that would crowd its list row
- **THEN** Edit opens a focused modal or popup containing the complete validated form while the underlying list remains stable

### Requirement: Command summaries prioritize human meaning
Saved-entry rows SHALL use a shared presentation vocabulary for category names, action names, capitalization, icons, badges, target labels, active/stale states, and secondary detail. Machine identifiers and wire syntax SHALL be hidden from ordinary views and available only in an explicitly technical diagnostic or copy-command context.

#### Scenario: User compares different command categories
- **WHEN** Title, Gesture, Moodle, Restraint, and Custom Trigger entries appear in their respective lists
- **THEN** their rows share a coherent hierarchy and visual language while retaining category-specific information

#### Scenario: Entry is stale or invalid
- **WHEN** a saved entry no longer resolves or lacks required configuration
- **THEN** the row shows a friendly visible warning and disables unsafe actions without replacing the whole row with raw protocol text

### Requirement: Release commands use fixed vocabulary
The Sub-facing Wardrobe and Restraints tabs SHALL present release commands as fixed protocol actions rather than editable aliases. Wardrobe release SHALL remain `unlock`, and Owner restraint force-release SHALL remain `restraint unlock`; the UI SHALL explain these exact forms while individual restraint aliases continue toggling their associated device.

#### Scenario: Sub checks how an Owner releases locks
- **WHEN** the Sub views Wardrobe or Restraints configuration
- **THEN** the tab shows the exact fixed release command and does not offer an alias editor for it
