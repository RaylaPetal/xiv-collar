## REMOVED Requirements

### Requirement: Restraints catalog scoped to its own scan and tagged designs
**Reason**: A restraint device is no longer a reference to a whole Glamourer design scanned from a saved-designs library. It now captures a single equipped gear piece directly (see "Restraint device captured from a single equipped gear piece").
**Migration**: The Sub re-captures each restraint device by equipping the desired piece and using the new per-slot capture action in the Restraints tab. The Restraints folder allowlist setting and the Restraints scan step no longer exist; Restraints also drops out of the unified "Scan all" action (see `collar/catalog-sync`).

### Requirement: Restraint device catalog export by name
**Reason**: Replaced by a simpler export requirement of the same name - every restraint device is now captured and named in one step, so there is no longer a "scanned but untagged" distinction to export around.
**Migration**: None needed for the Owner - the exported text still contains one name per device, unchanged in format.

## ADDED Requirements

### Requirement: Restraint device captured from a single equipped gear piece
The system SHALL let the Sub capture a restraint device from a single equipment slot's currently-equipped item, reading its item, stain, and stain2 the same way `collar/collaring`'s Collar item is captured, rather than referencing a whole Glamourer design. The Sub SHALL choose which of the lockable equipment slots to capture from, and SHALL name the device at capture time. A captured device SHALL be immediately available for rule assignment and export - there is no separate "scan" step and no untagged/tagged distinction.

#### Scenario: Sub captures a device from an equipped item
- **WHEN** the Sub has a specific gear piece equipped in a chosen slot and captures it as a new restraint device with a name
- **THEN** the device is saved with that slot's item, stain, and stain2, and is immediately available to assign rules to, alias, and export

#### Scenario: Capturing a device does not require scanning a design library
- **WHEN** the Sub opens the Restraints tab
- **THEN** capturing a new device only requires an item already equipped in some slot, with no prior scan of saved Glamourer designs

#### Scenario: Applying a captured device sets only its own slot
- **WHEN** a restraint device captured from a single slot is applied
- **THEN** only that one equipment slot changes, and every other slot remains exactly as free to edit as if no device were active

### Requirement: Exporting captured restraint device names
The system SHALL let the Sub export their captured restraint device names as plain text, the same way Wardrobe and Moodles export their catalog names, so an Owner can build restraint quick commands from a Sub-provided name list.

#### Scenario: Sub exports captured device names
- **WHEN** the Sub has one or more restraint devices captured
- **THEN** the exported text contains each captured device's display name once

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

### Requirement: Owner can add a restraint quick command by name
The system SHALL let the Owner add a restraint quick command by typing a device name themselves, the same freeform "Add Command" pattern the Title category already provides, in addition to populating the list by importing a Sub-exported file.

#### Scenario: Owner adds a restraint quick command manually
- **WHEN** the Owner types a device name the Sub told them and adds it as a restraint quick command
- **THEN** a new restraint quick command entry is created for that name, available for rule assignment the same as an imported entry

## MODIFIED Requirements

### Requirement: A device carries one or more restriction rules
The system SHALL let the Sub assign one or more restriction rules to a restraint device from the fixed set: forced pose, walk-only, action block, Gagged (chat mangling), Arms Cuffed, Legs Cuffed, and Full Body Cuffed. Applying the device SHALL activate every rule assigned to it; releasing the device SHALL deactivate every rule it activated.

#### Scenario: Applying a multi-rule device activates every rule
- **WHEN** a restraint device has both a walk-only rule and an action-block rule assigned
- **AND** the device is applied
- **THEN** both the walk-only restriction and the action-block restriction become active

### Requirement: Gag rule mangles the Sub's own outgoing chat text
When a device with a Gagged (chat-mangling) rule is applied, the system SHALL intercept every chat message the Sub sends and replace its text with a muffled/nonsense variant before transmission, so the message actually sent - not only the Sub's local display of it - is garbled. The rule SHALL apply to every outgoing chat channel the Sub sends on while the device remains applied. The rule SHALL be labeled "Gagged" wherever it is shown to the Sub or Owner.

#### Scenario: Outgoing chat is garbled before it sends
- **WHEN** a device with a gag rule is applied
- **AND** the Sub sends a chat message on any channel
- **THEN** the text actually transmitted is a muffled/nonsense variant of what the Sub typed, not the original text

#### Scenario: Releasing the device restores normal chat
- **WHEN** a device with an active gag rule is released
- **THEN** the Sub's subsequent outgoing chat messages send unmodified

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
