## 1. Configuration and Penumbra Catalog Foundation

- [x] 1.1 Add versioned configuration for selected animation folders, selected restraint folders, the full local restraint-option catalog, and the slim imported peer restraint catalog; verify old configurations deserialize with existing slot/item devices and selected gesture mods unchanged.
- [x] 1.2 Migrate the legacy single animation folder filter into one normalized selected-folder entry without clearing explicit mod selections, and verify migration is idempotent across repeated startup.
- [x] 1.3 Preserve gesture manifest traversal and stable option identities while adding stable whole-mod identities for restraint scanning.
- [x] 1.4 Build the restraint scan projection so every mod under the selected folder union appears exactly once, while an empty folder selection returns no restraint entries; capture saved selections locally only and verify outside-folder mods never enter the catalog.

## 2. Multi-Folder Scan User Experience

- [x] 2.1 Add a reusable searchable Penumbra sort-folder multi-select dropdown with selected-folder display, individual removal, missing-folder indication, and full-path tooltips; verify it remains operable at Settings' minimum width.
- [x] 2.2 Replace the animation free-text folder field with the multi-folder dropdown and preserve the individual-mod checkbox filter inside the selected folder union; verify empty folders/mods scans all, folders-only scans their union, and explicit mods narrow that union.
- [x] 2.3 Add a dedicated Restraints folder dropdown, rescan action, matched-mod counts, and mod catalog preview to Scan & Export; verify empty selection clearly says no Penumbra restraints are shared rather than scanning everything.
- [x] 2.4 Include the restraint scanner in the unified Scan All action without modifying manually captured slot/item devices, and verify category-specific error/status feedback remains independent.

## 3. Structured Restraint Sharing

- [x] 3.1 Define a versioned slim restraint export record containing only stable mod ID and display name, with no filesystem path or local option state; verify serialized size stays bounded as source mods gain additional groups.
- [x] 3.2 Extend the Restraints catalog section to export structured scanned entries alongside compatible legacy device names and aliases; verify only entries from currently selected restraint folders are emitted.
- [x] 3.3 Extend mutation-free manual and relay import staging to reconcile the browseable restraint-mod catalog without auto-creating quick commands, while preserving Owner-authored/legacy commands; verify malformed relay entries leave every category unchanged.
- [x] 3.5 Export Sub-configured mod restraints separately from raw mod discovery entries and import only those configurations as ready-made Owner commands with their rules.
- [x] 3.4 Make relay and manual catalog paths use the same restraint snapshot representation and enforce existing plaintext/ciphertext limits before upload; verify an oversized restraint catalog fails locally and offline export/import still works.

## 4. Catalog-Backed Restraint Commands

- [x] 4.1 Extend restraint definitions and quick-command metadata with an explicit item-backed versus catalog-backed source and stable catalog identity, defaulting legacy records to item-backed; verify config round trips do not reinterpret old devices.
- [x] 4.2 Define readable, bounded enable-and-lock command serialization for a catalog identity, selected equipment item, and Owner-selected rules; resolve the item's Glamourer slot locally instead of serializing a slot. Individual entries have no disable action; the global restraint unlock is the sole release command.
- [x] 4.3 Resolve catalog commands only against the Sub's current full local restraint catalog after verified-peer and Restraints-permission checks; verify a stale or unshared identity changes neither Penumbra nor restriction state.
- [x] 4.4 Rework the Owner Restraints surface into a searchable one-entry-per-mod browser with an explicit choose → configure rules → save/favorite/copy/send flow; verify import alone creates no commands and the Owner never changes mod toggles.
- [x] 4.5 Remove legacy name-based restraint commands from export/import and Owner authoring while leaving old serialized data non-fatal; place direct slot/item controls after configured mod restraints.

## 5. Atomic Apply, Lock, and Cleanup

- [x] 5.1 Add a per-mod temporary-setting ownership coordinator shared by gesture and restraint activation so overlapping features restore the correct remaining layer regardless of release order; verify gesture-then-restraint and restraint-then-gesture sequences independently.
- [x] 5.2 Apply a catalog-backed restraint by validating all rules first, temporarily enabling the mod with its saved selections unchanged, redrawing, and acquiring rule leases as one unwindable operation; verify injected failure leaves no partial state.
- [x] 5.3 Track each active catalog restraint's owned temporary override and release it on matching unlock/replacement with a redraw while leaving unrelated gesture/restraint overrides intact; verify saved Penumbra settings are never overwritten.
- [x] 5.4 Route global restraint unlock, verified unpair cleanup, logout/disposal cleanup, and panic through the same idempotent release path; verify every rule and owned Penumbra override is released and repeated cleanup is harmless.
- [x] 5.5 Preserve conflict and force-lock precedence across item-backed and catalog-backed restraints; verify a refused conflict does not equip, enable, redraw, or partially acquire rules.

## 6. Verification and Documentation

- [x] 6.1 Add deterministic scanner tests for folder unions, nested prefixes, missing/moved folders, explicit gesture-mod narrowing, one-entry-per-restraint-mod behavior, and stable identities.
- [x] 6.2 Add catalog tests for structured restraint manual/relay round trips, atomic rollback, newer-snapshot removal, legacy preservation, favorites/rules carry-forward, privacy-field exclusion, and size ceilings.
- [x] 6.3 Add command/runtime tests for permission denial, stale identity, apply/revert/redraw, conflict rollback, shared temporary-setting ownership, unlock, unpair, disposal, and panic.
- [x] 6.4 Run Debug and Release builds plus the plugin regression suite and strict OpenSpec validation; verify zero build errors and record any environment-only warnings.
- [x] 6.5 Perform a two-client in-game pass with small and large Penumbra collections: select multiple folders, relay-sync, browse/search as Owner, apply several triggerless and animated restraints, overlap a gesture, unlock, panic, and restart; record results before release.
