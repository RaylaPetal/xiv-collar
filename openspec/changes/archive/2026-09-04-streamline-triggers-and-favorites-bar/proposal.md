## Why

The Owner's "Alias / one-off" quick-command section is largely redundant: every single-action alias or Custom Trigger a Sub creates already has an equivalent category-specific slot in the Owner's DOM commands (Title, Outfit, Gesture, Moodles, Restraints), yet export/import always funnels it into one flat, undifferentiated Aliases bucket instead. Separately, the current favorites-access surface (a DTR click that opens a full ImGui "Favorites" window) is heavier than it needs to be for a quick-glance, quick-fire control - a compact, movable, Umbra-style menu button would fit the use case better and read as more native to the game's own UI conventions.

## What Changes

- **BREAKING**: Export/import routing changes - a Sub's single-action alias (Title/Outfit/Gesture/Restraint/Moodle alias, or a Custom Trigger bundling exactly one action) is exported and imported into its matching category's own quick-command list (Titles/Outfits/Gestures/Restraints/Moodles) instead of the generic Aliases section.
- The Owner's "Alias / one-off" section is narrowed to what has no single matching category: genuinely multi-action Custom Trigger bundles, and the Owner's own manually-typed freeform raw commands. It is relabeled to reflect that narrower purpose instead of implying it is the catch-all for every alias.
- Reset-imports and per-category "Clear all" continue to only remove import-sourced/scan-sourced entries respectively; entries that move from the old Aliases bucket into a category list are tracked as import-sourced so reset-imports still clears them without touching that category's manually-added or scanned entries.
- **BREAKING**: Replace the DTR-entry-opens-a-favorites-window flow with a compact, Umbra-style menu: a movable on-screen button (default bottom-right, repositionable via a setting) that opens a native-feeling select/dropdown menu (not an ImGui window) instead of toggling the `FavoritesWindow`.
- The dropdown lists favorites grouped by category (Title, Outfit, Gesture, Follow, Moodles, Restraints, Custom Trigger bundles); selecting a category opens a second-level select listing that category's favorited commands to fire.
- The dropdown includes a control to open the Owner tab directly, and a control to open the main plugin window, matching what the old favorites window already offered.
- **BREAKING**: The DTR (server info bar) "Collar" entry is removed outright rather than kept as a second way to open the menu - Dalamud can invoke a DTR click handler outside any valid ImGui frame, which crashed the game when the handler called ImGui popup APIs directly during testing. The on-screen button is the sole surface for this menu going forward.

## Capabilities

### New Capabilities
(none - this reshapes existing import/export and UI-surface behavior)

### Modified Capabilities
- `collar/catalog-sync`: Alias/Custom Trigger export and import routes single-action entries into their matching category's quick-command list instead of the generic Aliases section; reset-imports and dedup behavior extend to those categories' import-sourced entries.
- `collar/ui-organization`: The Owner's "Alias / one-off" section is narrowed to multi-action bundles and freeform commands; the favorites window and the DTR entry are both removed, replaced by a single movable, Umbra-style dropdown menu grouped by category with a second-level select and Owner-tab/main-window shortcuts.

## Impact

- `CollarSystem.Plugin/Commands/CatalogSyncService.cs`: `ExportAliasEntries`, `BuildExport`, `ParseImport`, `ImportAliasLines`, and the per-category import helpers change to route single-action aliases/triggers by category.
- `CollarSystem.Plugin/Config/PluginConfig.cs`: `QuickCommand` gains an import-source/category marker where needed so reset-imports can still target only import-sourced entries once they live in shared category lists.
- `CollarSystem.Plugin/Config/AliasBook.cs`: `CustomTriggerDefinition`/`CustomTriggerAction` need a single-action-vs-bundle distinction usable by export/import.
- `CollarSystem.Plugin/UI/CollarWindow.cs`: `DrawOwnerModule`, `DrawFreeformComposer`, `DrawImportCommandsButton`, and the "Alias / one-off" section's label/behavior.
- `CollarSystem.Plugin/UI/FavoritesWindow.cs`: removed, replaced by `CollarSystem.Plugin/UI/QuickAccessMenu.cs` (the popup) and `CollarSystem.Plugin/UI/FavoritesBarButton.cs` (the on-screen button).
- `CollarSystem.Plugin/Plugin.cs`: the DTR bar entry and `IDtrBar` service usage are removed entirely.
