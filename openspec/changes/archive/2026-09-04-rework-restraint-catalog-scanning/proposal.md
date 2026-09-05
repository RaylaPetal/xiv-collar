## Why

Restraints currently depend on the Sub manually constructing devices or the Owner already knowing enough
about the Sub's setup to recreate one. That defeats the Owner-control workflow: a paired Owner should be
able to browse the restraint content the Sub deliberately shared, choose it, assign restrictions, and lock
it without exchanging names and setup details out of band.

## What Changes

- Add a dedicated Sub-side Penumbra restraint scan scope based on a multi-folder allowlist. Only mods under
  those selected folders become remotely visible or commandable as restraint content.
- Scan each selected restraint mod as one stable catalog entry. Do not expose or remotely change its
  option groups or toggles; activation temporarily enables the mod with its existing saved settings.
- Include the filtered restraint catalog in manual export/import and encrypted relay catalog sync so the
  Owner receives structured, searchable restraint choices rather than plain device names.
- Let the Sub explicitly choose scanned mods and configure named restraint rule sets too; export those
  creations alongside the raw mod library so they import as ready-made Owner commands.
- Rework Owner restraint authoring around the imported Sub catalog: browse a shared restraint mod,
  attach the desired restriction rules, save/favorite it, and send a force-lock command whose stable
  identity the Sub resolves against its own local scan.
- Preserve explicit Sub consent and safety boundaries: unshared mods cannot be selected remotely, the
  Restraints permission still gates application, and panic/unlock restores temporary Penumbra state and
  releases every rule.
- Retire legacy name-based restraint catalog commands from export/import and the Owner UI. Keep direct
  slot/item authoring as an advanced section below detected mod restraints.
- Replace the animation scan's single free-text folder filter with a searchable multi-select folder
  dropdown, matching the Wardrobe allowlist interaction and allowing any number of Penumbra folders.
- Keep optional individual-mod selection inside the chosen animation folders, with clear “all matching
  mods” versus “explicitly selected mods” behavior.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `collar/restraints`: Add filtered Penumbra restraint-mod discovery, catalog-backed Owner force-control,
  and reversible whole-mod enable locking alongside legacy slot/item devices.
- `collar/gesture`: Replace the single folder filter with a persistent multi-folder animation allowlist and
  dropdown-based selection workflow.
- `collar/catalog-sync`: Synchronize structured restraint catalog entries atomically through both offline
  files and the encrypted relay.
- `collar/ui-organization`: Add understandable scan scopes and searchable Owner restraint selection without
  making narrow windows unusable.

## Impact

- Configuration and migration for restraint catalogs, selected restraint folders, and the animation folder
  allowlist.
- Penumbra scanning and temporary-settings application/reversion.
- Restraint command serialization, validation, conflict handling, unlock/panic cleanup, and quick commands.
- Catalog export/import schemas and relay snapshot reconciliation.
- Settings scan UI and Owner restraint authoring/picker UI.
- Regression coverage for scope filtering, stable identity, migration, command authorization, atomic sync,
  and Penumbra redraw/reversion.
