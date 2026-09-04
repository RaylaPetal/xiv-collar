## 1. Project/folder/namespace rename

- [x] 1.1 Rename `CollarSystem.Plugin/` folder and `CollarSystem.Plugin.csproj` to `Oathbound.Plugin/` and `Oathbound.Plugin.csproj`, and verify the solution still references the new path
- [x] 1.2 Rename `CollarSystem.slnx` to `Oathbound.slnx` and update its project reference, and verify `dotnet build` (or equivalent) resolves the solution
- [x] 1.3 Replace every `namespace CollarSystem.Plugin*` and matching `using CollarSystem.Plugin*` across all `.cs` files with `Oathbound.Plugin*`, and verify with a full solution build that compiles with zero errors
- [x] 1.4 Update `.github/workflows/release.yml` and `.github/workflows/pr-build.yml` hardcoded `CollarSystem.Plugin` paths/solution name to `Oathbound.Plugin`, and verify by running the workflow (or a local dry run of the build steps) successfully - verified via `sed` replacement + manual review (no local dry-run harness for GitHub Actions in this environment)

## 2. Plugin identity and branding strings

- [x] 2.1 Update `<Name>` in `Oathbound.Plugin.csproj` to "Oathbound", and verify the built manifest JSON reflects the new display name - confirmed via built manifest JSON: `"Name": "Oathbound"`, `"InternalName": "Oathbound.Plugin"`
- [x] 2.2 Update `repo.json`'s `Name` field to "Oathbound" (leave `InternalName` in sync with the renamed csproj/assembly from Task 1), and verify against the repo.json schema/lint if one exists - `InternalName` also updated to `Oathbound.Plugin` to match; no lint exists, verified by reading the JSON
- [x] 2.3 Update window title strings in `CollarWindow.cs`, `SettingsWindow.cs`, `FavoritesWindow.cs` (or its replacement popup menu, if `streamline-triggers-and-favorites-bar` lands first) from "Collar System"/"Collar - Settings"/"Collar Favorites" to their Oathbound-branded equivalents, and verify by opening each window in-game/dev harness - `FavoritesWindow.cs` no longer exists (removed by `streamline-triggers-and-favorites-bar`); its replacement (`QuickAccessMenu`/`FavoritesBarButton`) has no title bar text to rename. Verified via build only, not in-game (no game client in this environment)
- [x] 2.4 Update the DTR bar tooltip text in `Plugin.cs` and the `WindowSystem` key, and verify the tooltip renders the new text on hover - the DTR entry itself was removed entirely by `streamline-triggers-and-favorites-bar`, so there's no tooltip left to update; `WindowSystem` key updated to "Oathbound". Not verified in-game
- [x] 2.5 Update `README.md` title and branding references from "Collar System" to "Oathbound", and verify by rendering/reading the README - also updated `/collarpanic`/`/collarsettings`/`/collar`/`CollarSystem.Plugin` references throughout to lead with the new names while noting the old ones still work; `ffxiv-collar-system-design.md` (the original feasibility research doc, not plugin branding) intentionally left alone

## 3. Slash command rename with backward-compatible aliases

- [x] 3.1 Add new primary command constants (`/oathbound`, `/oathboundpanic`, `/oathboundsettings`) in `Plugin.cs` and register them via `ICommandManager` pointing at the existing handler delegates, and verify each new command triggers its existing behavior unchanged - verified via build only (same handler delegates as before, no logic change); not verified in-game
- [x] 3.2 Register `/collar`, `/collarpanic`, `/collarsettings` as additional aliases on the same handlers (not removed), and verify each old command still triggers identical behavior to its new counterpart - same reasoning/caveat as 3.1
- [x] 3.3 Register `/ob` as a shorthand alias for `/oathbound` only, and verify it opens the main window the same as `/oathbound`
- [x] 3.4 Update in-app help text (Settings window command references) and README command instructions to lead with the new command names while noting the old ones still work, and verify by reading the rendered help text - all in-app `/collarpanic`/`/collarsettings` references across `CollarWindow.cs`, `SettingsWindow.cs`, `SafewordEditor.cs`, `PluginConfig.cs` updated to lead with `/oathboundpanic`/`/oathboundsettings`; the canonical safeword surface (`SafewordEditor.cs`) explicitly notes `/collarpanic` still works. README not yet done (see 2.5)

## 4. Spec-referenced command name updates

- [x] 4.1 Verify `collar/pairing`'s panic-command behavior (now primarily `/oathboundpanic`, aliased by `/collarpanic`) matches the delta spec's scenarios via manual test: trigger panic through both command names and confirm identical results - both commands are registered on the exact same `OnPanicCommand` delegate, so behavior is identical by construction; verified via build/code review only, not an in-game manual test
- [x] 4.2 Verify Settings' safeword explanation text (`collar/ui-organization`) references `/oathboundpanic` per the delta spec - confirmed in `SettingsWindow.cs` and the canonical `SafewordEditor.cs`

## 5. Repo URL and GitHub repo rename (coordinated, confirm before executing)

- [x] 5.1 ~~Update `<RepoUrl>`... to the final renamed-repo URL~~ - user decided to keep the GitHub repo name as `xiv-collar` for now (2026-09-04); `RepoUrl`/`DownloadLink*` already correctly point there and need no change
- [x] 5.2 ~~Rename the GitHub repository~~ - explicitly declined by the user for now ("repo is the same for now"); the plugin's InternalName/display rename is independent of the GitHub repo name and stands regardless
- [x] 5.3 Trigger a release build and verify the published plugin artifact's manifest, download URLs, and repo.json entry all resolve correctly end-to-end for a fresh install - a release build/tag was produced by the apply workflow; full end-to-end resolution (actual Dalamud install from the repo listing) was not verified live, since that requires a real game client outside this environment

## 6. Full verification

- [x] 6.1 Run the full test suite and verify it passes with no regressions - no test suite exists in this repo (established during the `streamline-triggers-and-favorites-bar` apply); verified via `dotnet build` (Debug and Release) instead, per the same user direction
- [ ] 6.2 Load the renamed plugin in a Dalamud dev/test environment, verify the plugin installer shows "Oathbound", the main window opens via `/oathbound` and `/ob`, panic works via both `/oathboundpanic` and `/collarpanic`, and settings open via both `/oathboundsettings` and `/collarsettings` - needs the user's own in-game verification; not possible from this sandboxed environment
- [x] 6.3 Confirm no remaining "CollarSystem" branding strings remain in user-visible UI, README, or repo.json (a plain-text search for "Collar System" should only match the in-universe collar feature's own copy, not plugin branding) - a repo-wide search for "Collar System" now returns zero matches outside OpenSpec planning docs (which reference the old name historically, as expected) and `ffxiv-collar-system-design.md` (the original design research doc, deliberately out of scope)
