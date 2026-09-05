## 1. Confirm-before-replace for outstanding invitations

- [x] 1.1 Add `PairingService.DescribeOutstandingInvitation()`, a query-only method reporting the target and expiry of any unexpired `outgoingInvitation`, without sending anything.
- [x] 1.2 Wire `SettingsWindow`'s Send Invite button to show an explicit confirmation naming the outstanding invite's target (via 1.1) before calling through; sending without an outstanding invite is unchanged.
- [x] 1.3 `HandleAcknowledgementTellAsync`'s existing `outgoing.InvitationId != invitationId` check already ignores a confirmation tell for a replaced (now-orphaned) invite unchanged by this work - confirmed by reading the code path, no automated regression coverage (see note below).

## 2. Bounded retry on the inviter's acknowledgement handling

- [x] 2.1 Wrap `HandleAcknowledgementTellAsync`'s fetch/verify/consume sequence in a bounded retry loop (5 attempts, 2/4/8/16/16s backoff) that only retries on transient `RelayException` codes (`network`, `service_unavailable`, `rate_limited`); non-transient codes (`unauthorized`, invalid acceptance shape) still fail on the first attempt.
- [x] 2.2 Call `SetError` with a user-facing message when every retry is exhausted, so `LastError`/`LastErrorChanged` fire the same way other `PairingService` failures already do - `CollarWindow`/`SettingsWindow` render it with no further UI changes needed.

## 3. Invite target validation

- [x] 3.1 Add `ChatComposer.TryValidateTellTarget` - a static "Name Surname@World" shape check (non-empty name/world, exactly one `@`).
- [x] 3.2 Call the validator from `PairingService.CreateAndSendInvitationAsync` before creating anything; on failure, `SetError` the rejection reason and send nothing. (Centralized here rather than in `SettingsWindow` specifically, since `SettingsWindow` already renders `LastError` - one source of truth instead of two.)

## 4. Shorter invitation id / proof digest wire encoding

- [x] 4.1 Update `protocol/constants.json` and `protocol/schemas/common.schema.json`/`acceptance.schema.json` to the new, shorter invitation id (128-bit/22 chars) and proof digest (128-bit/32 hex chars) shapes; update the affected fixture.
- [x] 4.2 Add `RelayCrypto.RandomInvitationId()`/`RandomProofDigestHex()` and use them in `PairingService` in place of the 256-bit/SHA-256 generators.
- [x] 4.3 Update `worker/src/lib/capability.ts`'s capability pattern and add `worker/src/lib/validate.ts`'s `isProofDigestHex`, used by `worker/src/routes/invitations.ts`'s accept handler; update `worker/test/helpers.ts`'s default proof digest and the one hardcoded 64-char value in `worker/test/invitation-envelope.spec.ts` to match; `npm test` passes.
- [x] 4.4 Confirm `ComposeRelayInvitation`/`ComposePairingAck` produce shorter tells end-to-end with a manual pairing test between two real characters - verified by the user in-game.

## 5. Verification

- [x] 5.1 No automated plugin-side test project exists for this change (`Oathbound.Plugin.Tests` was removed - see design.md "Removing Oathbound.Plugin.Tests..."); `dotnet build Oathbound.slnx` and `worker`'s `npm run typecheck`, `npm test`, and `npm run lint` all pass against the current tree.
- [x] 5.2 Manually pair two characters end-to-end (invite -> accept -> confirm -> activated), exercise the outstanding-invitation-replacement confirmation, and exercise the invalid-target rejection, in the running plugin - verified by the user in-game.

Note on removed automated coverage: `Oathbound.Plugin.Tests` could never have exercised `PairingService` directly - constructing it transitively requires `CollarCommand` -> `SlotLockManager`/`MoodlesCommand` -> `GlamourerIpc`/`MoodlesIpc`, and both IPC wrappers call the Dalamud-injected `Plugin.PluginInterface` directly in their constructors, which is unset outside a real plugin host. Tasks 1.3, 2.1, and 2.2's retry/ignore behavior is therefore verified by reading the code path in this change, not by an automated check; 4.4, 5.2 remain manual, in-game verification for whenever that's next available.
