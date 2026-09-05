## MODIFIED Requirements

### Requirement: Forced pose rule locks the Sub into a pose and blocks movement
When a device with a forced-pose rule is applied, the system SHALL place the Sub into the configured pose and continuously suppress all manual movement paths for the full duration. If the pose cannot be entered or complete movement enforcement is unavailable, the whole device application SHALL fail and roll back without reporting success.

#### Scenario: Forced pose engages and blocks every movement path
- **WHEN** a device with a forced-pose rule targeting ground-sit is applied
- **THEN** the Sub enters ground-sit and keyboard, controller, mouse, and autorun movement have no effect while it remains applied

#### Scenario: Forced pose engages and blocks movement
- **WHEN** a device with a forced-pose rule targeting ground-sit is applied
- **THEN** the Sub enters ground-sit and movement input has no effect while the device remains applied

#### Scenario: Forced pose cannot be enforced
- **WHEN** the configured pose or required movement enforcement is unavailable
- **THEN** device application fails atomically and reports the failed capability locally

#### Scenario: Releasing the device restores movement
- **WHEN** a device with an active forced-pose rule is released
- **THEN** movement is no longer suppressed by that rule

### Requirement: Walk-only rule forces walking without blocking movement
When a device with a walk-only rule is applied, the system SHALL continuously force both normal and automove locomotion into walking and SHALL reject sprint or other supported actions that would bypass walking, without suppressing directional input. If walking cannot be enforced, device application SHALL fail without reporting success.

#### Scenario: Running is continuously suppressed
- **WHEN** walk-only is active and the Sub or game attempts to restore running or sprinting
- **THEN** the character remains at walking speed while directional movement stays responsive

#### Scenario: Running is suppressed while walk-only is active
- **WHEN** walk-only is active and the Sub attempts to run
- **THEN** the character continues moving only at walking speed

#### Scenario: Releasing the device restores the prior locomotion mode
- **WHEN** the final active walk-only rule releases
- **THEN** the locomotion mode that existed before enforcement is restored

#### Scenario: Releasing the device restores running
- **WHEN** a walk-only device is released
- **THEN** the Sub can run normally again unless another active rule still requires walking

### Requirement: Action block rule suppresses action and skill usage
When an action-block rule is applied, the system SHALL reject supported action and skill execution requests regardless of whether they originate from a hotbar, game menu, keybind, macro, or command path, without suppressing movement. If action interception is unavailable, device application SHALL fail without reporting success.

#### Scenario: Action use is rejected from supported entry points
- **WHEN** action-block is active and the Sub attempts an action or skill from any supported invocation path
- **THEN** the action or skill does not execute

#### Scenario: Action usage is suppressed while action-block is active
- **WHEN** action-block is active and the Sub attempts a hotbar action or skill
- **THEN** the action does not execute

#### Scenario: Action interception is unavailable
- **WHEN** a device requests action-block but the action interceptor is unavailable
- **THEN** device application fails atomically and identifies the unavailable enforcement locally

#### Scenario: Releasing the device restores action usage
- **WHEN** the final active action-block rule releases
- **THEN** actions and skills execute normally again

### Requirement: Full Body Cuffed rule locks the Sub into a chosen bound animation and blocks movement
The system SHALL let a Sub or Owner assign Full Body Cuffed with an animation from the Sub's library. Applying it SHALL resolve and hold that animation on the Sub and continuously suppress every manual movement path until release. Owner-side selection SHALL use the paired Sub's imported animation library, never the Owner's local installed-mod library. Missing animation identity or enforcement capability SHALL fail the application atomically.

#### Scenario: Full Body Cuffed engages and immobilizes
- **WHEN** a valid Full Body Cuffed device is applied
- **THEN** its animation remains active and keyboard, controller, mouse, and autorun movement have no effect until release

#### Scenario: Full Body Cuffed engages the animation and blocks movement
- **WHEN** a Full Body Cuffed device with a chosen animation is applied
- **THEN** the animation remains active and movement input has no effect

#### Scenario: Owner chooses an animation
- **WHEN** the Owner opens the Full Body Cuffed animation picker
- **THEN** only animations imported from the paired Sub's shared library are offered

#### Scenario: Imported animation is stale
- **WHEN** the selected Sub animation no longer resolves on the Sub at apply time
- **THEN** the entire device application fails without leaving its slot or any rules active

#### Scenario: Releasing restores movement and animation settings
- **WHEN** Full Body Cuffed releases
- **THEN** its movement claim ends and its temporary animation settings are reverted

#### Scenario: Releasing the device restores movement and reverts the animation
- **WHEN** a Full Body Cuffed device releases
- **THEN** movement is restored unless another rule claims it and temporary animation settings are reverted

#### Scenario: A conflicting Full Body Cuffed request is refused
- **WHEN** a different Full Body Cuffed animation is already active
- **THEN** the new request is refused and the existing animation and movement restriction remain unchanged

#### Scenario: A rule cannot be assigned without a chosen animation
- **WHEN** Full Body Cuffed is configured without an animation
- **THEN** the system refuses to save the assignment

### Requirement: Owner-side restraint quick commands
The system SHALL let the Owner maintain restraint quick commands with rules from the fixed rule set. Any bound-animation rule SHALL be selected from the paired Sub's imported Gesture catalog, display its readable mod/group/animation/trigger label, and retain the stable Sub-side identity needed for execution. The picker SHALL NOT read or rescan the Owner's local animation mods.

#### Scenario: Owner edits a bound-animation rule
- **WHEN** the Owner chooses an animation for Arms Cuffed, Legs Cuffed, or Full Body Cuffed
- **THEN** the picker shows the imported Sub library and saves both its readable label and stable Sub-side identity

#### Scenario: Owner assigns rules to a quick command
- **WHEN** the Owner assigns valid restraint rules and saves them
- **THEN** the rules are retained on that quick command

#### Scenario: Owner sends a saved restraint quick command
- **WHEN** the Owner sends a saved restraint command with valid assigned rules
- **THEN** the command carries those rules for atomic enforcement by the Sub

#### Scenario: Owner quick command with no rules assigned yet
- **WHEN** a restraint quick command has no rules
- **THEN** its Send action remains unavailable until a valid rule set is saved

#### Scenario: No Sub library was imported
- **WHEN** the Owner opens a restraint animation picker without imported Sub gesture entries
- **THEN** the UI explains that the Sub's catalog must be imported and does not offer Owner-local animations
