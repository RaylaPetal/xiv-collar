## ADDED Requirements

### Requirement: Temporary gesture activation is reverted after use
The system SHALL let a Sub manually revert a played gesture's temporary Penumbra mod activation back to the mod's saved settings at any time via a dedicated control, and SHALL automatically revert it after a period of inactivity following the last gesture play, so a temporary activation does not persist indefinitely.

#### Scenario: Sub manually resets an active temporary gesture activation
- **WHEN** a gesture has temporarily activated a mod's settings
- **AND** the Sub uses the reset control
- **THEN** the mod's temporary settings are reverted and its saved settings apply again

#### Scenario: Temporary gesture activation times out
- **WHEN** a gesture has temporarily activated a mod's settings
- **AND** no further gesture is played for approximately 30 seconds
- **THEN** the temporary settings are automatically reverted and the mod's saved settings apply again

#### Scenario: New gesture play resets the timeout
- **WHEN** a temporary gesture activation is pending automatic reversion
- **AND** the Sub plays another gesture before the timeout elapses
- **THEN** the timeout restarts from the new play
