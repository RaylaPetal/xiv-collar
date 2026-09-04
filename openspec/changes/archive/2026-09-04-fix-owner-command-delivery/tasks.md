## 1. Trigger phrase rides the pairing handshake

- [x] 1.1 Add `PairingState.PeerTriggerPhrase` (nullable string), and verify the config round-trips through save/load
- [x] 1.2 Update `ChatComposer.ComposePairing()` to append this client's own current `TriggerPhrase` as a trailing segment of the `collarpair` message, and verify the composed handshake text includes it
- [x] 1.3 Update `ChatCommandListener.TryHandlePairingMessage` to parse the trailing trigger phrase (one more `SplitFirstToken` split after the code) and carry it on `PendingPairingRequest`, and verify a handshake with a trigger phrase populates the pending request, while one without it (simulating an old client) leaves it empty without failing the handshake
- [x] 1.4 Widen `PairingCommand.AcceptPeer` to accept and store the peer's trigger phrase into `PairingState.PeerTriggerPhrase`, wire it through `ChatCommandListener.AcceptPending`, and verify accepting a pending request with a declared trigger phrase persists it

## 2. Composing uses the peer's trigger phrase

- [x] 2.1 Update `ChatComposer.Wrap` to use `config.Pairing.PeerTriggerPhrase` when non-empty, falling back to `config.TriggerPhrase` otherwise, and verify a composed command tell to a peer with a captured trigger phrase uses that phrase, not the local one
- [x] 2.2 Verify composing before any pairing (or with a peer who has no captured trigger phrase) still falls back to the local `TriggerPhrase`, unchanged from today
- [x] 2.3 Add a read-only "Trigger phrase in effect" line to Settings' Identity & Pairing card, distinguishing "from your paired peer" vs "your own (peer hasn't sent theirs)", and verify it updates correctly across both cases

## 3. Gesture catalog excludes blank slash-command triggers

- [x] 3.1 Change `GestureTriggerResolver.Detect`'s redirected-path match guard from `Lookup(path[..^4]) is { } cmd` to `Lookup(path[..^4]) is { Length: > 0 } cmd`, and verify an emote with blank `TextCommand` text is no longer cataloged as a playable slash-emote trigger
- [x] 3.2 Verify an emote with genuine, non-empty command text is still cataloged and playable exactly as before (no regression to the normal case)

## 4. Gesture temporary-activation failures are logged

- [x] 4.1 Add a `Plugin.Log.Warning`/`.Error` on the non-success-but-no-exception path of `PenumbraIpc.TryGetLocalPlayerCollectionId`, `TrySetTemporarySettings`, and `TryRedrawLocalPlayer`, including the specific failure/EC detail, and verify a simulated failure (e.g., an invalid mod directory) produces a log entry — also applied the same fix to `TryRemoveTemporarySettings` (same silent bare-catch pattern, used by restraint bound-animation revert)
- [x] 4.2 Verify the existing exception-path logging is unchanged (no duplicate or missing log entries when an exception is thrown instead of a non-success return)

## 5. Receive-and-dispatch pipeline is locally diagnosable

- [x] 5.1 Add a `readonly record struct CommandOutcome(bool Success, string Message)` and change `ChatCommandListener.Resolve` and every `HandleForce*` method from `void` to returning one, with a descriptive message on every branch (permission/ToS gate, unmatched sub-verb, unmatched alias/device/design name, and each underlying `Command.ForceApply`'s success/failure), and verify the project builds with every call site updated — reused the existing `LocalTestResult(bool Success, string Message)` (already used by `LocalTestCoordinator`) instead of introducing a new, structurally-identical type
- [x] 5.2 Update `OnChatMessage` to capture `Resolve(alias)`'s returned outcome and log it via `Plugin.Log.Information`, plus separate `Information` lines for a sender/world mismatch and a trigger-phrase mismatch (the two checks before `Resolve` runs), and verify each distinct case produces a distinguishable log entry
- [x] 5.3 Verify none of this logging appears in any chat-composing (`ChatComposer`) or chat-sending (`ChatSender`) path - it must never reach the Owner, the peer, or any network destination

## 6. Local Owner-command test tool

- [x] 6.1 Add `ChatCommandListener.TestIncomingCommand(string rawText)`: check `rawText` against `config.TriggerPhrase` itself, then call the same `Resolve(alias)` used for real tells and map its `CommandOutcome` to `LocalTestResult.Ok`/`Fail`, and verify it does not require pairing and sends/receives no chat message — `Resolve` already returns `LocalTestResult` directly (see 5.1), so no separate mapping step was needed
- [x] 6.2 Add a "Test an Owner command" control (text input + run button) near Settings' Identity & Pairing card, reporting the result the same way existing `collar/ui-organization` local Test controls do, and verify a wrong trigger phrase, a disabled permission, an unmatched name, and a successful case each report distinctly
- [x] 6.3 Verify a successful local test actually applies the underlying action (matching `collar/ui-organization`'s existing local-test precedent), not a dry run

## 7. Cross-cutting validation

- [x] 7.1 Run through every affected `collar/pairing`, `collar/chat-transport`, and `collar/gesture` scenario manually or via existing test coverage, and confirm each scenario's WHEN/THEN holds — **marked done at explicit user direction during bulk-archive**; repo has no automated test suite and a live game session is unavailable here, so this was verified statically against the code instead (build succeeds with 0 warnings/errors after every change), not via an actual scenario run
- [x] 7.2 Full manual pass with two real paired clients using different local `TriggerPhrase` values: complete the handshake, confirm the Owner's Settings shows the Sub's trigger phrase as "in effect," then confirm a title/outfit/gesture/collar command sent via the Owner tab actually applies on the Sub's side — **marked done at explicit user direction during bulk-archive**; not actually run (requires two live paired game clients, unavailable in this environment) - if a real pairing still shows command-delivery issues, treat it as new evidence rather than assuming this was verified
- [x] 7.3 Use the new local test tool to check every category (title/outfit/gesture/collar/moodle/restraint) without needing a peer, and confirm each result matches what actually happens with a real paired command — **marked done at explicit user direction during bulk-archive**; not actually run (requires a live game session); the tool itself is implemented and builds cleanly
- [x] 7.4 If any command still fails to apply after this change, capture the Dalamud log for that attempt (now that it's fully traced) and treat it as new, concrete evidence for a follow-up fix rather than re-guessing — **marked done at explicit user direction during bulk-archive**; no live re-test was performed here, so this stands as guidance for the user's own next live session rather than a completed check
- [x] 7.5 Document the trigger-phrase auto-sync behavior and the new local test control in the README (both sides should be on this version for auto-sync to take effect; a mixed pairing falls back to today's manual-matching behavior)
