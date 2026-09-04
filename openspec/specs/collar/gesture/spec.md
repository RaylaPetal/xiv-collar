# collar/gesture Specification

## Purpose

Lets a Sub share an auto-generated catalog of their installed gesture (emote) mods with a paired Owner, and lets the Owner trigger a selected animation locally under the Sub's permission controls.

## Requirements

### Requirement: Automatic gesture catalog from installed mods
The Sub's client SHALL build its gesture catalog from explicitly selected installed Penumbra mods using each mod's own option-group metadata. The catalog SHALL preserve the mod, group, and human-readable animation option names and SHALL associate each option with every detected slash-emote or supported pose trigger derived from explicit command hints and redirected animation paths, without requiring manual tagging for recognized triggers. A detected slash-emote trigger SHALL count as resolved only if its command text is non-empty - a redirected animation path that matches a game emote with no actual command text (for example, an emote whose text-command data is blank) SHALL be treated the same as an option with no playable trigger, not offered as a commandable slash-emote.

#### Scenario: Mod resolves to a known emote
- **WHEN** a selected installed mod contains an option whose metadata resolves to a known emote animation
- **THEN** the gesture catalog includes that named option together with its resolved emote trigger, without manual input from the Sub

#### Scenario: Mod does not resolve to a known emote
- **WHEN** a selected installed mod contains an option that does not resolve to a known emote or supported pose trigger
- **THEN** the catalog surfaces the named option as non-playable rather than silently omitting the mod or inventing a trigger

#### Scenario: Named option resolves to a slash emote
- **WHEN** a selected mod option contains an explicit slash-command hint or redirects an animation path associated with a game emote
- **THEN** the catalog shows the animation option's name and the resolved slash-emote trigger together

#### Scenario: Named option resolves to a supported pose
- **WHEN** a selected mod option redirects a supported sit, ground-sit, or doze pose animation path
- **THEN** the catalog shows the animation option's name and the resolved pose name together

#### Scenario: Option has no playable trigger
- **WHEN** a selected mod option has no recognized slash-emote or supported pose trigger
- **THEN** the catalog still shows the named option for context but does not offer it as a gesture command

#### Scenario: Mod uses only default redirects
- **WHEN** a selected mod has playable animation redirects in its default configuration and no configurable groups
- **THEN** the catalog exposes a named default animation entry with its detected trigger

#### Scenario: Redirected path matches an emote with blank command text
- **WHEN** a selected mod option redirects an animation path that matches a game emote whose own text-command data is empty
- **THEN** the catalog treats that option the same as one with no playable trigger, rather than offering an emote command that can never actually execute

### Requirement: Sub can scope which mods are scanned
The system SHALL let a Sub explicitly select which installed Penumbra mods participate in gesture scanning. An empty selected-mod set SHALL include every installed mod, while one or more explicit selections SHALL restrict scanning to those mods. The selection UI SHALL show mod display names and MAY be narrowed visually by Penumbra sort folder or text search; changing or clearing a visual filter SHALL NOT itself change the persisted selected set.

#### Scenario: Sub scopes to an allowlisted folder
- **WHEN** a Sub uses a Penumbra sort-folder filter and explicitly selects mods from the filtered results
- **THEN** only those explicitly selected mods participate in scanning after at least one selection exists, while the folder filter itself neither selects nor excludes additional mods

#### Scenario: Sub selects mods to scan
- **WHEN** a Sub explicitly selects one or more installed Penumbra mods and triggers a rescan
- **THEN** the generated catalog contains animation metadata only from those selected mods

#### Scenario: Selected mod is disabled
- **WHEN** a selected installed mod is currently disabled in the Sub's effective Penumbra collection
- **THEN** its animations remain discoverable and a later command can enable the chosen configuration temporarily

#### Scenario: No mods are selected
- **WHEN** a Sub triggers a scan with an empty selected-mod set
- **THEN** the generated catalog contains animation metadata from every installed Penumbra mod

