## Purpose

Lets a Sub share an auto-generated catalog of their installed gesture (emote) mods with a paired Owner, and lets the Owner prompt a gesture that the Sub confirms and triggers locally.

## ADDED Requirements

### Requirement: Automatic gesture catalog from installed mods
The Sub's client SHALL build its gesture catalog automatically by scanning installed Penumbra mods and using Penumbra's own file-to-emote identification, without requiring the Sub to manually tag each mod.

#### Scenario: Mod resolves to a known emote
- **WHEN** an installed mod's changed files match a known emote animation in Penumbra's identification data
- **THEN** the gesture catalog includes that mod labeled with the resolved emote name(s), without manual input from the Sub

#### Scenario: Mod does not resolve to a known emote
- **WHEN** an installed mod's changed files do not match any known emote animation
- **THEN** the catalog surfaces that mod as unresolved rather than silently omitting it, and the Sub may manually assign an emote to it

### Requirement: Sub can scope which mods are scanned
The system SHALL let a Sub restrict gesture catalog scanning to an allowlisted set of mod folders, rather than exposing every installed mod to the Owner.

#### Scenario: Sub scopes to an allowlisted folder
- **WHEN** a Sub configures an allowlist of mod folders for gesture scanning
- **THEN** the generated catalog includes only mods located under those folders

### Requirement: Catalog shared with paired Owner
The system SHALL relay the Sub's current gesture catalog (mod identifier, folder path, and resolved emote name(s)) to a paired Owner with the "gesture" permission enabled, and cache it locally on the Owner's client for offline browsing.

#### Scenario: Owner receives updated catalog
- **WHEN** a Sub's gesture catalog changes and the Sub is paired with an Owner who has "gesture" permission enabled
- **THEN** the updated catalog is relayed to the Owner's client and cached there

### Requirement: Gesture trigger requires Sub confirmation
The system SHALL NOT auto-fire a gesture on the Sub's client without an explicit Sub-side confirmation action for that trigger. An Owner-sent gesture request SHALL be visibly queued on the Sub's client until the Sub confirms it.

#### Scenario: Owner sends a gesture prompt
- **WHEN** an Owner selects a cataloged gesture and sends it to a paired Sub with "gesture" permission enabled
- **THEN** the Sub's client visibly queues the prompt and does not trigger the gesture until the Sub confirms

#### Scenario: Sub confirms a queued gesture
- **WHEN** a Sub confirms a queued gesture prompt
- **THEN** the Sub's client activates the mapped mod/collection and triggers the corresponding emote

#### Scenario: Gesture command without permission
- **WHEN** an Owner sends a gesture prompt to a Sub who has not enabled the "gesture" permission
- **THEN** the Sub's client rejects the command and no prompt is queued
