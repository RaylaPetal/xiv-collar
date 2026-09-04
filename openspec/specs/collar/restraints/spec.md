# collar/restraints Specification

## Purpose

Lets a Sub capture a single equipped gear piece as a restraint device carrying movement, action, chat, and bound-animation restriction rules, and lets both the Sub (via alias) and the Owner (via force override) apply and release those devices, so equipping a device has real, enforced gameplay consequences rather than only a cosmetic effect.

## Requirements

### Requirement: Restraint device captured from a single equipped gear piece
The system SHALL let the Sub capture a restraint device by picking one of the lockable equipment slots and then picking an item for that slot from a searchable item picker, rather than by equipping the item first and reading it back from live game state, and rather than referencing a whole Glamourer design. The picker SHALL let the Sub choose from every item valid for the chosen slot, not only items the Sub currently owns or has equipped. The Sub SHALL name the device at capture time. A captured device SHALL be immediately available for rule assignment and export - there is no separate "scan" step and no untagged/tagged distinction.

#### Scenario: Sub captures a device from an equipped item
- **WHEN** the Sub picks a slot and picks an item for it from the picker, and captures it as a new restraint device with a name
- **THEN** the device is saved with that slot and item (undyed), and is immediately available to assign rules to, alias, and export

#### Scenario: Capturing a device does not require scanning a design library
- **WHEN** the Sub opens the Restraints tab
- **THEN** capturing a new device only requires picking a slot and an item from the picker, with no prior scan of saved Glamourer designs and no requirement that the item be currently equipped or owned

#### Scenario: Applying a captured device sets only its own slot
- **WHEN** a restraint device captured from a single slot is applied
- **THEN** only that one equipment slot changes, and every other slot remains exactly as free to edit as if no device were active

### Requirement: Owner-authored ad-hoc restraint device
The system SHALL let the Owner define a restraint device's slot and item directly, using the same slot-and-item picker the Sub's own capture flow uses, without requiring the Sub to have captured, named, or shared the name of that device beforehand. The Owner SHALL give the ad-hoc device a local label for their own reference and SHALL assign it restriction rules from the same fixed rule set Sub-captured devices use. Sending an Owner-authored ad-hoc device to the paired Sub SHALL carry the full slot, item, and rule definition in the command itself, and SHALL apply and release using the same force-apply/force-release override precedence as a name-referenced quick command.

#### Scenario: Owner defines and sends an ad-hoc device
- **WHEN** the Owner picks a slot and an item from the picker, assigns one or more restriction rules, and sends it
- **THEN** the paired Sub's client equips that slot with that item and activates exactly the assigned rules, with no lookup of any Sub-side captured device by name

#### Scenario: Ad-hoc device follows the same force-release precedence
- **WHEN** the Owner has sent an ad-hoc device and the Sub attempts to remove it through their own controls
- **THEN** the device remains active and its rules stay in effect, the same as an Owner-forced name-referenced device

#### Scenario: Ad-hoc device conflicts are checked the same as any other device
- **WHEN** an Owner-authored ad-hoc device's assigned rules would conflict with an already-active rule from a different device
- **THEN** the ad-hoc device's apply is refused and the existing active rule remains unchanged, the same as a conflict between two name-referenced devices

### Requirement: A device carries one or more restriction rules
The system SHALL let the Sub assign one or more restriction rules to a restraint device from the fixed set: forced pose, walk-only, action block, Gagged (chat mangling), Arms Cuffed, Legs Cuffed, and Full Body Cuffed. Applying the device SHALL activate every rule assigned to it; releasing the device SHALL deactivate every rule it activated.

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
When a device with a Gagged (chat-mangling) rule is applied, the system SHALL intercept every chat message the Sub sends and replace its text with a muffled/nonsense variant before transmission, so the message actually sent - not only the Sub's local display of it - is garbled. The rule SHALL apply to every outgoing chat channel the Sub sends on while the device remains applied. The rule SHALL be labeled "Gagged" wherever it is shown to the Sub or Owner.

#### Scenario: Outgoing chat is garbled before it sends
- **WHEN** a device with a gag rule is applied
- **AND** the Sub sends a chat message on any channel
- **THEN** the text actually transmitted is a muffled/nonsense variant of what the Sub typed, not the original text

#### Scenario: Releasing the device restores normal chat
- **WHEN** a device with an active gag rule is released
- **THEN** the Sub's subsequent outgoing chat messages send unmodified

