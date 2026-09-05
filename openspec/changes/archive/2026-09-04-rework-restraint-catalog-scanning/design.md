## Context

See `proposal.md` for motivation. Today gesture scanning already parses Penumbra option manifests into a
full Sub-local catalog and exports a smaller identity/presentation record. Restraints instead store manually
captured equipment items, while Owner imported restraint entries are mostly names plus independently chosen
rules. Settings also combines a single free-text animation folder filter with a separate multi-mod checkbox
list, despite already having a reusable allowlist pattern for Wardrobe.

The relay remains catalog-only: operational restraint commands continue through verified in-game tells.
The wire size limit means shared records must remain compact and must never duplicate full selection state
for every option.

## Goals / Non-Goals

**Goals:**

- Give the Owner complete authoring control over every Penumbra restraint option the Sub deliberately
  shares, without requiring either person to exchange internal names manually.
- Use one stable identity from Sub scan through export/import, Owner selection, tell command, and Sub apply.
- Make Penumbra folder selection reusable, multi-select, searchable, and understandable.
- Apply and revert Penumbra settings and restriction rules as one failure-safe restraint lifecycle.
- Preserve existing item devices, aliases, saved commands, and offline catalog transfer.

**Non-Goals:**

- Giving an Owner access to arbitrary installed mods outside the Sub's selected restraint folders.
- Sending gameplay commands, local filesystem paths, or full Penumbra configuration through Cloudflare.
- Inferring restriction rules from mod names; rules remain an explicit Owner/Sub choice.
- Removing the current slot/item device model in this change.
- Persistently editing the Sub's saved Penumbra mod configuration.

## Decisions

### 1. Restraint scanning catalogs mods, not manifest options

Gesture scanning continues parsing manifests and options. Restraint scanning instead emits exactly one
entry per eligible Penumbra mod. It records the mod's stable directory identity and its current saved group
selections only so temporary enabling can preserve them; group names and options are never shared as remote
choices and the Owner cannot change them.

This is preferable to treating every gesture as a restraint or maintaining a second JSON parser: the former
would expose unrelated mods and the latter would allow stable IDs and manifest behavior to drift.

### 2. Restraint folders are an allowlist; animation folders are a convenience scope

`SelectedRestraintFolders` defaults empty and empty means “share no Penumbra restraints.” Remote restraint
control changes appearance and restriction state, so exposure must be deliberate. `SelectedGestureFolders`
also becomes a list, but both it and `SelectedGestureMods` empty retain gesture's existing “scan all” behavior.
When gesture folders exist and explicit mods do not, their union is scanned; explicit mods narrow that union.

The legacy single animation folder string migrates to one normalized list entry. Existing explicit mod
selections remain authoritative and are not silently cleared.

### 3. Export one slim reference per restraint mod

must resolve the ID in its current local restraint catalog before applying anything.
The Sub-local restraint catalog record carries mod directory, current saved selections, and enabled state.
The exported/imported record carries only a stable ID and display name. Import updates the browseable
catalog but creates no commands. An Owner explicitly selects a mod, assigns rules, and saves or sends that
single enable command. The Sub must resolve the ID in its current local catalog before applying anything.
must resolve the ID in its current local restraint catalog before applying anything.

This mirrors the successful gesture split and prevents catalog size from scaling with every option group's
selection state. It also ensures a copied command cannot manufacture access to an unshared mod: knowing a
name or ID is insufficient after the local entry disappears from the allowed scan.

### 4. Catalog-backed and item-backed restraints share one runtime ownership model

Extend restraint definitions/quick commands with an explicit source kind and optional catalog identity.
Existing definitions deserialize as item-backed. Runtime active-device state records which temporary
Penumbra override it owns alongside the rule leases it owns. Apply stages validation first, then activates
the temporary mod, redraws, and acquires rules; any failure unwinds completed steps. Unlock, replacement,
panic, and teardown release rules, revert only owned temporary settings, and redraw.

Using the existing restriction-rule engine preserves conflict/reference-count behavior. Treating the mod
override as owned restraint state avoids interfering with unrelated gesture temporary activation; if the
same mod is touched by both systems, a shared per-mod temporary-setting coordinator must serialize and
restore layers rather than letting one feature blindly remove the other's override.

### 5. Structured restraint-mod entries receive their own versioned export lines

Add a versioned encoded record inside the Restraints section, distinguishable from legacy plain names and
alias lines. Both manual import and relay replacement use the same mutation-free parser/staging plan.
Reconciliation keys structured entries by stable catalog ID and updates the browseable catalog without
auto-generating a quick command for every imported mod. Saved Owner-authored commands retain their
favorites/rules and become stale visibly if the referenced mod disappears.

This preserves old files and commands while allowing newer Owners to browse real mod options. Rejecting a
malformed structured line for relay snapshots prevents truncation from looking like deliberate deletion;
manual imports retain their existing compatibility policy for unrelated legacy lines.

### 6. Reuse one folder-picker component for gesture and restraint scopes

Build a compact helper that derives distinct Penumbra sort-folder paths from the installed mod list and
renders search, multi-selection, selected chips/rows, removal, and tooltips. Gesture settings keep an
optional mod-level multi-select filtered to the selected folder union. Restraints initially need folder
selection plus a catalog preview; individual restraint-mod exclusion can use the same mod-level helper if
the scan volume makes it necessary, without changing stable identities.

Owner restraint browsing is a searchable list grouped by mod folder/name. Choosing a mod opens the rule
editor; only that explicit choice becomes a saved command.

## Risks / Trade-offs

- **Penumbra temporary settings from gesture and restraint features can overlap** → centralize per-mod
  ownership/layering and test release order in both directions.
- **Large restraint collections can approach catalog plaintext/ciphertext limits** → export slim records,
  show matched/exported counts, fail locally before upload, and keep manual transfer available.
- **A mod update can change groups/options and therefore identity** → stable IDs use normalized mod/group/
  option identity; stale Owner commands fail closed and the next catalog sync reconciles them visibly.
- **Folder names and paths can move** → show missing selections, exclude them from scans, and let users
  remove or replace them without silently widening to all mods.
- **Applying a Penumbra option may not visibly alter equipment until redraw** → redraw after apply and every
  release path, and unwind on IPC failure.
- **Retaining legacy devices makes the model more complex** → display source/type clearly and isolate
  migrations; defer removal until a separate breaking change with explicit user migration.

## Migration Plan

1. Add versioned configuration fields and migrate the legacy animation folder string into the new list.
2. Introduce shared scan records/parser and populate the new local restraint catalog only after an explicit
   restraint-folder selection and scan.
3. Add versioned export/import support while continuing to read legacy plain restraint names.
4. Add Owner picker/command support and Sub resolution behind existing Restraints permission.
5. Add temporary-setting ownership and cleanup before enabling catalog-backed send controls.
6. Verify upgrade, panic/unlock, relay/manual sync, and mixed legacy/catalog behavior in Debug and Release.

Rollback can ignore the new configuration fields and structured export records; existing item-backed
devices remain intact. Before rollback, users should unlock catalog-backed restraints so temporary
Penumbra settings are reverted by the version that owns them.
