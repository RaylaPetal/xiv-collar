## 1. Moodle IPC and scan correctness

- [ ] 1.1 Verify the installed/current Moodles IPC declarations for preset listing, apply, and clear; replace the guessed subscribers with exact zero-input/result and player-target signatures, and verify a live preset-list invocation no longer logs an IPC length/type mismatch.
- [ ] 1.2 Add a Moodles scan result model that distinguishes success, empty, unavailable, and failed states; preserve the last catalog on failure and verify tests cover each outcome.
- [x] 1.3 Update Moodles catalog refresh and Settings feedback so a successful local scan with at least one saved preset displays that preset, a true empty scan says zero, and an IPC failure displays an error.
- [x] 1.4 Make Moodles “Copy names” export each name from the latest successful local Moodles catalog exactly once, and verify with zero-, one-, and multi-preset cases.

## 2. PoseKit-equivalent animation discovery

- [x] 2.1 Add Penumbra wrappers for mod root, effective local-player collection settings, temporary mod settings, temporary-settings removal if needed, and local-player redraw; verify all calls fail gracefully when Penumbra or the collection is unavailable.
- [ ] 2.2 Port/adapt PoseKit's manifest DTOs and scanner for `default_mod.json` and top-level `group_*.json`, preserving mod/group/option names and complete selections; verify fixtures cover default-only, single-select, multi-select, disabled, and malformed mods.
- [ ] 2.3 Port/adapt PoseKit's Lumina Emote/ActionTimeline reverse index and trigger heuristics for explicit `(/command)` hints, slash emotes, and sit/ground-sit/doze poses; verify representative redirected paths resolve to the same triggers as PoseKit.
- [x] 2.4 Replace gesture folder-allowlist-only configuration with explicit selected Penumbra mods plus non-mutating folder/text filters, migrate usable prior selections where possible, and verify filtering never changes the persisted selected set.

## 3. Structured gesture identity and UI

- [x] 3.1 Replace flat gesture catalog/alias records with structured mod, group selections, animation option name, and slash-command-or-pose trigger identities; verify multiple triggers on one option remain distinct commandable entries.
- [x] 3.2 Add best-effort migration for legacy gesture aliases, accepting only unique mod/emote matches and visibly marking ambiguous or unmatched entries; verify migration never guesses between multiple options.
- [x] 3.3 Rework the Settings/Gesture views to match PoseKit's browsable named-animation flow, including explicit mod selection, grouped animation option names, trigger labels, disabled-mod discoverability, search, rescan feedback, and no-playable-trigger display.
- [x] 3.4 Replace name-only gesture clipboard exchange with a versioned structured representation and friendly labels, update Owner import/quick commands accordingly, and verify round-tripping preserves mod, option, complete selections, and trigger.
- [x] 3.5 Add a dedicated polished “Add animation” picker window with PoseKit-style search/rescan, collapsible mods and groups, named option/trigger rows, disabled/non-playable context, and selection back into the alias form; verify the main Gesture tab no longer embeds the dense picker.
- [x] 3.6 Preserve Penumbra/PoseKit manifest order for animation groups, options, and triggers instead of alphabetically sorting their labels; verify large numbered packs render in numeric source order rather than `1, 10, 100` order.
- [x] 3.7 Render saved gesture aliases in a responsive wrapping layout that keeps long animation/mod names readable and the Remove action visible without clipping; verify narrow-window layout wraps within its text column.

## 4. Temporary activation and immediate playback

- [x] 4.1 Implement one gesture execution path that resolves the current local catalog entry, applies a collar-scoped complete temporary mod-selection payload to the effective collection, redraws the local player, then plays the tied slash emote or supported pose; verify call order and that activation failure prevents playback.
- [x] 4.2 Route permitted Owner direct and alias gesture commands through immediate execution, retain pairing/Gesture-permission/acknowledgement checks, and verify rejected commands change neither Penumbra state nor the active animation.
- [x] 4.3 Remove pending gesture queue state and confirm/dismiss UI, and verify no inbound gesture command can stall waiting for a second Sub action.
- [x] 4.4 Verify gesture execution does not call permanent Penumbra setting APIs and that the temporary override uses a plugin-specific source tag with the complete real group selection map.

## 5. Integration, migration, and documentation

- [x] 5.1 Update help text and README descriptions for local Moodles scanning, named animation export/import, explicit mod selection, immediate permission-gated gesture execution, and its automation-risk implications; verify no user-facing text still promises a confirmation queue.
- [ ] 5.2 Build the solution and run the targeted scanner, serialization, migration, Moodle-result, permission, and execution-order tests; resolve all failures.
- [ ] 5.3 Perform an in-game smoke test with at least one saved local Moodle preset and one disabled multi-option animation mod: verify Moodle scan/copy/apply, animation-name visibility, structured Owner selection, temporary enable/redraw, and tied gesture playback end to end.
