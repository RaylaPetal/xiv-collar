## ADDED Requirements

### Requirement: Penumbra folder selection uses searchable multi-select dropdowns
Animation and restraint scan settings SHALL each present Penumbra sort folders through a compact searchable
multi-select dropdown consistent with Wardrobe's folder allowlist behavior. Selected folders SHALL remain
visible and removable without requiring exact path typing, and the control SHALL remain usable at the
minimum supported Settings width.

#### Scenario: Sub selects several folders
- **WHEN** the Sub opens either folder dropdown and chooses multiple entries
- **THEN** every chosen folder remains selected, visible, and individually removable after the dropdown closes

#### Scenario: Narrow Settings window
- **WHEN** Settings is displayed at its minimum supported width
- **THEN** folder names may truncate with tooltips but selection, removal, search, and scan controls remain reachable

### Requirement: Owner restraint catalog is grouped and searchable
The Owner restraint surface SHALL list imported Penumbra restraint mods once each, provide text search by
mod name, and expose an explicit choose → rule editing → save/favorite/copy/send flow
actions without requiring the Owner to manually type the Sub's mod names. Long labels SHALL wrap or
truncate with a tooltip rather than pushing action controls outside the window.

#### Scenario: Owner browses a large restraint catalog
- **WHEN** hundreds of restraint options were shared by the Sub
- **THEN** the Owner can narrow them by search and choose only the relevant mod

#### Scenario: Long restraint label in a narrow window
- **WHEN** a selected mod/group/option label exceeds the available width
- **THEN** the action controls remain visible and the full label is available through wrapped detail or a tooltip
