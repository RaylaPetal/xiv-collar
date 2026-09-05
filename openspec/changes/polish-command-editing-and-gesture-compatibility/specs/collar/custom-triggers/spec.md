## ADDED Requirements

### Requirement: Saved Custom Triggers are fully editable
The system SHALL let the user edit an existing Sub Custom Trigger and an existing Owner-authored custom bundle without deleting and recreating it. Editing SHALL allow changing its visible label or alias, adding/removing/reordering actions, and modifying every action-specific value through the same validated controls used during creation. Saving SHALL update the existing entry in place; cancelling SHALL preserve it unchanged.

#### Scenario: User edits a saved bundle
- **WHEN** the user opens Edit on a saved Custom Trigger, changes its actions, and saves
- **THEN** the same saved entry contains the revised ordered actions and its next execution uses them

#### Scenario: User cancels editing
- **WHEN** the user changes fields in an editor and cancels or closes it without saving
- **THEN** the persisted trigger remains exactly as it was before editing

#### Scenario: Edited bundle is invalid
- **WHEN** an edit leaves the alias invalid, the action list empty, or an action missing a required target
- **THEN** Save remains unavailable and the editor identifies what must be corrected

#### Scenario: Owner invokes a bundle containing multiple restraints
- **WHEN** a saved Custom Trigger contains two compatible restraint actions and is invoked as an Owner command
- **THEN** both restraints are applied by stable device identity under the same Owner force-lock, and re-running the apply-only bundle does not toggle either restraint off

### Requirement: Custom Trigger summaries are structured and friendly
The system SHALL present saved and draft Custom Trigger actions using consistent user-facing names, capitalization, icons or compact visual grouping, and readable target details. It SHALL distinguish multiple actions without relying solely on a lowercase comma-joined sentence and SHALL hide opaque stable identities from the normal summary.

#### Scenario: Bundle contains restraints and a Moodle
- **WHEN** a bundle contains Body Cuffed, Gagged, and a Moodle named Exhibitionists
- **THEN** the summary presents three clearly separated, consistently capitalized actions and their readable targets without opaque IDs or raw serialization text

#### Scenario: Detail exceeds available row width
- **WHEN** a bundle contains more actions than fit comfortably on one row
- **THEN** the UI remains scannable using wrapping, a detail expansion, or an editor view rather than clipping or collapsing everything into an unreadable sentence
