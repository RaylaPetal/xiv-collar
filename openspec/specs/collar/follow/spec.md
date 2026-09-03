# collar/follow Specification

## Purpose

Lets a paired Owner engage a movement lock ("leash") on a Sub who has separately opted into this higher-risk permission, blocking the Sub's own movement input until released.

## Requirements

### Requirement: Separate opt-in permission for movement lock
The system SHALL gate all follow/movement-lock commands behind a permission distinct from outfit/gesture/title, and SHALL require the Sub to explicitly enable it before any such command can apply.

#### Scenario: Movement-lock command without the dedicated permission
- **WHEN** an Owner sends a follow/movement-lock command to a Sub who has not separately enabled that permission
- **THEN** the Sub's client rejects the command, even if other categories are enabled

### Requirement: Movement input blocked while locked
The system SHALL, once a movement lock is engaged on a consenting Sub, cause the Sub's movement input (forward/back/strafe/turn) to have no effect on the character until the lock is released.

#### Scenario: Sub presses a movement key while locked
- **WHEN** a movement lock is active on a Sub's client
- **AND** the Sub presses a movement key
- **THEN** the character does not move as a result of that input

### Requirement: Auto-unfollow suppressed while locked
The system SHALL prevent movement input from cancelling an active follow/auto-move while the movement lock is engaged, so a Sub's incidental key press does not break the leash.

#### Scenario: Sub nudges a key during an active leash
- **WHEN** a Sub is being auto-followed/moved under an active movement lock
- **AND** the Sub presses a movement key
- **THEN** the follow/auto-move continues uninterrupted

### Requirement: Movement lock releases on panic, unpair, or Owner release
The system SHALL release any active movement lock immediately when the Sub triggers the panic action, when the pairing ends, or when the Owner sends a release command, restoring normal movement input.

#### Scenario: Panic releases the leash
- **WHEN** a Sub under an active movement lock triggers the panic action
- **THEN** the movement lock is released and the Sub's movement input functions normally again

#### Scenario: Owner releases the leash
- **WHEN** an Owner sends a release command for an active movement lock
- **THEN** the Sub's client releases the lock and restores normal movement input

### Requirement: Leash triggers live with collar controls
The Sub SHALL configure the movement-lock engage and release trigger words from the main Collar module rather than Settings. New installations and configurations that still use the prior untouched defaults SHALL use `leash` to engage and `unleash` to release; customized trigger words SHALL be preserved.

#### Scenario: New configuration uses leash defaults
- **WHEN** a user starts with no prior customized movement-lock triggers
- **THEN** the Collar module shows `leash` as the engage trigger and `unleash` as the release trigger

#### Scenario: Prior defaults are migrated
- **WHEN** an existing configuration still contains the untouched `leash-on` and `leash-off` pair
- **THEN** it is migrated to `leash` and `unleash` without user intervention

#### Scenario: Customized triggers are preserved
- **WHEN** either saved movement-lock trigger differs from its prior untouched default
- **THEN** the saved trigger values remain unchanged during migration

#### Scenario: User edits leash triggers
- **WHEN** the user changes either leash trigger in the Collar module
- **THEN** the new value is saved and used for subsequent paired Owner commands

#### Scenario: User opens Settings
- **WHEN** leash trigger configuration is available in the Collar module
- **THEN** Settings does not show duplicate leash trigger inputs
