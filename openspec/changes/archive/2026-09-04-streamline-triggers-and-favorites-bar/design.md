## Context

Today, single-action aliases (`TitleAliasDefinition`, `OutfitAliasDefinition`, `GestureAliasDefinition`, `RestraintAliasDefinition`, `MoodleAliasDefinition`) and every Custom Trigger - single- or multi-action alike - all funnel through `CatalogSyncService.ExportAliasEntries`/`ImportAliasLines` into one flat `QuickCommands.Aliases` list, rendered as the Owner's "Alias / one-off" section (`CollarWindow.DrawFreeformComposer`). The Owner's other six categories (`Titles`, `Outfits`, `Gestures`, `Follow`, `Moodles`, `Restraints`) are separate `List<QuickCommand>` fields populated by their own scan/import/manual paths and rendered by `DrawOwnerSection`/`DrawSavedQuickRow`. See proposal.md - Why.

Favorites access today is `FavoritesWindow` (a normal ImGui window toggled by the DTR entry), flattening all seven `QuickCommand` lists by their `IsFavorite` flag.

**Post-implementation update**: in-game testing surfaced that routing the quick-access menu's open/close through the DTR entry's click handler crashed the game - Dalamud invokes that handler outside any valid ImGui frame, and it was calling ImGui popup APIs (`OpenPopup`/`IsPopupOpen`) directly, which dereferences invalid internal ImGui state with no current window. Rather than keep the DTR entry and route its click through a safer indirection, it was removed outright - the on-screen button is the sole surface for this menu now (see the ADDED/REMOVED requirements in the `collar/ui-organization` delta spec, and the `QuickAccessMenu`/`FavoritesBarButton` Decisions below, updated to match).

## Goals / Non-Goals

**Goals:**
- Route single-action aliases/triggers into their matching category's existing `QuickCommand` list and UI section instead of a generic bucket.
- Let reset-imports still cleanly remove only import-sourced entries once a category list mixes scanned, manually-added, and import-sourced entries.
- Replace the favorites window with a lightweight popup menu reachable from both the DTR entry and a new movable on-screen button.

**Non-Goals:**
- No dependency on the actual Umbra plugin or its code - it is a visual/UX reference only (a compact, movable, non-window bottom-of-screen control). Nothing here requires Umbra to be installed.
- No change to how Custom Triggers are authored on the Sub side, or to the wire protocol used when an Owner triggers an alias/bundle live - this only changes the out-of-band export/import file and the Owner-side quick-command UI/access surface.
- Not attempting drag-to-reposition on the game HUD directly; the button's position is set via a Settings control (e.g. an anchor/offset picker or preset positions), not free-form in-world dragging.

## Decisions

### Single-action vs. bundle distinction travels with the Custom Trigger
`CustomTriggerDefinition.Actions.Count == 1` is the existing, sufficient signal - no new field needed on `CustomTriggerDefinition` itself. Export/import code branches on this count to decide whether an entry's summary line goes into its single action's category section or the Custom Trigger Bundle section. The five existing single-category alias definition types (`TitleAliasDefinition`, etc.) are unambiguous by construction and always route to their own category.

**Alternative considered**: tag every alias/trigger with an explicit `TargetCategory` at creation time. Rejected - redundant with data already implied by the action list, and adds a field that could drift out of sync with the actual bundled actions.

### Import provenance via a per-entry source flag on `QuickCommand`
Add an `ImportSource` enum/flag to `QuickCommand`: `Manual`, `Imported` - not a three-state `Manual`/`Scanned`/`Imported` as first considered, since only the Sub scans (never the Owner); every populated `OwnerQuickCommands` entry is either typed by the Owner or came from a Sub-exported file via "Import commands", so two states fully cover it. Reset-imports filters each category list (`Titles`, `Outfits`, `Gestures`, `Moodles`, `Restraints`) down to entries not flagged `Imported`, instead of clearing/not-clearing the whole list. The Custom Trigger Bundle list keeps today's coarse whole-list clear (already specced as acceptable, matching Restraints' existing manual-entry behavior) since it wasn't split further.

