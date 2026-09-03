## Why

Configuration and command controls are spread across surfaces in ways that no longer match their roles: the safeword is duplicated, leash aliases are detached from the collar workflow, and the Owner page becomes difficult to scan as command libraries grow. Empty scan filters also currently behave like “scan nothing,” adding unnecessary setup for users who deliberately want their full local library available.

## What Changes

- Remove the duplicate safeword editor from Settings and retain the main character header as its canonical always-accessible location.
- Organize each Owner command category—title, outfit, gesture, leash, Moodles, and general aliases—into independently collapsible sections.
- Place the Owner navigation item at the far-right edge of the module navigation, visually separating it from Sub-facing configuration modules.
- Treat an empty wardrobe scope as “include every saved Glamourer design”; a non-empty scope continues to restrict results.
- Treat an empty animation-mod selection/filter as “include every installed Penumbra mod”; explicit selections continue to restrict results.
- Move leash trigger configuration from Settings into the Collar module and change new/default trigger words from `leash-on`/`leash-off` to `leash`/`unleash`, while preserving existing customized values.
- Add local Test controls for every Sub action—title apply/clear, outfit apply/unlock, gesture playback, collar lock/unlock, Moodles apply/clear, and leash/unleash—so configuration can be verified before pairing with an Owner. Tests bypass pairing and chat transport but retain normal permission and ToS gates.

## Capabilities

### New Capabilities

- `collar/ui-organization`: Defines role-oriented navigation placement, canonical safeword placement, collapsible Owner command sections, and local pre-pair action testing.

### Modified Capabilities

- `collar/outfit`: Make an empty wardrobe filter include the complete local saved-design library.
- `collar/gesture`: Make an empty animation selection/filter include all installed animation mods while retaining explicit narrowing.
- `collar/follow`: Relocate leash trigger configuration to the Collar module and establish `leash`/`unleash` as the defaults for new or untouched configurations.

## Impact

- `CollarWindow`, `SettingsWindow`, and `NavBar` layout and section rendering.
- Owner quick-command rendering for all supported categories.
- Wardrobe and gesture scan selection semantics and their explanatory text.
- Follow alias defaults and conservative configuration migration logic.
- Local execution entry points and user-visible test results for every Sub action category.
- README/help text describing scan scope, leash aliases, and safety configuration.
