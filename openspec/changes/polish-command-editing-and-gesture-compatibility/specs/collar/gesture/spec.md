## MODIFIED Requirements

### Requirement: Permitted gesture command plays immediately with temporary mod settings
The system SHALL resolve and execute both legacy opaque gesture identities and current quoted human-readable selectors when the paired Sub has enabled Gesture permission and completed the automation-risk acknowledgement. Resolution SHALL target exactly one local catalog entry; before playing, the Sub's client SHALL apply a scoped temporary Penumbra override to the Sub's effective collection that enables the selected mod and supplies its complete group-selection state, SHALL redraw the local player, and SHALL then play the animation's tied slash-emote or supported pose trigger. The system SHALL NOT persistently change the mod's saved Penumbra settings, and SHALL locally distinguish missing, ambiguous, temporary-setting, redraw, and playback failures rather than silently doing nothing.

#### Scenario: Owner sends a legacy opaque gesture identity
- **WHEN** an Owner sends a gesture command containing a legacy opaque ID that uniquely identifies a local catalog entry
- **THEN** the Sub resolves that entry and plays it through the same temporary activation path as a readable selector

#### Scenario: Owner sends a permitted slash-emote animation
- **WHEN** an Owner sends a quoted readable selector that uniquely identifies a cataloged animation
- **THEN** the Sub temporarily enables the selected mod/options, redraws the local player, and plays its tied trigger without a second confirmation action

#### Scenario: Owner sends a permitted pose animation
- **WHEN** an Owner sends a valid gesture command tied to a supported sit, ground-sit, or doze pose and the paired Sub has Gesture permission enabled
- **THEN** the Sub temporarily enables the selected mod/options, redraws the local player, and enters the tied pose without a second confirmation action

#### Scenario: Gesture command without permission
- **WHEN** an Owner sends a gesture command to a Sub who has not enabled Gesture permission or completed its prerequisite acknowledgement
- **THEN** the Sub's client rejects the command and changes neither Penumbra state nor the played gesture

#### Scenario: Gesture selector cannot resolve uniquely
- **WHEN** the supplied legacy ID or readable selector is missing, stale, or ambiguous in the Sub's local catalog
- **THEN** no temporary settings or animation are applied and the local diagnostic identifies the resolution failure category

#### Scenario: Temporary activation fails
- **WHEN** the selected mod/options cannot be applied, the redraw fails, or the tied trigger cannot be played
- **THEN** the Sub avoids or rolls back partial temporary state where possible and locally reports the failed stage