### Requirement: Arms Cuffed and Legs Cuffed rules lock the Sub into a chosen bound animation
The system SHALL let the Sub or Owner assign an Arms Cuffed rule, a Legs Cuffed rule, or both to a restraint device (or Owner quick command), each carrying its own chosen animation selected from the Sub's own installed-mod animation catalog using the same searchable picker `collar/gesture` provides for choosing an animation. When a device carrying one of these rules is applied, the system SHALL temporarily activate the chosen animation's mod/option and hold the Sub in it for as long as the rule stays active, with no automatic timeout. When the rule is released, the system SHALL revert that temporary activation, the same reversion `collar/gesture`'s own temporary gesture activation performs, independently of `collar/gesture`'s own idle-timeout behavior.

#### Scenario: Arms Cuffed engages and holds the chosen animation
- **WHEN** a device with an Arms Cuffed rule carrying a chosen animation is applied
- **THEN** the Sub's client temporarily activates that animation's mod/option and holds the Sub in it, with no automatic timeout reverting it while the device remains applied

#### Scenario: Legs Cuffed engages and holds the chosen animation
- **WHEN** a device with a Legs Cuffed rule carrying a chosen animation is applied
- **THEN** the Sub's client temporarily activates that animation's mod/option and holds the Sub in it, with no automatic timeout reverting it while the device remains applied

#### Scenario: Releasing the device reverts the held animation
- **WHEN** a device with an active Arms Cuffed or Legs Cuffed rule is released
- **THEN** the temporarily-activated mod/option is reverted to its saved settings, the same as `collar/gesture`'s own temporary activation reversion

#### Scenario: Arms Cuffed and Legs Cuffed can be active together
- **WHEN** a device carries both an Arms Cuffed rule and a Legs Cuffed rule, each with its own chosen animation
- **THEN** applying the device holds both chosen animations active at once, independently of each other

#### Scenario: A rule cannot be assigned without a chosen animation
- **WHEN** the Sub or Owner attempts to assign an Arms Cuffed or Legs Cuffed rule without selecting an animation for it
- **THEN** the system refuses to save that rule assignment

### Requirement: Full Body Cuffed rule locks the Sub into a chosen bound animation and blocks movement
The system SHALL let the Sub or Owner assign a Full Body Cuffed rule to a restraint device (or Owner quick command), carrying its own chosen animation selected the same way Arms Cuffed and Legs Cuffed choose theirs. When a device carrying this rule is applied, the system SHALL temporarily activate and hold the chosen animation exactly as Arms Cuffed/Legs Cuffed do, and SHALL additionally suppress all movement input until the device is released, the same movement suppression the forced-pose rule provides.

#### Scenario: Full Body Cuffed engages the animation and blocks movement
- **WHEN** a device with a Full Body Cuffed rule carrying a chosen animation is applied
- **THEN** the Sub's client holds the chosen animation active and movement input has no effect while the device remains applied

#### Scenario: Releasing the device restores movement and reverts the animation
- **WHEN** a device with an active Full Body Cuffed rule is released
- **THEN** movement input is no longer suppressed by that rule, and the temporarily-activated animation is reverted to its saved settings

#### Scenario: A conflicting Full Body Cuffed request is refused
- **WHEN** one device with a Full Body Cuffed rule carrying one animation is active
- **AND** a second device with a Full Body Cuffed rule carrying a different animation is applied
- **THEN** the second device's apply is refused and the first device's Full Body Cuffed animation and movement suppression remain active and unchanged

#### Scenario: A rule cannot be assigned without a chosen animation
- **WHEN** the Sub or Owner attempts to assign a Full Body Cuffed rule without selecting an animation for it
- **THEN** the system refuses to save that rule assignment

### Requirement: Sub self-apply and self-release via alias
The system SHALL let the Sub apply or release a restraint device through their own locally-defined alias, the same trigger mechanism `collar/outfit` uses for wardrobe aliases.

#### Scenario: Sub applies a device via alias
- **WHEN** the Sub triggers a locally-defined alias mapped to a restraint device
- **THEN** the device is applied and its rules become active

### Requirement: Owner force-apply and force-release override
The system SHALL let the Owner force-apply a restraint device to a paired Sub by name, using the restriction rules the Owner assigned to that quick command rather than any rules the Sub may have separately tagged for the same design, and force-release it, using the same override precedence `collar/outfit`'s ForceApply/ForceUnlock uses: while an Owner-forced device is active, the Sub's own self-apply/self-release controls for that device SHALL have no effect, and only the matching force-release (or panic) can end it.

