## ADDED Requirements

### Requirement: Owner-side restraint quick commands
The system SHALL let the Owner maintain a saved list of restraint device quick commands (one per tagged device name), each sendable to the paired Sub as a `restraint lock <name>` command with a single action, the same quick-command pattern Outfit/Gesture/Moodles already provide.

#### Scenario: Owner sends a saved restraint quick command
- **WHEN** the Owner has a saved restraint quick command for a device name
- **THEN** triggering it sends the same `restraint lock <name>` command a manually typed override would send

### Requirement: Restraint device catalog export by name
The system SHALL let the Sub export their currently-tagged restraint device names as plain text, the same way Wardrobe and Moodles export their catalog names, so an Owner can build restraint quick commands from a Sub-provided name list.

#### Scenario: Sub exports tagged device names
- **WHEN** the Sub has one or more restraint devices tagged
- **THEN** the exported text contains each tagged device's display name once
