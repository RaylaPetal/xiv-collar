## Why

`CollarWindow`'s 9-tab navbar gives 8 tabs to Sub-side alias authoring (scanning this client's own Penumbra/Glamourer/Honorific/Moodles setup) and exactly 1 tab ("Owner") to everything an Owner actually does - every category's send/browse controls, catalog sync, and import/reset all crammed into one collapsible-accordion surface. The visual weight is inverted from per-role usefulness, and the "Gesture" category name already conflicts with itself in the code (`SettingsWindow.cs`'s own scan section calls it "Animation mods to scan" while the tab, module, and Owner accordion still say "Gesture") - it's also genuinely ambiguous next to a Custom Trigger's chat action, which can send a plain vanilla emote like `/highfive` with no relation to the Gesture category's Penumbra-mod-swap mechanism at all.

## What Changes

- Restructure `CollarWindow`'s navigation from "8 Sub tabs + 1 Owner tab" to one shared set of category tabs whose content is role-aware: the same tab shows the Sub's alias-authoring controls when Role is Sub, and the Owner's browse/send controls for that category when Role is Owner.
- Split the current "Collar" tab into two tabs, "Collar" and "Follow / Leash" - matching the Owner accordion's existing separation and the already-separate Follow permission toggle, rather than keeping them bundled.
- Add a new "Sync" tab holding catalog relay sync and import/reset (currently top-of-accordion in the Owner tab) - these have no Sub-side counterpart, so they don't fit the shared-category pattern; visible to both roles, active for Owner, informational for Sub.
- Rename the "Gesture" category's user-facing label to "Animation" everywhere it names the category (nav tab, module title, Owner section header, Permissions checkbox) - **not** the underlying config field, C# identifiers, or the `gesture` keyword in the Owner's direct-override tell grammar (`ChatCommandListener.ReservedCategoryWords`), which must stay as-is for wire compatibility with already-paired installs on either plugin version.
- Rename the "Wardrobe" nav tab's label to "Outfit", matching the category's existing internal naming everywhere else (`OutfitCommand`, `QuickCommands.Outfits`, the `outfit` reserved word, and the Owner accordion's existing "Outfit" header) - Settings' unrelated "Wardrobe design allowlist & scan" section keeps its own wording, a different concept (which Glamourer designs are eligible for scanning).
- Fix the Owner Moodles quick-command section's icon, which currently reuses Gesture's `TheaterMasks` icon instead of the nav tab's own `Smile` icon.
- Remove the favorites quick-access menu's "Open Owner commands" shortcut - it opened the main window directly to the old dedicated Owner tab, which no longer exists once Owner content moves into the shared category tabs; the existing plain "Open main window" control already opens to the same, now-Owner-flavored, content.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `collar/ui-organization`: replaces "Owner navigation is separated from Sub modules" and "Owner command categories are independently collapsible" with the shared, role-aware category-tab model (including the Collar/Follow split and the new Sync tab); updates the Gesture-quick-list, clear-all, and reset-imports requirements for the new tab locations and the Animation rename; updates the favorites-menu requirements to drop the now-redundant "Open Owner commands" shortcut.

## Impact

- `Oathbound.Plugin/UI/CollarWindow.cs` - `NavItems`, the `Draw()` switch, every `Draw*Module`/`Draw*QuickSection` pair (these become one role-aware method per category instead of a separate Sub module and Owner accordion section), `DrawOwnerModule`'s removal, new Sync tab content.
- `Oathbound.Plugin/UI/FavoritesBarButton.cs` / the quick-access menu code - removing the "Open Owner commands" entry.
- No `Oathbound.Plugin/Config/` or `Oathbound.Plugin/Commands/` changes to data shape - `QuickCommands`, `AliasBook`, and `ChatCommandListener.ReservedCategoryWords` are unaffected; this is a UI-layer reorganization and relabeling only.
