## MODIFIED Requirements

### Requirement: Owner-held lock on applied outfit
The system SHALL let an Owner apply an outfit with a lock such that the Sub cannot revert or change the locked slots through the plugin without the matching release action. The lock SHALL cover only the equipment slots that the applied design itself changes, enforced by this system's own per-slot lock tracking (see `collar/slot-locking`), not by Glamourer's own actor-wide lock. Every slot the design does not change SHALL remain exactly as free to edit as if no lock were active.

#### Scenario: Sub attempts to revert a locked outfit
- **WHEN** an Owner has applied a locked outfit to a Sub
- **AND** the Sub attempts to revert or change one of that design's locked slots through the plugin without the matching release action
- **THEN** the system blocks the change

#### Scenario: Locking an outfit leaves unrelated slots editable
- **WHEN** an Owner has applied a locked outfit whose design changes only some equipment slots
- **THEN** the Sub can freely change any slot the design did not touch, through Glamourer, another tool, or this plugin's own aliases

#### Scenario: Owner unlocks the outfit
- **WHEN** an Owner sends an unlock command for a previously locked outfit
- **THEN** the Sub's client releases the lock on that design's slots and reverts them to Glamourer's automation-managed state, and the Sub may change those slots freely again

### Requirement: Locked outfit released on panic or unpair
The system SHALL release every slot of an Owner-held outfit lock, regardless of who currently holds the lock, when the Sub triggers the panic action or when the pairing ends.

#### Scenario: Panic releases a locked outfit
- **WHEN** a Sub with a locked outfit triggers the panic action
- **THEN** every slot of the outfit lock is released as part of the panic sequence
