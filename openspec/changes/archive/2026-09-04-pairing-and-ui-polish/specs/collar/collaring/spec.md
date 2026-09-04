## MODIFIED Requirements

### Requirement: Sub configures their own collar item
The system SHALL let a Sub designate a collar by picking an item from a searchable item picker locked to the Neck slot, rather than by equipping the item first and reading it back from live game state, and SHALL NOT require or accept a manually-entered item identifier as an alternative. The picker SHALL let the Sub choose from every item valid for the Neck slot, not only items the Sub currently owns or has equipped. The system SHALL let the Sub clear or replace this configuration at any time while no collar lock from this system is currently active.

#### Scenario: Sub captures their currently-equipped Neck item
- **WHEN** a Sub picks an item from the Neck-locked picker and saves it as their collar
- **THEN** that item is saved as the Sub's configured collar item, whether or not it is currently equipped

#### Scenario: Sub replaces the configured collar while unlocked
- **WHEN** a Sub has a configured collar item and no active collar lock, and picks a different item from the Neck-locked picker
- **THEN** the newly picked item replaces the previous configuration
