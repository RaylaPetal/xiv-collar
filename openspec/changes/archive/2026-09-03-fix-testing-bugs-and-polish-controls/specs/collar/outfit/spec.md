## MODIFIED Requirements

### Requirement: Owner-held lock on applied outfit
The system SHALL let an Owner apply an outfit with a lock such that the Sub cannot revert or change the locked outfit through the plugin without the Owner's matching unlock key. Once unlocked - by the Owner's key, or by the Sub's own unlock alias - the system SHALL revert the outfit to Glamourer's automation-managed state rather than leaving the manually-applied design in place.

#### Scenario: Sub attempts to revert a locked outfit
- **WHEN** an Owner has applied a locked outfit to a Sub
- **AND** the Sub attempts to revert or change that outfit through the plugin without the Owner's key
- **THEN** the system blocks the change

#### Scenario: Owner unlocks the outfit
- **WHEN** an Owner sends an unlock command using the matching key for a previously locked outfit
- **THEN** the Sub's client removes the lock and reverts the outfit to Glamourer's automation-managed state

#### Scenario: Sub unlocks their own outfit
- **WHEN** a Sub triggers their own unlock alias for a currently-applied outfit
- **THEN** the Sub's client removes any lock and reverts the outfit to Glamourer's automation-managed state