#### Scenario: Owner force-applies a device
- **WHEN** a paired Owner sends a force-apply command for a restraint quick command carrying Owner-assigned rules
- **THEN** the Sub's client activates exactly those rules, regardless of whether the Sub has separately tagged that same design with different or no rules

#### Scenario: Sub cannot self-release an Owner-forced device
- **WHEN** the Owner has force-applied a restraint device
- **AND** the Sub attempts to release it through their own alias
- **THEN** the device remains active and its rules stay in effect

### Requirement: Conflicting rule requests are refused
The system SHALL refuse to activate a restriction rule that conflicts with an already-active rule from a different device (for example, two forced-pose rules targeting different poses, or two Arms Cuffed, Legs Cuffed, or Full Body Cuffed rules carrying different animations), leaving the existing active rule and the requesting device's apply action unchanged. Two devices activating the same non-conflicting rule kind, or the same rule kind with the same configuration (for example, two devices both carrying action-block, or two devices both carrying an Arms Cuffed rule with the same chosen animation), SHALL be allowed to be active at once.

#### Scenario: A conflicting forced-pose request is refused
- **WHEN** one device with a forced-pose rule is active
- **AND** a second device with a different forced-pose target is applied
- **THEN** the second device's apply is refused and the first device's pose rule remains active and unchanged

#### Scenario: Non-conflicting duplicate rule kinds coexist
- **WHEN** one device with an action-block rule is active
- **AND** a second device that also carries an action-block rule is applied
- **THEN** both devices remain active and action usage stays suppressed

#### Scenario: A conflicting bound-animation request is refused
- **WHEN** one device with an Arms Cuffed rule carrying one animation is active
- **AND** a second device with an Arms Cuffed rule carrying a different animation is applied
- **THEN** the second device's apply is refused and the first device's Arms Cuffed animation remains active and unchanged

### Requirement: Panic releases every active restriction rule
The system SHALL deactivate every currently active restriction rule, from every applied device regardless of whether it was Sub-applied or Owner-forced, when the Sub triggers the panic action. Any temporarily-activated Arms Cuffed, Legs Cuffed, or Full Body Cuffed animation SHALL be reverted as part of this release.

#### Scenario: Panic clears all active restrictions
- **WHEN** the Sub has multiple restraint devices active, including at least one Owner-forced device
- **AND** the Sub triggers panic
- **THEN** movement, action usage, outgoing chat, and any held bound animations all return to unrestricted, and every device is released

### Requirement: Owner-side restraint quick commands
The system SHALL let the Owner maintain a saved list of restraint device quick commands, one per captured device name, each carrying an Owner-assigned set of restriction rules chosen from the same fixed rule set the Sub's own device-capture UI uses (forced pose, walk-only, action block, Gagged, Arms Cuffed, Legs Cuffed, and Full Body Cuffed - the latter three each with their own chosen animation, selected via the same picker `collar/gesture` provides). Each quick command SHALL be sendable to the paired Sub as a `restraint lock <name>` command carrying the Owner-assigned rules, with a single action, the same quick-command pattern Outfit/Gesture/Moodles already provide.

#### Scenario: Owner assigns rules to a quick command
- **WHEN** the Owner selects a restraint quick command entry and chooses one or more restriction rules for it, the same way the Sub's own device-capture UI does
- **THEN** those rules are saved with that quick command and are included whenever the Owner sends it

#### Scenario: Owner sends a saved restraint quick command
- **WHEN** the Owner has a saved restraint quick command for a device name with rules assigned
- **THEN** triggering it sends a `restraint lock <name>` command carrying the Owner-assigned rules

#### Scenario: Owner quick command with no rules assigned yet
- **WHEN** the Owner has a restraint quick command imported but has not yet assigned any rules to it
- **THEN** the system prevents sending that quick command and indicates rules must be assigned first

### Requirement: Owner can add a restraint quick command by name
The system SHALL let the Owner add a restraint quick command by typing a device name themselves, the same freeform "Add Command" pattern the Title category already provides, in addition to populating the list by importing a Sub-exported file.

#### Scenario: Owner adds a restraint quick command manually
- **WHEN** the Owner types a device name the Sub told them and adds it as a restraint quick command
- **THEN** a new restraint quick command entry is created for that name, available for rule assignment the same as an imported entry

### Requirement: Exporting captured restraint device names
The system SHALL let the Sub export their captured restraint device names as plain text, the same way Wardrobe and Moodles export their catalog names, so an Owner can build restraint quick commands from a Sub-provided name list.

#### Scenario: Sub exports captured device names
- **WHEN** the Sub has one or more restraint devices captured
- **THEN** the exported text contains each captured device's display name once
