## Why

The plugin has no proper standalone name today - it's referred to only as "Collar System," which is also the exact phrase used for the in-universe collaring mechanic itself. Giving the plugin its own identity, "Oathbound," separates the product's branding from the roleplay feature it implements, and gives it a name fit for the Dalamud plugin listing, README, and community references.

## What Changes

- **BREAKING**: Full structural rename of the plugin project: `CollarSystem.Plugin/CollarSystem.Plugin.csproj` and its containing folder become `Oathbound.Plugin/Oathbound.Plugin.csproj`; `CollarSystem.slnx` becomes `Oathbound.slnx`; every C# `namespace CollarSystem.Plugin*` becomes `namespace Oathbound.Plugin*` across all source files.
- **BREAKING**: The Dalamud plugin's `InternalName` (filename-derived, currently `CollarSystem.Plugin`) becomes `Oathbound.Plugin`. Because Dalamud tracks installed plugins by InternalName, existing users will see the old plugin as removed and need to install "Oathbound" fresh from the updated repo listing - this is an unavoidable one-time break for existing installs, not an in-place upgrade.
- Display branding updates to "Oathbound" everywhere it currently reads "Collar System" as the plugin's own name: the `<Name>` in the csproj, `repo.json`'s `Name` field, the main window title bar, Settings window title, favorites window/menu title, the DTR bar tooltip, and README.
- CI workflow files (`release.yml`, `pr-build.yml`) update their hardcoded `CollarSystem.Plugin` paths/solution name to the new project name.
- `repo.json` and the csproj's `<RepoUrl>` update to the renamed GitHub repository's URL (see Impact - the GitHub repo itself is renamed as part of this change, which is an operational step handled outside code review, not a file edit).
- New primary slash commands are introduced: `/oathbound` (replacing `/collar` as primary), `/oathboundpanic` (replacing `/collarpanic`), `/oathboundsettings` (replacing `/collarsettings`), plus a `/ob` shorthand alias for the main command. The existing `/collar`, `/collarpanic`, and `/collarsettings` commands are kept registered as backward-compatible aliases so existing user macros/keybinds continue to work; all in-app help text and README instructions are updated to lead with the new names.
- The in-universe "collar" roleplay terminology - the collaring feature itself, its tab/checkbox/quick-command labels, and internal class names like `CollarWindow`/`CollarCommand`/`PluginConfig` - is explicitly NOT renamed. Only the plugin's own product identity changes.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `collar/pairing`: the safeword-panic requirement's reference to `/collarpanic` is updated to name `/oathboundpanic` as the primary invocation, with `/collarpanic` noted as a still-working alias.
- `collar/ui-organization`: Settings' safeword explanation text updates to reference `/oathboundpanic` as the primary command name, with `/collarpanic` noted as a still-working alias.

## Impact

- `CollarSystem.Plugin/` -> `Oathbound.Plugin/` (folder + csproj rename), all `.cs` files' `namespace` declarations, `CollarSystem.slnx` -> `Oathbound.slnx`.
- `CollarSystem.Plugin/Plugin.cs`: `CommandName`, `PanicCommandName`, `SettingsCommandName` constants, new `/ob` alias registration, DTR tooltip text, `WindowSystem` key.
- `CollarSystem.Plugin/UI/CollarWindow.cs`, `SettingsWindow.cs`, `FavoritesWindow.cs`: window title strings.
- `repo.json`, `CollarSystem.Plugin.csproj`'s `<Name>`/`<RepoUrl>`.
- `.github/workflows/release.yml`, `.github/workflows/pr-build.yml`: hardcoded project path/solution name.
- `README.md`: title, branding text, command references, activation instructions.
- The GitHub repository `RaylaPetal/xiv-collar` is renamed (operational action, coordinated with the code changes so `repo.json`/`RepoUrl` point at the correct final URL).
