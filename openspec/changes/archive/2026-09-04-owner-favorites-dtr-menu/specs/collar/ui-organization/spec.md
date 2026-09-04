## ADDED Requirements

### Requirement: A server-info-bar entry gives quick access to Owner commands
The system SHALL add an entry to FFXIV's own server info bar (Dalamud's DTR bar) that is visible regardless of the configured Role, matching every other Owner-facing surface in this plugin. Clicking the entry SHALL toggle a compact favorites window (see "Owner can favorite quick commands for quick access").

#### Scenario: DTR entry is visible regardless of Role
- **WHEN** the plugin is loaded, whether the local Role is configured as Sub or Owner
- **THEN** the server info bar entry is visible

#### Scenario: Clicking the DTR entry toggles the favorites window
- **WHEN** the user clicks the server info bar entry
- **THEN** the favorites window opens if it was closed, or closes if it was already open

### Requirement: Owner can favorite quick commands for quick access
The system SHALL let the Owner mark any saved quick command, in any of the seven categories (Title, Outfit, Gesture, Follow, Moodles, Restraints, Aliases), as a favorite via a toggle shown next to that entry in its normal quick-command list. Favorite state SHALL persist with the quick command and SHALL NOT affect its normal Send/Copy/Remove behavior in its own category's list.

#### Scenario: Owner favorites a quick command
- **WHEN** the Owner toggles the favorite control on a saved quick command
- **THEN** that command is marked as a favorite and persists as such across window sessions

#### Scenario: Un-favoriting removes it from the favorites window without deleting it
- **WHEN** the Owner un-favorites a previously favorited quick command
- **THEN** the command no longer appears in the favorites window but remains in its own category's quick-command list, unchanged otherwise

### Requirement: Compact favorites window lists only favorited commands
The favorites window SHALL list every currently favorited quick command across all seven categories, flat (not grouped by category), each with the same Send/Copy controls its normal list row already provides. The window SHALL include one control that opens the main window directly to the Owner tab, for any command not favorited. An empty favorites list SHALL be represented clearly, not as a blank window.

#### Scenario: Favorites window lists favorited commands from multiple categories
- **WHEN** the Owner has favorited commands from more than one category
- **THEN** the favorites window lists all of them together, each still sendable/copyable

#### Scenario: Opening the Owner tab from the favorites window
- **WHEN** the Owner clicks the favorites window's "Open Owner commands" control
- **THEN** the main window opens (or comes to focus) with the Owner tab selected

#### Scenario: No favorites yet
- **WHEN** the Owner opens the favorites window with no quick command currently favorited
- **THEN** the window explains that nothing is favorited yet and how to add one, rather than appearing empty with no explanation
