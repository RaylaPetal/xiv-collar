## Why

Sending an Owner command today always requires opening the full main window and navigating to the Owner tab - there's no quick, native-feeling way to fire a favorite command (or jump straight to the full Owner tab) without that detour. Other Dalamud plugins (e.g. Aetherphone, confirmed by inspecting its own DLL in this environment) solve exactly this with an entry in FFXIV's own server info bar ("DTR bar"), via Dalamud's officially-supported `IDtrBar`/`IDtrBarEntry` API - a `Text`/`Tooltip` label with an `OnClick` handler, rendered natively alongside the clock/world/FPS indicators, not a raw `AtkResNode` modification (a much more fragile, version-specific approach with nothing like it anywhere in this codebase to build on, and not what Aetherphone itself actually uses).

## What Changes

- **A new DTR bar entry** ("Collar") sits in the server info bar, always visible regardless of Role (matching this plugin's existing "Role never hides UI" convention). Clicking it toggles a new compact favorites window.
- **A favoriting system**: every Owner quick-command entry, across all seven categories (Title, Outfit, Gesture, Follow, Moodles, Restraints, Aliases), gains a star toggle next to it. Six of the seven already share one row-drawing method (`DrawSavedQuickRow`); Restraints' own row editor (`DrawRestraintQuickRow`) gets the same toggle separately. Favorite state is a plain persisted flag per `QuickCommand` entry - no new list, no separate management UI.
- **A new compact favorites window**, opened by the DTR entry, lists only favorited commands (flat, with Send/Copy, matching this plugin's existing per-row controls) and includes one button that opens the main window directly to the Owner tab, for anything not favorited.
- **Scope decision, recorded not silently absorbed**: this is a small toggleable window (like `ItemPickerWindow`/`AnimationPickerWindow` already are), not a true auto-dismissing native dropdown - FFXIV's own dropdown-close-on-outside-click behavior isn't something Dalamud's `Window` system replicates, and building a raw `ImGui.BeginPopup`-based transient popup triggered from a DTR click (which fires outside the normal per-frame `Window.Draw` flow) would be a new, more fragile UI pattern this codebase doesn't use anywhere else. The window closes via its own control or clicking the DTR entry again, the same as any other window here.

## Capabilities

### Modified Capabilities
- `collar/ui-organization`: adds the DTR bar entry, the favoriting system, and the compact favorites window as new discoverability/compactness features alongside this capability's existing ones.

## Impact

- `CollarSystem.Plugin/Plugin.cs` - new `[PluginService] internal static IDtrBar DtrBar` registration; creates and disposes a `Dtr Bar` entry (`DtrBar.Get("Collar")`) wired to toggle the new favorites window; `CollarWindow` gains a method to open directly to the Owner tab.
- `CollarSystem.Plugin/Config/PluginConfig.cs` - `QuickCommand` gains an `IsFavorite` bool.
- `CollarSystem.Plugin/UI/CollarWindow.cs` - `DrawSavedQuickRow` and `DrawRestraintQuickRow` each gain a star toggle button.
- `CollarSystem.Plugin/UI/FavoritesWindow.cs` (new) - the compact window listing every favorited `QuickCommand` across all seven lists, with Send/Copy per entry and one "Open Owner commands" button.
- **No breaking change**: purely additive - `IsFavorite` defaults to false for every existing saved quick command, so nothing currently saved changes behavior; the DTR entry and favorites window are new UI surfaces with no effect on anything that doesn't opt in by starring something.
