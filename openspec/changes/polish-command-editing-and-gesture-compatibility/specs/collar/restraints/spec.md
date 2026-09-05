## ADDED Requirements

### Requirement: Saved restraint definitions and quick commands are editable
The system SHALL let a Sub edit a captured restraint device and let an Owner edit a saved restraint quick command or ad-hoc restraint definition in place. Editable values SHALL include its friendly name or label, equipment target where applicable, enabled rules, pose choice, and bound-animation choices. A bound-animation choice SHALL be valid when it identifies either a playable triggered animation or a triggerless idle animation whose Penumbra mod/options can be enabled without issuing a gesture. Saving SHALL validate required animation identity and update the command payload consistently; cancelling SHALL leave the saved definition unchanged.

#### Scenario: Owner revises rules on a saved restraint command
- **WHEN** the Owner edits a saved restraint command, removes Gagged, adds Arms Cuffed, chooses a valid imported Sub animation, and saves
- **THEN** its displayed summary and next sent payload both reflect exactly the revised rules

#### Scenario: Sub edits a captured restraint device
- **WHEN** the Sub changes a captured device's friendly name, item, or rules and saves a valid edit
- **THEN** the existing device is updated without requiring deletion and its aliases continue referencing the same device identity

#### Scenario: Bound animation is missing
- **WHEN** an edit enables a bound-animation rule without a valid triggered or triggerless animation selection
- **THEN** Save is unavailable and the editor marks the missing selection

#### Scenario: User selects a triggerless idle animation
- **WHEN** the user edits an Arms Cuffed, Legs Cuffed, or Full Body Cuffed rule and selects a cataloged idle animation with no gesture or pose trigger
- **THEN** the selection is accepted and retained as an enable-only bound animation rather than requiring an unrelated trigger

### Requirement: Restraint presentation uses friendly rule names and hierarchy
The system SHALL render restraint devices and command summaries with consistently capitalized friendly rule names, readable animation labels, and visual grouping that separates the device from its rules. Normal rows SHALL NOT expose enum casing, raw `rules:` syntax, opaque animation IDs, or an undifferentiated comma-joined list.

#### Scenario: Restraint has several rules
- **WHEN** a restraint contains Body Cuffed, Gagged, and another rule
- **THEN** the device name is visually primary and each rule is presented as a distinct readable subordinate item or compact badge

#### Scenario: Bound animation details are available
- **WHEN** Arms Cuffed, Legs Cuffed, or Full Body Cuffed has a selected animation
- **THEN** the UI shows a concise friendly rule label by default and makes the readable animation detail available without showing its opaque identity

### Requirement: Bound restraints support triggerless activation and visible release
The system SHALL allow Arms Cuffed, Legs Cuffed, and Full Body Cuffed rules to use a selected animation that has no detected slash-emote or pose trigger. Applying such a rule SHALL temporarily enable the selected Penumbra mod and complete option state, redraw the Sub, and issue no gesture or pose command. Releasing any bound-animation rule SHALL remove its temporary Penumbra settings and redraw the Sub after removal so the restored settings take visible effect. A triggered selection SHALL retain its existing redraw-then-play behavior.

#### Scenario: Triggerless idle restraint engages
- **WHEN** a bound restraint selects a valid triggerless idle animation and is applied
- **THEN** its Penumbra mod/options are temporarily enabled and the Sub is redrawn without entering ground-sit or issuing any other gesture command

#### Scenario: Triggered restraint still plays its trigger
- **WHEN** a bound restraint selects a valid animation with a detected pose or slash-emote trigger
- **THEN** the mod/options are enabled, the Sub is redrawn, and the selected trigger plays through the established delayed playback path

#### Scenario: Bound restraint unlocks
- **WHEN** a triggered or triggerless bound restraint is released
- **THEN** its temporary Penumbra settings are removed and the Sub is redrawn afterward so the prior animation state is visibly restored

#### Scenario: Release redraw fails
- **WHEN** temporary settings are removed but the post-removal redraw fails
- **THEN** restriction bookkeeping is still released and the local diagnostic reports that visual restoration may require a manual redraw
