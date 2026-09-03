# collar/restraints Specification

## Purpose

Lets a Sub tag scanned Glamourer designs as restraint devices carrying movement, action, and chat restriction rules, and lets both the Sub (via alias) and the Owner (via force override) apply and release those devices, so equipping a device has real, enforced gameplay consequences rather than only a cosmetic effect.

## Requirements

### Requirement: Restraints catalog scoped to its own scan and tagged designs
The system SHALL scan the Sub's saved Glamourer designs for Restraints independently of `collar/outfit`'s wardrobe scan, using its own folder allowlist (empty allowlist SHALL include every saved design, the same "empty means all" semantics `collar/outfit` uses). The Restraints nav tab SHALL populate from this independent scan, filtered to only those designs the Sub has tagged as a restraint device. Untagged designs SHALL NOT appear in the Restraints tab.

#### Scenario: Only tagged designs appear
- **WHEN** the Sub has scanned Restraints designs and tagged a subset of them as restraint devices
- **THEN** the Restraints tab lists only the tagged subset, and the untagged designs remain available only for tagging in the Restraints tab, not the Wardrobe/Outfit tab

#### Scenario: Restraints and Wardrobe scan scopes are independent
- **WHEN** the Sub configures a Restraints folder allowlist that differs from the Wardrobe folder allowlist
- **THEN** each scan includes only the designs within its own configured scope, and a design excluded from one scope can still be included in the other

### Requirement: A device carries one or more restriction rules
The system SHALL let the Sub assign one or more restriction rules to a restraint device from the fixed set: forced pose, walk-only, action block, and gag chat mangling. Applying the device SHALL activate every rule assigned to it; releasing the device SHALL deactivate every rule it activated.

#### Scenario: Applying a multi-rule device activates every rule
- **WHEN** a restraint device has both a walk-only rule and an action-block rule assigned
- **AND** the device is applied
- **THEN** both the walk-only restriction and the action-block restriction become active

### Requirement: Forced pose rule locks the Sub into a pose and blocks movement
When a device with a forced-pose rule is applied, the system SHALL place the Sub's character into the rule's configured pose and SHALL suppress all movement input until the device is released.

#### Scenario: Forced pose engages and blocks movement
- **WHEN** a device with a forced-pose rule targeting ground-sit is applied
- **THEN** the Sub's character enters the ground-sit pose and movement input has no effect while the device remains applied

#### Scenario: Releasing the device restores movement
- **WHEN** a device with an active forced-pose rule is released
- **THEN** movement input is no longer suppressed by that rule

### Requirement: Walk-only rule forces walking without blocking movement
When a device with a walk-only rule is applied, the system SHALL force the Sub's character into the walking movement state and SHALL suppress any input or game logic that would re-enable running, without suppressing directional movement input itself.

#### Scenario: Running is suppressed while walk-only is active
- **WHEN** a device with a walk-only rule is applied
- **AND** the Sub attempts to run
- **THEN** the character continues moving only at walking speed, and directional movement itself still responds to input

#### Scenario: Releasing the device restores running
- **WHEN** a device with an active walk-only rule is released
- **THEN** the Sub can run normally again

### Requirement: Action block rule suppresses action and skill usage
When a device with an action-block rule is applied, the system SHALL suppress execution of hotbar actions and skills until the device is released, without suppressing movement input.

#### Scenario: Action usage is suppressed while action-block is active
- **WHEN** a device with an action-block rule is applied
- **AND** the Sub attempts to use a hotbar action or skill
- **THEN** the action does not execute

#### Scenario: Releasing the device restores action usage
- **WHEN** a device with an active action-block rule is released
- **THEN** hotbar actions and skills execute normally again

### Requirement: Gag rule mangles the Sub's own outgoing chat text
When a device with a gag chat-mangling rule is applied, the system SHALL intercept every chat message the Sub sends and replace its text with a muffled/nonsense variant before transmission, so the message actually sent - not only the Sub's local display of it - is garbled. The rule SHALL apply to every outgoing chat channel the Sub sends on while the device remains applied.

