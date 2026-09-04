## Why

Three gaps surfaced after using the plugin: the collar has no way to carry a persistent visual marker (a Moodle) the way it already carries a persistent equipment item; Moodles status names can contain the Moodles plugin's own `[color=N]`/`[glow=N]`/`[i]` markup, which this plugin currently displays as literal bracketed text instead of clean names; and the Sub's per-item alias export (used so an Owner can import one-off alias words) still only covers Title/Outfit/Gesture/Restraint, missing the Moodles and Custom Triggers alias categories added after that export was built.

## What Changes

- The Sub can optionally assign a Moodle status to their collar. When the collar applies and locks (at pairing acceptance, or via the Owner's `collar lock` override), the assigned Moodle applies at the same time and is periodically re-asserted for as long as the collar lock is active, so removing it through Moodles' own UI does not make it stick - it reverts within a short window. The Moodle clears whenever the collar's own lock releases (panic, or the Owner's `collar unlock`), the same lifecycle the collar's Neck-slot lock already has - no separate release path.
- Every place this plugin displays a Moodles status name (the Sub's own Moodles tab, Custom Triggers, Owner quick commands, the collar's Moodle picker) strips Moodles' own `[color=N]`, `[glow=N]`, and `[i]` markup tags before display, showing the plain underlying text instead of the literal bracketed markup.
- The Sub's Scan & Export "Aliases" section (and the matching Owner-side import into the "Alias/one-off" quick-command list) now also includes the Sub's Moodles aliases and Custom Trigger aliases, alongside the existing Title/Outfit/Gesture/Restraint alias words.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities
- `collar/collaring`: adds an optional Moodle assignment to the collar's configuration, applies/re-asserts it whenever the collar is locked, and clears it whenever the collar's lock releases.
- `collar/moodles`: adds a requirement that every displayed Moodles status name has Moodles' own markup tags stripped before display.
- `collar/catalog-sync`: extends the Aliases export/import section to also include the Sub's Moodles and Custom Trigger alias words.

## Impact

- `CollarSystem.Plugin/Config/PluginConfig.cs` (`CollarState`): new optional Moodle status fields.
- `CollarSystem.Plugin/Commands/CollarCommand.cs`: apply/re-assert/clear the assigned Moodle alongside the Neck-slot lock's own apply/release paths.
- `CollarSystem.Plugin/Ipc/MoodlesIpc.cs` / `CollarSystem.Plugin/Commands/MoodlesCommand.cs`: a shared markup-stripping helper for status names, and (new) a lightweight periodic re-assertion mechanism for the collar-locked Moodle.
- `CollarSystem.Plugin/UI/CollarWindow.cs`: a Moodle picker on the Collar tab; every existing Moodles-name display point (DrawMoodlesModule, DrawMoodlesQuickSection, DrawCustomTriggersModule's Moodle picker, DrawCollarModule) routes status names through the new stripping helper.
- `CollarSystem.Plugin/Commands/CatalogSyncService.cs`: `ExportAliasNames` gains the Moodles and Custom Trigger alias lists.
- README: document the collar-Moodle assignment and its lock lifecycle, the markup-stripping behavior, and the corrected alias export scope.
