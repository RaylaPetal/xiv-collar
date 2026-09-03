## ADDED Requirements

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
