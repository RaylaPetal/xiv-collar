## Context

See `proposal.md` - Why/What Changes for motivation. Relevant existing state:

- `PluginConfig.Role` (`PluginRole` enum, default `Sub`) and `PluginConfig.TriggerPhrase` (string, default `"command"`) already exist and are edited in `SettingsWindow`'s Identity & Pairing tab.
- `PluginConfig.Version` drives a version-gated `MigrateConfiguration()` in `Plugin.cs` (~line 354) - the existing pattern for one-time, load-time config changes.
- `Plugin.cs` instantiates `Window` subclasses in its constructor and registers them with Dalamud's `WindowSystem`; there is no existing "first run" concept anywhere in the codebase.
- `CollarWindow`'s tabs are not native ImGui tabs - `NavBar.Draw` renders a custom bar driven by a private `activeModule` string field, switched via a `NavItems` array of `(Id, Icon, Tooltip)` tuples (`title`, `outfit`, `animation`, `moodles`, `restraints`, `customtriggers`, `collar`, `follow`, `permissions`, `sync`).
- Existing installs deserialize new config fields to their C# default (e.g. `false`, `null`) - no explicit migration step is required just to add new bool/string fields, only to decide what a **pre-existing** install should experience (see Decisions below).

## Goals / Non-Goals

**Goals:**
- Define how the Welcome window and tutorial hook into plugin startup without a prior "first run" concept to build on.
- Define how the tutorial drives `CollarWindow`'s existing `activeModule` field from outside the window itself, without turning `CollarWindow` into a stateful multi-mode widget.
- Define what happens for installs that already exist before this change ships (they must not be surprise-ambushed by a Welcome window that resets Role/TriggerPhrase they've already configured).

**Non-Goals:**
- No change to `collar/ui-organization`'s tab set, labels, or role-aware content - the tutorial narrates existing tabs, it does not add or reorder them.
- No localization/translation system for tutorial copy - English strings only, same as the rest of the plugin's UI.
- No analytics/telemetry on tutorial engagement - purely local UI state.

## Decisions

**Existing installs are treated as already "welcomed."** On the version-gated migration path in `Plugin.cs`, an install whose config predates this change (detected via the existing `Version` field) has `HasCompletedWelcome`, `HasSeenOwnerTutorial`, and `HasSeenSubTutorial` all set to `true` as part of that migration step, rather than left at their `false` default. Alternative considered: let new bool fields default to `false` for everyone, including existing installs - rejected because it would pop a "Welcome" window and re-run a tutorial in front of users who are already mid-session with a paired, configured setup, which reads as a bug, not a feature.

**Tutorial driver lives outside `CollarWindow`, but writes its `activeModule` field via an internal setter.** A new small tutorial-state class (owned by `Plugin`) holds "is a tutorial active," "which Role's sequence," and "current step index," and calls a new internal method on `CollarWindow` (e.g. `SetActiveModuleForTutorial(string moduleId)`) each time it advances. `CollarWindow.Draw()` checks the tutorial state each frame to render the current step's explanatory callout over/alongside the normal tab content. Alternative considered: give `CollarWindow` its own embedded tutorial logic - rejected because the Welcome window (which is not part of `CollarWindow` at all) also needs to kick the very first step off, and Settings' "Rerun Tutorial" button needs to trigger it too; a driver `Plugin` already has a reference to is reachable from all three places, whereas logic buried inside `CollarWindow` is not easily reachable from `WelcomeWindow`/`SettingsWindow`.

**One shared tutorial-step sequence, filtered by Role at tutorial-start time**, rather than two hand-maintained lists. Each step names a tab id from the existing `NavItems` set plus explanation copy and, where a tab's content differs by Role (every shared category tab does), Role-specific copy. Building the Owner and Sub sequences from one source keeps the step list in sync with `NavItems` as tabs are renamed/added/removed over time, rather than two lists silently drifting apart. `permissions` is Sub-only and `sync` behaves differently but is shown to both, matching `collar/ui-organization`'s existing role-conditional behavior - the tutorial step list mirrors that, it does not invent new conditions.

**Welcome window is a distinct `Window`, not a mode of `CollarWindow`.** It is deliberately minimal (Role choice, trigger phrase input, a "Continue" action) and is shown before `CollarWindow` is ever opened for the first time. Alternative considered: fold Welcome into `CollarWindow`'s first tab - rejected because `CollarWindow`'s character header and nav bar assume Role/pairing state already exists and would render confusingly incomplete before the user has made a choice.

## Risks / Trade-offs

- [Tutorial callout logic adds a per-frame check inside `CollarWindow.Draw()`] → Keep the check to a single cheap boolean/state lookup at the top of `Draw()`; the callout itself only renders when a tutorial is actually active, so idle-state cost is negligible.
- [Migrating existing installs to "already welcomed" could mis-skip a genuinely new install that happens to load with a stale/default config in an unusual sequence, e.g. a fresh install racing a config-version bump] → Gate on the existing `Version` field the same way `MigrateConfiguration()` already does for every other one-time change, so behavior stays consistent with how the plugin already distinguishes "pre-existing install" from "brand new install."
- [Owner and Sub tutorial sequences sharing one source list could produce awkward copy if a step's Role-specific text is forgotten for one Role] → Each step's Role-specific copy is a required field in the step definition (no silent fallback to the other Role's text), so a missing one is a compile-time/data-completeness gap, not a silent wrong-copy bug at runtime.
