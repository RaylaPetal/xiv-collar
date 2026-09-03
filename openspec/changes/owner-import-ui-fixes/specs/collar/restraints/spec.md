## MODIFIED Requirements

### Requirement: Restraint device catalog export by name
The system SHALL let the Sub export every scanned Restraints design name as plain text, regardless of whether the Sub has tagged it as a device, the same way Wardrobe and Moodles export their catalog names, so an Owner can build restraint quick commands from a Sub-provided name list without requiring the Sub to tag devices first.

#### Scenario: Sub exports all scanned design names
- **WHEN** the Sub has scanned Restraints designs, tagged or untagged
- **THEN** the exported text contains each scanned design's display name once, including designs the Sub has not tagged as a device

#### Scenario: Sub exports tagged device names
- **WHEN** the Sub has one or more restraint devices tagged
- **THEN** the exported text still contains each tagged device's display name, alongside any untagged scanned designs

### Requirement: Owner-side restraint quick commands
The system SHALL let the Owner maintain a saved list of restraint device quick commands, one per scanned design name (tagged by the Sub or not), each carrying an Owner-assigned set of restriction rules chosen from the same fixed rule set the Sub's own device-tagging UI uses (forced pose, walk-only, action block, gag chat mangling). Each quick command SHALL be sendable to the paired Sub as a `restraint lock <name>` command carrying the Owner-assigned rules, with a single action, the same quick-command pattern Outfit/Gesture/Moodles already provide.

#### Scenario: Owner assigns rules to a quick command
- **WHEN** the Owner selects a restraint quick command entry and chooses one or more restriction rules for it, the same way the Sub's own tagging UI does
- **THEN** those rules are saved with that quick command and are included whenever the Owner sends it

#### Scenario: Owner sends a saved restraint quick command
- **WHEN** the Owner has a saved restraint quick command for a device name with rules assigned
- **THEN** triggering it sends a `restraint lock <name>` command carrying the Owner-assigned rules

#### Scenario: Owner quick command with no rules assigned yet
- **WHEN** the Owner has a restraint quick command imported but has not yet assigned any rules to it
- **THEN** the system prevents sending that quick command and indicates rules must be assigned first

### Requirement: Owner force-apply and force-release override
The system SHALL let the Owner force-apply a restraint device to a paired Sub by name, using the restriction rules the Owner assigned to that quick command rather than any rules the Sub may have separately tagged for the same design, and force-release it, using the same override precedence `collar/outfit`'s ForceApply/ForceUnlock uses: while an Owner-forced device is active, the Sub's own self-apply/self-release controls for that device SHALL have no effect, and only the matching force-release (or panic) can end it.

#### Scenario: Owner force-applies a device
- **WHEN** a paired Owner sends a force-apply command for a restraint quick command carrying Owner-assigned rules
- **THEN** the Sub's client activates exactly those rules, regardless of whether the Sub has separately tagged that same design with different or no rules

#### Scenario: Sub cannot self-release an Owner-forced device
- **WHEN** the Owner has force-applied a restraint device
- **AND** the Sub attempts to release it through their own alias
- **THEN** the device remains active and its rules stay in effect