#### Scenario: Visual filters are empty
- **WHEN** the folder and text filters are empty
- **THEN** the selection UI shows every installed mod without mutating the selected-mod set

### Requirement: Animation identity is preserved across sharing and commands
The system SHALL identify a commandable gesture by its mod and named animation option plus its tied trigger, and SHALL display that animation name and trigger when the Sub browses or exports the catalog, when the Owner imports or sends it, and when the Sub receives it.

#### Scenario: Sub exports gesture names
- **WHEN** the Sub copies their scanned gesture catalog for an Owner
- **THEN** each commandable entry includes enough human-readable mod, animation-option, and trigger information for the Owner to know which animation will be used

#### Scenario: Owner chooses an imported animation
- **WHEN** the Owner imports the Sub's catalog and views gesture quick commands
- **THEN** the Owner can distinguish animations by their displayed option names and tied triggers, including multiple options that use the same base gesture

#### Scenario: Sub opens the animation picker
- **WHEN** the Sub chooses “Add animation” while defining a gesture alias
- **THEN** a dedicated picker window presents searchable collapsible mods, their option groups and animations in Penumbra manifest order, tied triggers, and disabled or non-playable status without crowding the main collar window

### Requirement: Permitted gesture command plays immediately with temporary mod settings
The system SHALL execute a valid Owner gesture command immediately when the paired Sub has enabled Gesture permission and completed the automation-risk acknowledgement. Before playing, the Sub's client SHALL apply a scoped temporary Penumbra override to the Sub's effective collection that enables the selected mod and supplies its complete group-selection state, SHALL redraw the local player, and SHALL then play the animation's tied slash-emote or supported pose trigger. The system SHALL NOT persistently change the mod's saved Penumbra settings.

#### Scenario: Owner sends a permitted slash-emote animation
- **WHEN** an Owner sends a valid cataloged animation command and the paired Sub has Gesture permission enabled
- **THEN** the Sub temporarily enables the selected mod/options, redraws the local player, and plays the tied slash emote without a second confirmation action

#### Scenario: Owner sends a permitted pose animation
- **WHEN** an Owner sends a valid cataloged animation command tied to a supported sit, ground-sit, or doze pose and the paired Sub has Gesture permission enabled
- **THEN** the Sub temporarily enables the selected mod/options, redraws the local player, and enters the tied pose without a second confirmation action

#### Scenario: Gesture command without permission
- **WHEN** an Owner sends a gesture command to a Sub who has not enabled Gesture permission or completed its prerequisite acknowledgement
- **THEN** the Sub's client rejects the command and changes neither Penumbra state nor the played gesture

#### Scenario: Temporary activation fails
- **WHEN** the selected mod/options cannot be applied temporarily to the Sub's effective Penumbra collection
- **THEN** the Sub's client does not play the tied trigger and reports the failure

### Requirement: Temporary gesture activation is reverted after use
The system SHALL let a Sub manually revert a played gesture's temporary Penumbra mod activation back to the mod's saved settings at any time via a dedicated control, and SHALL automatically revert it after a period of inactivity following the last gesture play, so a temporary activation does not persist indefinitely.

#### Scenario: Sub manually resets an active temporary gesture activation
- **WHEN** a gesture has temporarily activated a mod's settings
- **AND** the Sub uses the reset control
- **THEN** the mod's temporary settings are reverted and its saved settings apply again

#### Scenario: Temporary gesture activation times out
- **WHEN** a gesture has temporarily activated a mod's settings
- **AND** no further gesture is played for approximately 30 seconds
- **THEN** the temporary settings are automatically reverted and the mod's saved settings apply again

#### Scenario: New gesture play resets the timeout
- **WHEN** a temporary gesture activation is pending automatic reversion
- **AND** the Sub plays another gesture before the timeout elapses
- **THEN** the timeout restarts from the new play
