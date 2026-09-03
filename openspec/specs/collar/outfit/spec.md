# collar/outfit Specification

## Purpose

Lets a paired Owner set a Sub's outfit and optionally lock it so only the Owner can revert it, applied locally on the Sub's own client so existing sync tooling can propagate it.

## Requirements

### Requirement: Owner sets Sub's outfit
The system SHALL let an Owner send an outfit command (a single gear slot, or a full saved state) to a paired Sub who has the "outfit" permission enabled. The Sub's client SHALL apply the change to its own local player character.

#### Scenario: Owner sets a full outfit state
- **WHEN** an Owner sends a full outfit-state command to a paired Sub with "outfit" permission enabled
- **THEN** the Sub's client applies that state to the Sub's own character

#### Scenario: Outfit command without permission
- **WHEN** an Owner sends an outfit command to a Sub who has not enabled the "outfit" permission
- **THEN** the Sub's client rejects the command and the outfit is unchanged

### Requirement: Owner-held lock on applied outfit
The system SHALL let an Owner apply an outfit with a lock such that the Sub cannot revert or change the locked outfit through the plugin without the Owner's matching unlock key.

#### Scenario: Sub attempts to revert a locked outfit
- **WHEN** an Owner has applied a locked outfit to a Sub
- **AND** the Sub attempts to revert or change that outfit through the plugin without the Owner's key
- **THEN** the system blocks the change

#### Scenario: Owner unlocks the outfit
- **WHEN** an Owner sends an unlock command using the matching key for a previously locked outfit
- **THEN** the Sub's client removes the lock and the Sub may change the outfit freely

### Requirement: Locked outfit released on panic or unpair
The system SHALL release any Owner-held outfit lock, independent of the Owner's key, when the Sub triggers the panic action or when the pairing ends.

#### Scenario: Panic releases a locked outfit
- **WHEN** a Sub with a locked outfit triggers the panic action
- **THEN** the outfit lock is released as part of the panic sequence, without requiring the Owner's key

### Requirement: Empty wardrobe scope includes all designs
The wardrobe scanner SHALL treat an empty design-folder scope as an explicit request to include every locally saved Glamourer design. When one or more folder scopes are configured, the scanner SHALL include only designs within those scopes.

#### Scenario: Wardrobe scope is empty
- **WHEN** the Sub rescans wardrobe designs with no folder scope configured
- **THEN** every locally saved Glamourer design is available in the wardrobe catalog

#### Scenario: Wardrobe scope is configured
- **WHEN** the Sub rescans with one or more folder scopes configured
- **THEN** only designs matching those folder scopes are available in the wardrobe catalog
