## 1. Enforcement Foundations

- [ ] 1.1 Expand movement capability discovery to cover every known movement input query, full-freeze state, mouse movement, autorun, and unfollow interception; verify startup tests report each missing capability independently.
- [ ] 1.2 Replace the single movement-lock behavior with reference-counted immobilize and preserve-follow claims; verify overlapping leash, forced-pose, and full-body claims release independently.
- [ ] 1.3 Add framework-tick assertion and ownership-safe teardown for full immobilization; verify manual keyboard, controller, mouse, and autorun movement stay blocked and panic restores movement.

## 2. Functional Leash

- [ ] 2.1 Implement paired-Owner world-object resolution and Oathbound-owned follow start/verification; verify `leash` makes the Sub follow the matched Owner rather than merely setting runtime state.
- [ ] 2.2 Protect active leash follow from movement input and unfollow requests without blocking follow-generated locomotion; verify keyboard, controller, mouse, and autorun attempts cannot break follow.
- [ ] 2.3 Make leash engage transactional and leash release idempotent across unleash, panic, unpair, target loss, and zone changes; verify every failure unwinds claims and produces a specific local diagnostic.

## 3. Reliable Restraint Rules

- [x] 3.1 Extend the restriction-enforcer contract with readiness and failed-engagement results, and preflight every requested rule before slot/rule commit; verify an unavailable hook leaves no device, slot lock, rule, or temporary mod active.
- [ ] 3.2 Make forced-pose application verify pose entry and hold a full immobilization claim for its duration; verify all supported manual movement paths remain blocked until final release.
- [ ] 3.3 Rework walk-only to save prior locomotion state, assert normal and automove walking every framework tick, and reject Sprint bypass; verify directional movement remains usable and prior state returns after final release.
- [ ] 3.4 Integrate action-block readiness into restraint preflight and test the supported hotbar, menu, keybind, macro, and command invocation paths against the action detour.
- [ ] 3.5 Make bound-animation setup transactional with slot and rule activation; verify missing/stale animations or Penumbra failures fully roll back and successful Full Body Cuffed stays immobilized until release.

## 4. Imported Sub Animation Source

- [x] 4.1 Add a persisted Owner-side imported gesture catalog containing stable Sub identity plus readable mod/group/animation/trigger metadata; verify legacy config loads and a catalog re-import refreshes entries without mixing in local scans.
- [x] 4.2 Refactor the animation picker to accept an explicit catalog and mode; verify Sub gesture editing uses the local catalog while Owner restraint editing uses only the imported Sub catalog and has no local rescan action.
- [x] 4.3 Store readable labels and stable Sub identities on Arms, Legs, and Full Body restraint selections; verify stale selections are visibly marked and cannot be sent as enforceable commands.

## 5. Readable Command Protocol

- [ ] 5.1 Implement one quoted-selector codec with escaping, normalization, shortest-unique disambiguation, and message-length validation; verify spaces, punctuation, quotes, collisions, malformed input, and overlong commands in parser tests.
- [x] 5.2 Generate new Gesture tells from readable imported metadata while accepting legacy opaque IDs; verify both forms resolve the same local catalog entry and ambiguous selectors apply nothing.
- [x] 5.3 Generate new Moodle tells with formatting stripped while retaining raw identity for IPC and legacy parsing; verify color/glow/emphasis tags never appear in new tells and sanitized-name collisions remain deterministic.
- [x] 5.4 Generate animation-bearing restraint commands with readable selectors and stable resolution; verify the tell is readable and the Sub activates the exact exported animation.
- [x] 5.5 Add a safe lazy migration path for successfully resolved legacy saved quick commands; verify rollback can still read stable legacy identities and no consent, sender, trigger, or direct-send gate changes.

## 6. Integration Verification

- [ ] 6.1 Add end-to-end local command tests covering successful and failed leash engagement, every restraint enforcement kind, panic/unpair teardown, and explicit unavailable-capability diagnostics.
- [ ] 6.2 Add Owner/Sub fixture tests with deliberately different local mod libraries; verify restraint pickers and sent payloads always reference the Sub-exported library.
- [x] 6.3 Build the plugin solution and run the full automated test suite; verify there are no compilation failures or regressions in pairing, permissions, catalog import, and chat transport.
- [ ] 6.4 Perform an in-game manual matrix for legacy/standard movement, keyboard/controller/mouse input, autorun, follow cancellation, walk/run/Sprint, action use, and readable tell output; record observed results for every scenario.
