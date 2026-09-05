## 1. Config fields and migration

- [x] 1.1 Add `HasCompletedWelcome`, `HasSeenOwnerTutorial`, `HasSeenSubTutorial` (bools, default `false`) to `PluginConfig.cs` and verify the plugin builds (`dotnet build Oathbound.Plugin/Oathbound.Plugin.csproj`)
- [x] 1.2 Bump `PluginConfig.Version` and add a migration branch in `Plugin.cs`'s `MigrateConfiguration()` that sets all three new fields to `true` for any config loaded at a pre-change version, and verify by loading a config file saved at the old version and confirming the fields come back `true` after load
- [x] 1.3 Verify a fresh install (no existing config file) loads with all three fields at their `false` default

## 2. Welcome window

- [x] 2.1 Create `Oathbound.Plugin/UI/WelcomeWindow.cs` (a `Window` subclass) with Role selection and trigger-phrase input controls that read/write `PluginConfig.Role` / `PluginConfig.TriggerPhrase` directly, and a "Continue" action; verify by building and opening it manually in-game
- [x] 2.2 Register `WelcomeWindow` with `WindowSystem` in `Plugin.cs` and open it automatically on startup only when `HasCompletedWelcome` is `false`; verify by clearing the flag in a test config and confirming the window opens on next plugin load, then does not reopen on the load after that
- [x] 2.3 On "Continue", set `HasCompletedWelcome = true`, close the Welcome window, and hand off into the guided tutorial for the Role chosen in the Welcome window (see Section 3); verify by completing Welcome and observing the tutorial start immediately

## 3. Tutorial driver and step content

- [x] 3.1 Add a tutorial-state holder (owned by `Plugin`) tracking whether a tutorial is active, which Role's sequence, and the current step index, plus a shared step list keyed by `NavItems` tab id with per-Role explanation copy, per design.md's "one shared tutorial-step sequence" decision
- [x] 3.2 Add an internal method on `CollarWindow` (e.g. `SetActiveModuleForTutorial`) that the driver calls to switch `activeModule`, and render an explanatory callout for the current step when a tutorial is active; verify by starting a tutorial and confirming the main window's tab changes automatically alongside each step's callout
- [x] 3.3 Implement "advance to next step" / "exit tutorial early" controls on the callout; verify exiting early closes the tutorial without changing any further tabs
- [x] 3.4 On tutorial completion (finishing the last step or exiting early), set `HasSeenOwnerTutorial` or `HasSeenSubTutorial` (matching the Role the tutorial ran for) to `true`; verify by completing/exiting a tutorial and confirming the corresponding flag flips
- [x] 3.5 Build the Owner step sequence and the Sub step sequence from the shared list, filtered/ordered per design.md (Owner and Sub views of each shared category tab, `permissions` Sub-only, `sync` shown to both with its existing role-conditional content); verify by running each sequence and confirming every step's tab id exists in `CollarWindow.NavItems`

## 4. First-ever Role switch triggers that Role's tutorial

- [x] 4.1 In the code path where `PluginConfig.Role` is changed (Settings' Identity & Pairing combo, and the Welcome window's own Role choice), detect a change to Owner where `HasSeenOwnerTutorial` is `false` and start the Owner tutorial automatically; verify by switching to Owner for the first time on a test config and confirming the Owner tutorial launches
- [x] 4.2 Do the same for a change to Sub with `HasSeenSubTutorial` false; verify equivalently
- [x] 4.3 Verify switching back to a Role whose tutorial has already been seen does NOT relaunch it (e.g. Owner -> Sub -> Owner with `HasSeenOwnerTutorial` already `true`)

## 5. Settings "Rerun Tutorial" control

- [x] 5.1 Add a "Rerun Tutorial" button to `SettingsWindow`'s Identity & Pairing tab, styled per the existing icon+separator+button+help-marker card pattern, that starts the tutorial for the currently active Role regardless of that Role's seen-flag; verify by clicking it after a Role's tutorial has already been marked seen and confirming it runs again
- [x] 5.2 Verify using "Rerun Tutorial" for one Role does not change the other Role's `HasSeen*Tutorial` flag

## 6. Manual verification

- [x] 6.1 Fresh-install walkthrough: delete/rename local config, launch the plugin, confirm Welcome appears, set Role to Sub, confirm the Sub tutorial runs through every expected tab, confirm Welcome does not reappear on next load
- [x] 6.2 Role-switch walkthrough: from the state above, switch Role to Owner in Settings, confirm the Owner tutorial runs automatically once, switch back to Sub, confirm no tutorial auto-launches
- [x] 6.3 Existing-install upgrade walkthrough: load a config saved at the prior `Version`, confirm no Welcome window and no auto-launched tutorial appear despite the new fields existing