**Alternative considered**: keep imported aliases in a separate shadow list per category and merge for display. Rejected - doubles the state to keep in sync with Send/Copy/Remove/Favorite and re-introduces exactly the kind of parallel-list bookkeeping this change is trying to remove.

### Quick-access menu is an ImGui popup, not a window
The on-screen button opens an `ImGui.OpenPopup`-driven menu (borderless, no titlebar, dismiss-on-click-away) rather than a `FavoritesWindow`-style window. This gets the "feels like a menu, not a window" quality the proposal asks for without a new UI framework dependency. The on-screen button itself is a small always-on-top overlay drawn via ImGui with `NoTitleBar | NoResize | NoScrollbar | NoBackground`-style flags (`ImGui.SetNextWindowPos` pinned each frame) so it reads as a floating button rather than a draggable pane, positioned from the persisted Settings value.

Public entry points into `QuickAccessMenu` (`Toggle()`) never call an ImGui popup API directly - only a plain bool flag is flipped there. Every real `OpenPopup`/`BeginPopup`/`CloseCurrentPopup` call happens inside `QuickAccessMenu.Draw()`, which `FavoritesBarButton.Draw()` calls unconditionally every frame. This is why the DTR entry could be dropped without losing anything: `Toggle()` was already required to be safely callable from an arbitrary, possibly-frame-less context (that's what caused the original crash - `IsPopupOpen`/`OpenPopup` called from the DTR click callback, which Dalamud can invoke with no valid "current ImGui window", dereferences invalid internal state) - but since the button is now the only caller and it always calls from inside a valid Draw(), the indirection is kept anyway for defense-in-depth, not because another external caller still needs it.

The popup is explicitly positioned via `FavoritesBarButton.ComputePosition`, pivoted so it grows away from whichever screen edges the button sits against, rather than relying on Dear ImGui's default mouse-position popup heuristic (which put it in the wrong place, e.g. top of screen for a bottom-anchored button, before this was made explicit).

**Alternative considered**: a real Dalamud/ImGui "window" for the button that the user drags directly. Rejected for v1 - a Settings-driven position (with sensible presets, e.g. corners + custom offset) is simpler to persist and validate than free drag state, and matches how the DTR bar itself is already positioned by Dalamud, not dragged by this plugin.

### Two-level popup structure
Top level: one row per category that currently has ≥1 favorite, plus "Open Owner commands" and "Open main window". Selecting a category row opens a nested popup/submenu (ImGui's `BeginMenu`-style nesting) listing that category's favorited entries with Send. This avoids one long flat list once favorites accumulate across categories, per proposal.md.

## Risks / Trade-offs

- [Existing saved exports/imports from before this change carry the old flat Aliases format] → Import parsing stays backward-compatible: an old-format `## ALIASES` section (or old-style entries) is still recognized as before and treated as multi-action-only content is empty; this is a read-time compatibility shim in the parser, not a stored migration.
- [Splitting one list into per-category import-sourced subsets could reclassify entries a Sub exports where the same alias word appears in two categories] → Dedup stays scoped per-category (as it already is per list today), so a collision across categories is not possible to introduce by this change; document this in the parser's dedup key (category + command).
- [A screen-anchored overlay button can overlap other HUD/plugin elements at some resolutions] → Ship a small set of anchor presets (each screen corner) plus a numeric offset, rather than fully free placement, to keep collisions easy to reason about and adjust.

## Migration Plan

- Existing `QuickCommand` entries in `Titles`/`Outfits`/`Gestures`/`Moodles`/`Restraints` default `ImportSource` to `Manual` (or `Scanned` where a scan-provenance flag already exists) on config load, so nothing already saved is wrongly swept by the new per-category reset-imports filtering.
- Existing `Aliases` entries are left as-is (now serving the narrower Custom Trigger Bundle role); no forced migration of already-imported single-action aliases into their category lists is required - they simply stop being added there and start landing correctly on the next import.
- `FavoritesWindow` is removed once the popup menu covers its scenarios; no persisted state to migrate since favorite status already lives on each `QuickCommand`.
