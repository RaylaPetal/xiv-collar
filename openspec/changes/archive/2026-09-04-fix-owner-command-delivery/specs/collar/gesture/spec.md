## MODIFIED Requirements

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
