## Why

A first-time user lands directly in `CollarWindow`'s ten-tab nav bar (Title, Outfit, Animation, Moodles, Restraints, Custom Triggers, Collar, Follow / Leash, Permissions, Sync) with no explanation of what Role means, no prompt to set a trigger phrase, and no walkthrough of what each tab does - `Role` defaults silently to `Sub` and `TriggerPhrase` defaults silently to `"command"` (`PluginConfig.cs`) unless the user happens to find Settings' Identity & Pairing tab. There is also no path back into an explanation later: a Sub who is later handed Owner duties for the first time gets no orientation to the very different Owner browse/send views that `collar/ui-organization`'s role-aware tabs now show.

## What Changes

- Add a one-time **Welcome window** that appears automatically on the first plugin load (before `CollarWindow` has ever been shown), letting the user set `Role` and `TriggerPhrase` before doing anything else. It does not reappear once dismissed.
- Immediately after the Welcome window is dismissed, automatically open a **guided tutorial** that drives `CollarWindow` itself: it switches `activeModule` through the tabs relevant to the chosen Role in turn, showing an explanatory overlay/callout for each one, rather than being a separate static help document.
- Track tutorial completion **per Role, independently** (`HasSeenOwnerTutorial`, `HasSeenSubTutorial`): the first time the local `Role` is ever set to Owner, the Owner tutorial runs once; the first time it is ever set to Sub, the Sub tutorial runs once. A user who is Sub-then-Owner-then-Sub-again only ever sees each role's tutorial on that role's own first occurrence, not on every switch back.
- Add a **"Rerun Tutorial"** button in SettingsWindow's Identity & Pairing tab that replays the tutorial for the currently active Role on demand, without affecting the other Role's completion flag.
- The Welcome window's Role/TriggerPhrase controls read from and write to the same `PluginConfig.Role` / `PluginConfig.TriggerPhrase` fields Settings already uses - no new parallel config surface for those two values.

## Capabilities

### New Capabilities

- `collar/onboarding`: first-run welcome screen (Role + trigger phrase setup), the guided per-tab tutorial that follows it, per-Role one-time tutorial tracking and re-triggering on a first-ever Role switch, and the Settings "Rerun Tutorial" control.

### Modified Capabilities

(none - `collar/ui-organization`'s existing tab set, labels, and role-aware content are read and driven by the tutorial, not changed by it)

## Impact

- `Oathbound.Plugin/Config/PluginConfig.cs` - new persisted fields: `HasSeenOwnerTutorial` (bool), `HasSeenSubTutorial` (bool), `HasCompletedWelcome` (bool, or equivalent "first run done" marker).
- `Oathbound.Plugin/UI/` - new `WelcomeWindow.cs` and tutorial-overlay UI (e.g. `TutorialOverlay.cs`), registered alongside existing windows.
- `Oathbound.Plugin/UI/CollarWindow.cs` - exposes a way for the tutorial driver to set `activeModule` programmatically and to render a per-tab explanatory callout while a tutorial is active.
- `Oathbound.Plugin/UI/SettingsWindow.cs` - new "Rerun Tutorial" button in the Identity & Pairing tab.
- `Oathbound.Plugin/Plugin.cs` - constructor/init logic to show the Welcome window on first run, and to detect a first-ever Role change and launch that Role's tutorial; window registration for the new window(s).
- No wire-protocol, relay, or cross-client impact - this is entirely local UI/config state on one install, same as the rest of `CollarWindow`/`SettingsWindow`.
