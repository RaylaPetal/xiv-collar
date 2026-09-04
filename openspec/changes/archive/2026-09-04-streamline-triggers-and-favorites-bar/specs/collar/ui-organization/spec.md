## MODIFIED Requirements

### Requirement: Owner command categories are independently collapsible
The Owner command surface SHALL present each supported command category in its own labeled collapsible section, preserving the category's existing add, import, compose, copy, and send operations. Expanding or collapsing one category SHALL NOT change the saved commands or the expanded state of other categories during the same window session. The category set SHALL be title, outfit, gesture, leash, Moodles, restraints, and Custom Trigger Bundles - the last covering only multi-action bundles and manually-typed freeform commands, since single-action aliases now live in their own matching category (see `collar/catalog-sync`).

#### Scenario: Owner opens command surface
- **WHEN** the Owner command surface contains title, outfit, gesture, leash, Moodles, restraints, and Custom Trigger Bundle controls
- **THEN** each category is identifiable and can be expanded or collapsed independently

#### Scenario: Owner collapses a category
- **WHEN** the user collapses a category containing saved commands
- **THEN** its detailed controls and rows are hidden without deleting or changing any saved command

### Requirement: Owner can favorite quick commands for quick access
The system SHALL let the Owner mark any saved quick command, in any of the seven categories (Title, Outfit, Gesture, Follow, Moodles, Restraints, Custom Trigger Bundles), as a favorite via a toggle shown next to that entry in its normal quick-command list. Favorite state SHALL persist with the quick command and SHALL NOT affect its normal Send/Copy/Remove behavior in its own category's list.

#### Scenario: Owner favorites a quick command
- **WHEN** the Owner toggles the favorite control on a saved quick command
- **THEN** that command is marked as a favorite and persists as such across window sessions

#### Scenario: Un-favoriting removes it from the favorites window without deleting it
- **WHEN** the Owner un-favorites a previously favorited quick command
- **THEN** the command no longer appears in the quick-access favorites menu but remains in its own category's quick-command list, unchanged otherwise

### Requirement: Compact favorites window lists only favorited commands
The system SHALL replace the compact favorites window with a quick-access dropdown menu (not an ImGui window). When the local Role is Owner, opening it SHALL list a top-level entry per category that currently has at least one favorited command, plus a control that opens the main window directly to the Owner tab; selecting a category's top-level entry SHALL open a second-level select listing that category's favorited commands, each sendable with the same effect its normal list row's Send control already provides, and an empty favorites list SHALL be represented clearly rather than as an empty or absent menu. When the local Role is Sub, the menu SHALL NOT list favorited commands or an "Open Owner commands" control at all, since a Sub-configured character has no paired Sub of its own to send them to. Regardless of Role, the menu SHALL always include a control that opens the main plugin window and a control that opens Settings.

#### Scenario: Favorites window lists favorited commands from multiple categories
- **WHEN** the Role is Owner and the Owner has favorited commands from more than one category
- **THEN** the quick-access menu's top level lists each of those categories, and selecting one opens a second-level select of that category's favorited commands

#### Scenario: Firing a favorite from the second-level select
- **WHEN** the Owner selects a favorited command from a category's second-level select
- **THEN** that command is sent with the same effect as sending it from its normal quick-command list row

#### Scenario: Opening the Owner tab from the favorites window
- **WHEN** the Role is Owner and the Owner selects the quick-access menu's "Open Owner commands" control
- **THEN** the main window opens (or comes to focus) with the Owner tab selected

#### Scenario: Opening the main window from the quick-access menu
- **WHEN** a user of either Role selects the quick-access menu's "Open main window" control
- **THEN** the main plugin window opens (or comes to focus)

#### Scenario: Opening Settings from the quick-access menu
- **WHEN** a user of either Role selects the quick-access menu's "Open settings" control
- **THEN** the Settings window opens (or comes to focus)

#### Scenario: No favorites yet
- **WHEN** the Role is Owner and the Owner opens the quick-access menu with no quick command currently favorited
- **THEN** the menu shows "Nothing favorited yet" rather than appearing empty with no explanation

#### Scenario: Sub sees only the plain open-window shortcuts
- **WHEN** the Role is Sub and the Sub opens the quick-access menu
- **THEN** the menu shows only "Open main window" and "Open settings" - no favorites list and no "Open Owner commands" control

## REMOVED Requirements

### Requirement: A server-info-bar entry gives quick access to Owner commands
**Reason**: The DTR (server info bar) entry crashed the game when clicked - Dalamud can invoke a DTR click handler outside any valid ImGui frame, and the handler was calling ImGui popup APIs directly. Rather than route the fix through Dalamud's DTR click lifecycle, the on-screen `FavoritesBarButton` (see "A movable on-screen button opens the quick-access favorites menu") now covers this access point on its own, so the DTR entry is removed outright instead of kept as a second, redundant way to open the same menu.
**Migration**: None - no persisted state was tied to the DTR entry. Anyone who relied on the "Collar" server info bar entry now uses the on-screen quick-access button instead (repositionable in Settings).

## ADDED Requirements

### Requirement: A movable on-screen button opens the quick-access favorites menu
The system SHALL show a compact, movable on-screen button - styled to sit unobtrusively over the game UI rather than as a native ImGui window titlebar/frame - that opens the quick-access favorites menu, and is the sole surface for doing so (no server-info-bar entry). Its position SHALL default to the bottom-right of the screen and SHALL be configurable via a Settings control, persisting across sessions.

#### Scenario: On-screen button opens the quick-access menu
- **WHEN** the Owner clicks the on-screen favorites button
- **THEN** the quick-access favorites menu opens at the button's location, or closes if it was already open

#### Scenario: Button position is repositionable
- **WHEN** the Owner changes the favorites button's position in Settings
- **THEN** the button renders at the new position and continues doing so across subsequent plugin sessions

#### Scenario: Button defaults to bottom-right
- **WHEN** the Owner has never configured a favorites button position
- **THEN** the button renders at the bottom-right of the screen