#### Scenario: Outgoing chat is garbled before it sends
- **WHEN** a device with a gag rule is applied
- **AND** the Sub sends a chat message on any channel
- **THEN** the text actually transmitted is a muffled/nonsense variant of what the Sub typed, not the original text

#### Scenario: Releasing the device restores normal chat
- **WHEN** a device with an active gag rule is released
- **THEN** the Sub's subsequent outgoing chat messages send unmodified

### Requirement: Sub self-apply and self-release via alias
The system SHALL let the Sub apply or release a restraint device through their own locally-defined alias, the same trigger mechanism `collar/outfit` uses for wardrobe aliases.

#### Scenario: Sub applies a device via alias
- **WHEN** the Sub triggers a locally-defined alias mapped to a restraint device
- **THEN** the device is applied and its rules become active

### Requirement: Owner force-apply and force-release override
The system SHALL let the Owner force-apply a restraint device to a paired Sub, and force-release it, using the same override precedence `collar/outfit`'s ForceApply/ForceUnlock uses: while an Owner-forced device is active, the Sub's own self-apply/self-release controls for that device SHALL have no effect, and only the matching force-release (or panic) can end it.

#### Scenario: Owner force-applies a device
- **WHEN** a paired Owner sends a force-apply command for a restraint device the Sub has locally defined
- **THEN** the Sub's client applies the device and its rules become active

#### Scenario: Sub cannot self-release an Owner-forced device
- **WHEN** the Owner has force-applied a restraint device
- **AND** the Sub attempts to release it through their own alias
- **THEN** the device remains active and its rules stay in effect

### Requirement: Conflicting rule requests are refused
The system SHALL refuse to activate a restriction rule that conflicts with an already-active rule from a different device (for example, two forced-pose rules targeting different poses), leaving the existing active rule and the requesting device's apply action unchanged. Two devices activating the same non-conflicting rule kind (for example, two devices both carrying action-block) SHALL be allowed to be active at once.

#### Scenario: A conflicting forced-pose request is refused
- **WHEN** one device with a forced-pose rule is active
- **AND** a second device with a different forced-pose target is applied
- **THEN** the second device's apply is refused and the first device's pose rule remains active and unchanged

#### Scenario: Non-conflicting duplicate rule kinds coexist
- **WHEN** one device with an action-block rule is active
- **AND** a second device that also carries an action-block rule is applied
- **THEN** both devices remain active and action usage stays suppressed

### Requirement: Panic releases every active restriction rule
The system SHALL deactivate every currently active restriction rule, from every applied device regardless of whether it was Sub-applied or Owner-forced, when the Sub triggers the panic action.

#### Scenario: Panic clears all active restrictions
- **WHEN** the Sub has multiple restraint devices active, including at least one Owner-forced device
- **AND** the Sub triggers panic
- **THEN** movement, action usage, and outgoing chat all return to unrestricted, and every device is released

### Requirement: Owner-side restraint quick commands
The system SHALL let the Owner maintain a saved list of restraint device quick commands (one per tagged device name), each sendable to the paired Sub as a `restraint lock <name>` command with a single action, the same quick-command pattern Outfit/Gesture/Moodles already provide.

#### Scenario: Owner sends a saved restraint quick command
- **WHEN** the Owner has a saved restraint quick command for a device name
- **THEN** triggering it sends the same `restraint lock <name>` command a manually typed override would send

### Requirement: Restraint device catalog export by name
The system SHALL let the Sub export their currently-tagged restraint device names as plain text, the same way Wardrobe and Moodles export their catalog names, so an Owner can build restraint quick commands from a Sub-provided name list.

#### Scenario: Sub exports tagged device names
- **WHEN** the Sub has one or more restraint devices tagged
- **THEN** the exported text contains each tagged device's display name once
