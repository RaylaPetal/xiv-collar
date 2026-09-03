# collar/collaring Specification

## Purpose

Lets a Sub designate a single Neck-slot item as their collar, applied and locked automatically the moment they accept a pairing, as the persistent marker of an active contract rather than just another swappable outfit alias.

## Requirements

### Requirement: Sub configures their own collar item
The system SHALL let a Sub designate a collar by capturing whichever item is currently equipped in their own Neck slot, and SHALL NOT require or accept a manually-entered item identifier as an alternative. The system SHALL let the Sub clear or replace this configuration at any time while no collar lock from this system is currently active.

#### Scenario: Sub captures their currently-equipped Neck item
- **WHEN** a Sub has an item equipped in their Neck slot and captures it as their collar
- **THEN** that item is saved as the Sub's configured collar item

#### Scenario: Sub replaces the configured collar while unlocked
- **WHEN** a Sub has a configured collar item and no active collar lock, and captures a different currently-equipped Neck item
- **THEN** the newly captured item replaces the previous configuration

### Requirement: Collar permission gates auto-apply
The system SHALL NOT apply or lock a Sub's configured collar unless the Sub has separately enabled a "Collar" permission, independent of Title/Outfit/Gesture/Follow. Configuring a collar item alone SHALL NOT be sufficient for it to ever be applied.

#### Scenario: Collar configured but permission disabled
- **WHEN** a Sub has a configured collar item and the "Collar" permission disabled
- **THEN** accepting a pairing request does not apply or lock the collar

### Requirement: Collar applied and locked on pairing acceptance
The system SHALL apply the Sub's configured collar item to the Sub's own Neck slot and lock it, as part of the Sub accepting a pairing request, when the Sub has both a configured collar item and the "Collar" permission enabled. The lock SHALL use a key the Sub's client generates itself, never one supplied by the Owner.

#### Scenario: Collar applied at acceptance
- **WHEN** a Sub with a configured collar item and "Collar" permission enabled accepts a pending pairing request
- **THEN** that item is applied to the Sub's Neck slot and locked, using a key generated locally

### Requirement: Collar lock resists casual removal but never panic
The system SHALL refuse to remove or change a locked collar through the plugin's own alias/UI paths without the matching release action, exactly as it does for a locked outfit. The system SHALL always release a locked collar when the Sub triggers the panic action, with no exception and regardless of any other state.

#### Scenario: Sub's own alias-triggered action cannot remove a locked collar
- **WHEN** a locked collar is active
- **AND** the Sub's client would otherwise change the Neck slot through a normal alias-triggered action
- **THEN** the system blocks the change

#### Scenario: Panic always releases the collar
- **WHEN** a Sub with a locked collar triggers the panic action
- **THEN** the collar lock is released as part of the panic sequence, unconditionally

### Requirement: Owner can release the collar without panic
The system SHALL let a paired Owner send a dedicated release command that unlocks a Sub's collar using the key that locked it, without requiring the Sub to trigger panic.

#### Scenario: Owner releases the collar
- **WHEN** an Owner sends the collar release command to a Sub with an active collar lock
- **THEN** the Sub's client releases the lock using the key that applied it

### Requirement: Owner can (re-)apply the collar directly
The system SHALL let a paired Owner with the Sub's "Collar" permission enabled send a dedicated command that applies and locks the Sub's configured collar item, independent of pairing acceptance, when a collar item is configured. This command SHALL take no item argument - it applies whichever item the Sub has configured.

#### Scenario: Owner re-locks a previously released collar
- **WHEN** an Owner sends the collar lock command to a Sub whose collar was previously released (via the Owner's release command) and who still has a collar item configured with "Collar" permission enabled
- **THEN** the Sub's client re-applies and locks that same item

#### Scenario: Owner applies the collar for the first time outside pairing acceptance
- **WHEN** an Owner sends the collar lock command to a Sub who has a collar item configured and "Collar" permission enabled, but whose collar was never applied at pairing acceptance
- **THEN** the Sub's client applies and locks that item

#### Scenario: Collar lock command without a configured item
- **WHEN** an Owner sends the collar lock command to a Sub with no collar item configured
- **THEN** the Sub's client takes no action
