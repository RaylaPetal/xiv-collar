## Context

The plugin's identity is currently the single string "Collar System," used both as the Dalamud-facing plugin name (`<Name>` in `CollarSystem.Plugin.csproj`, `repo.json`) and, because InternalName/assembly name/namespace are all filename-derived from the csproj (`CollarSystem.Plugin/CollarSystem.Plugin.csproj`), baked into every C# file's `namespace` declaration and the built DLL name. See proposal.md - Why and Impact.

## Goals / Non-Goals

**Goals:**
- Give the plugin its own name, "Oathbound," at every level: display branding, InternalName, assembly, namespaces, repo URL, and primary slash commands.
- Keep existing users' macros/keybinds on `/collar`, `/collarpanic`, `/collarsettings` working via aliases.
- Keep the in-universe "collar" roleplay terminology (feature copy, class names like `CollarWindow`) untouched, since it names the mechanic, not the product.

**Non-Goals:**
- Not an in-place Dalamud update for existing installs - InternalName changes are inherently a fresh-install event; this design does not attempt to work around that.
- Not renaming any collar-domain class, config key, or UI copy that refers to the roleplay collar mechanic.
- Not deciding the exact commit/PR sequencing for the GitHub repo rename beyond flagging it as a coordinated, manually-confirmed operational step.

## Decisions

### Namespace rename is a mechanical top-to-bottom find/replace, done via the .csproj/folder rename first
Rename `CollarSystem.Plugin/` -> `Oathbound.Plugin/`, `CollarSystem.Plugin.csproj` -> `Oathbound.Plugin.csproj`, `CollarSystem.slnx` -> `Oathbound.slnx`, then replace `namespace CollarSystem.Plugin` (and its `.UI`/`.Commands`/`.Config`/`.Ipc`/`.Safety` sub-namespaces) with `namespace Oathbound.Plugin[...]` and matching `using` directives across all `.cs` files. Because no `<AssemblyName>`/`<RootNamespace>` overrides exist today, the folder/csproj rename alone changes InternalName/assembly name; the namespace edit is a separate, purely-mechanical pass verified by a full build.

**Alternative considered**: keep the csproj/folder/namespace as `CollarSystem.Plugin` and only override `<AssemblyName>`/InternalName explicitly. Rejected - the user's chosen "full structural rename" scope explicitly wants the project identity itself renamed, not just an override layered on top of a stale name.

### Command rename: new primary names, old names kept as aliases, `/ob` as shorthand
`Plugin.cs`'s `CommandName`/`PanicCommandName`/`SettingsCommandName` constants become the new primary values (`/oathbound`, `/oathboundpanic`, `/oathboundsettings`). Each is registered via Dalamud's `ICommandManager` as today; the old `/collar`, `/collarpanic`, `/collarsettings` strings are registered as additional commands pointing at the exact same handler delegates (no behavior duplication - same handler, multiple registered trigger strings), so both old and new users' bindings work identically. `/ob` is registered the same way as an extra alias for the main `/oathbound` command only (not for panic/settings, which are lower-frequency and already have full mnemonic names).

**Alternative considered**: make `/collar*` commands print a deprecation notice and forward. Rejected as unnecessary friction - a silent, fully-equivalent alias costs nothing at runtime and matches how the proposal frames this (avoid breaking existing macros), while help text/README lead with the new names so new users learn the current convention.

### Display strings updated directly, in place
Window titles (`CollarWindow.cs`, `SettingsWindow.cs`, `FavoritesWindow.cs`), the DTR tooltip, `repo.json`'s `Name`, and the csproj's `<Name>` are literal string edits - no abstraction needed since these are set once each at a single call site.

### GitHub repo rename is sequenced, not automated by the apply step
Renaming `RaylaPetal/xiv-collar` on GitHub is a one-way, high-blast-radius operational action (existing clone URLs, forks, and issue/PR links depend on GitHub's redirect holding). This design treats it as a manually-confirmed step coordinated with, but not silently performed as part of, the code changes: the code changes (repo.json `RepoUrl`/`DownloadLink*`, csproj `<RepoUrl>`) are prepared to point at the final URL, and the actual `gh repo rename` (or GitHub UI rename) is called out as its own task requiring explicit user confirmation at apply time.

## Risks / Trade-offs

- [InternalName change drops existing users from Dalamud's installed-plugins list] → Unavoidable given the full-rename scope the user chose; mitigate by clear README/release-notes messaging ("reinstall from the repo listing") and by keeping `/collar*` command aliases so muscle memory still works once reinstalled.
- [Namespace rename touches every source file] → Low functional risk (pure rename, not logic change) but mechanical; verify via a full solution build after the rename, not just a partial grep-and-replace.
- [GitHub repo rename breaks the manually-maintained `RepoUrl` field in repo.json/csproj if sequenced wrong] → Update `RepoUrl`/`DownloadLink*` in the same change/PR that performs the GitHub rename, and confirm the rename with the user before executing it (per this project's guardrails on hard-to-reverse, externally-visible actions).
- [`/ob` colliding with another plugin's command] → Low risk (Dalamud commands are per-plugin-registered, not globally reserved), but verify no existing `/ob` conflict during manual testing.

## Migration Plan

- No data/config migration needed - `PluginConfig` and all persisted state are unaffected by this rename (only namespace/assembly/display identity changes, not config schema).
- Release notes/README explicitly instruct existing users to remove the old "Collar System" entry (if Dalamud shows it as broken/missing) and install "Oathbound" fresh from the updated repo listing.
- Rollback: since this is a rename with alias commands preserved, reverting is a straightforward revert of the same commits; no forward-only data changes are introduced.
