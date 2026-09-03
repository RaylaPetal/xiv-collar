## MODIFIED Requirements

### Requirement: Collar applied and locked on pairing acceptance
The system SHALL apply the Sub's configured collar item to the Sub's own Neck slot and lock that slot, as part of the Sub accepting a pairing request, when the Sub has both a configured collar item and the "Collar" permission enabled. The lock SHALL be enforced by this system's own per-slot lock tracking (see `collar/slot-locking`), not by Glamourer's own actor-wide lock, and SHALL cover only the Neck slot.

#### Scenario: Collar applied at acceptance
- **WHEN** a Sub with a configured collar item and "Collar" permission enabled accepts a pending pairing request
- **THEN** that item is applied to the Sub's Neck slot and the Neck slot is locked

### Requirement: Collar lock resists casual removal but never panic, and locks only the Neck slot
The system SHALL refuse to remove or change a locked collar's Neck slot through the plugin's own alias/UI paths without the matching release action. The system SHALL NOT restrict any slot other than Neck while the collar is locked - every other slot remains exactly as free to edit as if the collar were not locked. The system SHALL always release the collar's Neck-slot lock when the Sub triggers the panic action, with no exception and regardless of any other state.

#### Scenario: Sub's own alias-triggered action cannot remove a locked collar
- **WHEN** a locked collar is active
- **AND** the Sub's client would otherwise change the Neck slot through a normal alias-triggered action
- **THEN** the system blocks the change

#### Scenario: Locking the collar leaves every other slot editable
- **WHEN** a collar lock is active on the Neck slot
- **THEN** the Sub can freely change any other equipment slot through Glamourer, another tool, or this plugin's own aliases

#### Scenario: Panic always releases the collar
- **WHEN** a Sub with a locked collar triggers the panic action
- **THEN** the collar's Neck-slot lock is released as part of the panic sequence, unconditionally

### Requirement: Owner can release the collar without panic
The system SHALL let a paired Owner send a dedicated release command that releases the lock on a Sub's collar's Neck slot, without requiring the Sub to trigger panic. Releasing the lock SHALL revert the Neck slot to Glamourer's automation-managed state and SHALL NOT affect any other slot.

#### Scenario: Owner releases the collar
- **WHEN** an Owner sends the collar release command to a Sub with an active collar lock
- **THEN** the Sub's client releases the Neck-slot lock and the Neck slot reverts to Glamourer's automation-managed state, with every other slot unaffected
