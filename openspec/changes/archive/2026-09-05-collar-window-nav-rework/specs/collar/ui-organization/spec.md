## REMOVED Requirements

### Requirement: Owner navigation is separated from Sub modules
**Reason**: Replaced by the shared, role-aware category-tab model - there is no longer a single dedicated Owner destination to separate from Sub modules, since Owner and Sub now share the same tab per category.
**Migration**: See the new "Category tabs present role-aware content" requirement.

### Requirement: Owner command categories are independently collapsible
**Reason**: The Owner command surface (one collapsible accordion holding every category) is replaced by one tab per category, matching the Sub-side tab-per-category model. Each category's existing add, import, compose, copy, and send operations are preserved, just relocated to that category's own tab instead of a collapsible section of a shared accordion.
**Migration**: See the new "Category tabs present role-aware content" requirement.

### Requirement: Owner Gesture quick-command list is grouped and searchable
**Reason**: Renamed to Animation (see "Animation category naming avoids ambiguity with vanilla gestures") and relocated from the Owner accordion to the shared Animation tab's Owner-role view.
**Migration**: See the new "Owner Animation quick-command list is grouped and searchable" requirement.

## ADDED Requirements

### Requirement: Category tabs present role-aware content
The main module navigation SHALL present one tab per shared gameplay category - Title, Outfit, Animation, Moodles, Restraints, Custom Trigger Bundles, Collar, and Follow / Leash - visible regardless of Role. Each shared category tab SHALL show that category's Sub-side alias-authoring controls when the local Role is Sub, and that category's Owner-side browse/send controls (preserving each category's existing add, import, compose, copy, and send operations) when the local Role is Owner. Switching Role SHALL change only which view a shared category tab renders - not which tab is currently selected, the saved data underlying either view, or any other tab's expanded/collapsed state. Each shared category's icon SHALL be the same regardless of which role's view is currently rendered.

#### Scenario: Owner opens a shared category tab
- **WHEN** the local Role is Owner and the Owner selects a shared category tab
- **THEN** that tab shows the category's browse/send controls, with the same add, import, compose, copy, and send operations it offered before this change

#### Scenario: Sub opens a shared category tab
- **WHEN** the local Role is Sub and the Sub selects a shared category tab
- **THEN** that tab shows the category's alias-authoring controls, unchanged from before this change

#### Scenario: Switching Role keeps the same tab selected
- **WHEN** a shared category tab is selected and the user changes Role in Settings
- **THEN** the same tab remains selected and its content switches to the new Role's view, with no data loss in either view

#### Scenario: Category icon is consistent across role views
- **WHEN** a shared category tab is viewed under either Role
- **THEN** the icon shown for that category is the same in both the Sub-role view and the Owner-role view

### Requirement: Collar and Follow/Leash are separate tabs
The system SHALL present Collar and Follow / Leash as two independent tabs rather than one combined tab, on both the Sub alias-authoring side and the Owner browse/send side - matching the already-separate Follow permission toggle in Permissions.

#### Scenario: Sub configures collar and leash independently
- **WHEN** the local Role is Sub
- **THEN** collar item/Moodle configuration appears on the Collar tab and leash trigger-word configuration appears on the Follow / Leash tab, each independent of the other

#### Scenario: Owner sends collar and leash commands from separate tabs
- **WHEN** the local Role is Owner
- **THEN** collar lock/unlock quick commands appear on the Collar tab and leash quick commands appear on the Follow / Leash tab, each independent of the other

### Requirement: Sync tab holds catalog relay sync and import/reset
The system SHALL present a Sync tab, visible regardless of Role, holding the catalog relay sync controls and the import/reset controls previously found at the top of the Owner accordion. When the local Role is Owner, these controls SHALL function exactly as they did before this change. When the local Role is Sub, the tab SHALL present every catalog-related action end to end: an explanation that relay sync itself is the Owner's action with nothing to click for it here; the per-category scanning controls (Wardrobe, Animation, Moodles, Restraints) and the "Scan all" convenience button, previously in Settings' "Scan & Export" card; and the offline/manual catalog export action, also previously in that card. Settings SHALL NOT retain a scanning section of its own once this moves - the "Catalog sync (relay)" Permissions toggle SHALL remain unaffected by this change.

#### Scenario: Owner uses the Sync tab
- **WHEN** the local Role is Owner and the Owner selects the Sync tab
- **THEN** catalog relay sync and import/reset behave exactly as they did in the former Owner accordion

#### Scenario: Sub scans and exports their catalog from the Sync tab
- **WHEN** the local Role is Sub and the Sub selects the Sync tab
- **THEN** the tab explains that relay sync is the Owner's action, offers the same per-category scan controls and "Scan all" button previously found in Settings, and offers an "Export..." control (disabled until at least one category has been scanned or a Restraints device tagged) that writes the same file Settings used to export

#### Scenario: Settings no longer has a scanning section
- **WHEN** a user opens Settings after this change
- **THEN** no "Scanning" tab or scan controls remain there - Identity & Pairing and ToS are the tabs that remain

#### Scenario: Reset imports also clears the browsable mod catalogs
- **WHEN** the Owner uses "Reset imports" on the Sync tab
- **THEN** the imported Animation and Restraints mod catalogs are cleared in addition to the imported quick-command lists, so a stale or duplicate entry from an earlier import cannot linger until the next one

### Requirement: An empty Owner quick-command list points to the Sync tab
When the local Role is Owner and a shared category's quick-command list is empty because nothing has been imported yet, the system SHALL offer a control that switches directly to the Sync tab, rather than only naming it in text - since Import commands no longer sits on the same tab the empty list is shown on.

#### Scenario: Owner follows the prompt from an empty category
- **WHEN** the local Role is Owner and the Outfit, Animation, or Moodles tab's quick-command list is empty
- **THEN** the tab shows a control that switches directly to the Sync tab when selected

### Requirement: Animation category naming avoids ambiguity with vanilla gestures
The system SHALL label the Gesture category as "Animation" in every user-facing surface that names the category - nav tab, module title, Owner section header, and the Permissions checkbox - reserving the word "gesture" for FFXIV's own built-in emotes as sent through a Custom Trigger's chat action, which shares no mechanism with the Animation category's Penumbra mod-swap behavior. This rename SHALL NOT change the underlying configuration field name, any C# identifier, or the `gesture` keyword used in the Owner's direct-override tell grammar (`ChatCommandListener.ReservedCategoryWords`), which SHALL remain unchanged so that two paired installs on different plugin versions continue to recognize each other's override tells.

#### Scenario: User views the category label
- **WHEN** a user views the category's nav tab, module title, Owner section header, or the Permissions checkbox
- **THEN** the label reads "Animation", not "Gesture"

#### Scenario: Direct-override tell grammar is unaffected
- **WHEN** an Owner sends a direct-override command using the reserved category word for this category
- **THEN** the wire keyword remains `gesture` and is recognized identically regardless of either side's plugin version

#### Scenario: A Custom Trigger's vanilla gesture is distinguishable
- **WHEN** a Custom Trigger's chat action sends a plain vanilla emote command (e.g. `/highfive`)
- **THEN** nothing in the UI describes that action as using the Animation category, since it uses a different mechanism entirely

### Requirement: Outfit tab naming matches its category
The system SHALL label the Sub-side Outfit category tab as "Outfit", matching its existing internal category naming (`OutfitCommand`, `QuickCommands.Outfits`, the `outfit` reserved word) and the Owner-side view's existing label, rather than "Wardrobe". Settings' "Wardrobe design allowlist & scan" section keeps its existing label - a distinct concept describing which Glamourer designs are eligible for scanning, not the category's name.

#### Scenario: Sub views the Outfit tab
- **WHEN** the local Role is Sub and the Sub views the category tab for outfits
- **THEN** the tab is labeled "Outfit"

#### Scenario: Settings' allowlist wording is unaffected
- **WHEN** a user views Settings' Scan & Export section
- **THEN** its "Wardrobe design allowlist & scan" wording is unchanged by this rename

### Requirement: Owner Animation quick-command list is grouped and searchable
The Owner-role view of the Animation tab's quick-command list SHALL present its entries grouped by their source mod and option group, with a text search control that filters visible entries by mod, group, animation, or trigger name - the same grouped/searchable presentation the Sub's animation picker window already provides. A flat, ungrouped scroll of every entry SHALL NOT be the list's only presentation once the list exceeds a small number of entries.

#### Scenario: Owner browses a large Animation quick-command list
- **WHEN** the Owner-role view of the Animation tab's quick-command list contains hundreds or thousands of entries
- **THEN** entries are shown grouped by mod and option group rather than as one flat scrolling list

#### Scenario: Owner searches the Animation quick-command list
- **WHEN** the Owner types text into the Animation tab's search control
- **THEN** only entries whose mod, group, animation, or trigger name matches the search text remain visible

#### Scenario: Owner clears the search
- **WHEN** the Owner clears the search text
- **THEN** every Animation quick-command entry becomes visible again, grouped as before

## MODIFIED Requirements

### Requirement: Clear all sits at the far right of its section's title row
Each quick-command section that offers a "Clear all" control (Outfit, Animation, Moodles, Restraints) SHALL place that control on the same row as the section's title, aligned to the far right, matching the placement already used elsewhere for a section title row.

#### Scenario: Owner views a quick-command section title row
- **WHEN** the Owner views the Outfit, Animation, Moodles, or Restraints quick-command section
- **THEN** "Clear all" appears on the title row, aligned to the far right, rather than as a separate line below the title

### Requirement: Reset-imports control sits next to Import
The Sync tab SHALL show a "Reset imports" control next to the existing "Import commands" control, distinct in label and effect from any single category's "Clear all" control.

#### Scenario: Owner locates the reset-imports control
- **WHEN** the Owner views the Sync tab's import controls
- **THEN** "Reset imports" is visible immediately next to "Import commands"

### Requirement: Owner can favorite quick commands for quick access
The system SHALL let the Owner mark any saved quick command, in any of the seven categories (Title, Outfit, Animation, Follow, Moodles, Restraints, Custom Trigger Bundles), as a favorite via a toggle shown next to that entry in its normal quick-command list. Favorite state SHALL persist with the quick command and SHALL NOT affect its normal Send/Copy/Remove behavior in its own category's list.

#### Scenario: Owner favorites a quick command
- **WHEN** the Owner toggles the favorite control on a saved quick command
- **THEN** that command is marked as a favorite and persists as such across window sessions

#### Scenario: Un-favoriting removes it from the favorites window without deleting it
- **WHEN** the Owner un-favorites a previously favorited quick command
- **THEN** the command no longer appears in the quick-access favorites menu but remains in its own category's quick-command list, unchanged otherwise

### Requirement: Compact favorites window lists only favorited commands
The system SHALL provide a quick-access dropdown menu (not an ImGui window). When the local Role is Owner, opening it SHALL list a top-level entry per category that currently has at least one favorited command; selecting a category's top-level entry SHALL open a second-level select listing that category's favorited commands, each sendable with the same effect its normal list row's Send control already provides, and an empty favorites list SHALL be represented clearly rather than as an empty or absent menu. When the local Role is Sub, the menu SHALL NOT list favorited commands at all, since a Sub-configured character has no paired Sub of its own to send them to. Regardless of Role, the menu SHALL always include a control that opens the main plugin window and a control that opens Settings; opening the main window SHALL suffice to reach Owner content directly, since every shared category tab already renders its Owner-role view whenever Role is Owner, so no separate "Open Owner commands" control is needed.

#### Scenario: Favorites window lists favorited commands from multiple categories
- **WHEN** the Role is Owner and the Owner has favorited commands from more than one category
- **THEN** the quick-access menu's top level lists each of those categories, and selecting one opens a second-level select of that category's favorited commands

#### Scenario: Firing a favorite from the second-level select
- **WHEN** the Owner selects a favorited command from a category's second-level select
- **THEN** that command is sent with the same effect as sending it from its normal quick-command list row

#### Scenario: Opening the main window from the quick-access menu
- **WHEN** a user of either Role selects the quick-access menu's "Open main window" control
- **THEN** the main plugin window opens (or comes to focus); if Role is Owner, it already shows Owner-role views on every shared category tab wherever it was left, with no separate "Open Owner commands" control needed to reach them

#### Scenario: Opening the Owner tab from the favorites window
- **WHEN** the Role is Owner and the Owner looks for a dedicated control to jump straight to Owner commands
- **THEN** no separate "Open Owner commands" control exists in the quick-access menu - the plain "Open main window" control already opens directly to Owner-role views on every shared category tab, since there is no longer one dedicated Owner tab to jump to

#### Scenario: Opening Settings from the quick-access menu
- **WHEN** a user of either Role selects the quick-access menu's "Open settings" control
- **THEN** the Settings window opens (or comes to focus)

#### Scenario: No favorites yet
- **WHEN** the Role is Owner and the Owner opens the quick-access menu with no quick command currently favorited
- **THEN** the menu shows "Nothing favorited yet" rather than appearing empty with no explanation

#### Scenario: Sub sees only the plain open-window shortcuts
- **WHEN** the Role is Sub and the Sub opens the quick-access menu
- **THEN** the menu shows only "Open main window" and "Open settings" - no favorites list
