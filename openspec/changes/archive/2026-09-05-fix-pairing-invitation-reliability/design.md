## Context

See `proposal.md` for motivation. The relevant code is `Oathbound.Plugin/Relay/PairingService.cs` (both roles' state machine), `Oathbound.Plugin/Relay/RelayClient.cs` (transport, never retries itself - retry is explicitly the caller's job per its own doc comment), `Oathbound.Plugin/UI/SettingsWindow.cs` (invite/accept buttons), and the wire shapes in `protocol/` shared with `worker/src/routes/invitations.ts`. The original relay design (`openspec/changes/archive/2026-09-04-add-cloudflare-pairing-catalog-relay/design.md`) already called for "a bounded resend/recheck" on a lost acknowledgement tell; that was implemented on the accepter side (`AwaitActivationAsync`, 40 attempts x 3s) but never on the inviter side (`HandleAcknowledgementTellAsync`), which is the gap this change closes.

## Goals / Non-Goals

**Goals:**
- Make the two silent-failure paths (outstanding-invitation clobber, unretried ack-consume) either impossible or visibly reported.
- Catch a malformed invite target before it turns into an unobservable failed `/tell`.
- Shrink the invitation id / proof digest wire encoding without changing their security properties.

**Non-Goals:**
- Changing the identity-binding model - both handshake tells stay mandatory (see proposal.md - Impact).
- Supporting more than one outstanding invitation at once - the one-invitation-at-a-time model from the original design ("One click, one invitation, one tell") is kept; this change makes replacing it explicit rather than silent.
- Retrying across plugin restarts beyond what `PendingRelayOperations` already recovers.

## Decisions

**Confirm-before-replace, not multi-invitation tracking.** `outgoingInvitation` stays a single slot. `CreateAndSendInvitationAsync` gains a pre-check: if `outgoingInvitation is { }` and it hasn't expired, the caller (Settings UI) must show a confirmation naming the existing target before calling through. This is simpler than tracking a set of outstanding invitations and matches the original one-invitation intent - it just stops it from happening silently.

**Bounded retry mirrors the existing accepter pattern.** `HandleAcknowledgementTellAsync`'s fetch/verify/consume sequence gets wrapped in the same shape as `AwaitActivationAsync`: a small number of attempts (proposed: 5) with short exponential backoff (proposed: 2s, 4s, 8s, 16s, 16s - bounded well under the tell/invitation's own expiry), catching only `RelayException` with transient codes (`network`, `service_unavailable`, `rate_limited`) and retrying those; anything else (`unauthorized`, malformed acceptance) still fails immediately since retrying won't change a verification outcome. `SetError` is called on final failure so `LastError`/`LastErrorChanged` surface it exactly like every other failure path in this class already does - no new UI plumbing needed, `CollarWindow.cs`/`SettingsWindow.cs` already render `pairingService.LastError`.

**Target validation is a plain shape check, not a game-state lookup.** The plugin has no way to ask FFXIV "does this character exist" - validation is limited to structural shape (`Name Surname@World`, non-empty segments). This catches the most common typo class (forgetting `@World` entirely, or leaving stray whitespace) without pretending to guarantee deliverability.

**Shorter invitation id / proof digest encoding.** Both are currently full-entropy values (`RandomCapabilityId` = 32 random bytes base64url = 43 chars; proof digest = SHA-256 hex = 64 chars) sized for the *storage-side* capability/collision requirements, not for tell-length economy. Reduce to a shorter encoding that still gives adequate collision resistance for a single-use, ~15-minute-lived, per-device-rate-limited token (e.g. 16 bytes / ~22 base64url chars for the invitation id; the proof digest can similarly be truncated to a shorter hex prefix, since its role is exact-match comparison against one specific accepted invitation, not global uniqueness). This is a `protocol/` change: `constants.json`/`schemas/*.schema.json` capability-shape validators and `vectors/*.json` must be updated together with `Oathbound.Plugin/Relay/RelayCrypto.cs` and `worker/src/lib/capability.ts`'s `isValidCapabilityShape`, per `CLAUDE.md`'s existing rule that `protocol/` is the cross-runtime source of truth and both runtimes' tests are what enforce agreement.

## Risks / Trade-offs

- [Shortening the invitation id reduces its entropy] -> still far beyond brute-force range for a 900-second-lived, single-use, rate-limited (10/hour/device) token; existing quotas (`worker/src/lib/quotas.ts`) already bound guess attempts more tightly than entropy alone would need to.
- [Retrying `HandleAcknowledgementTellAsync` adds latency before an error surfaces] -> bounded backoff (proposed ~46s total across 5 attempts) stays well inside the 15-minute invitation/acceptance expiry, so it cannot itself cause a false expiry.
- [Confirm-before-replace adds one extra click to the legitimate "I sent to the wrong person, let me redo it" flow] -> accepted, since the alternative is the silent-orphan bug this change exists to fix.
- [protocol/ format change is a coordinated two-runtime edit] -> `worker/test/vectors.spec.ts` gates on `protocol/vectors/crypto-vectors.json` per CLAUDE.md, but the plugin side has no automated test project (removed as part of this change - see below) and needs manual in-game verification against a running relay after both sides are updated.
- [Removing `Oathbound.Plugin.Tests` drops all of its prior coverage, not just the pieces this change couldn't extend] -> deliberate, user-directed trade-off: the harness could not exercise `PairingService` at all (constructing it transitively requires `CollarCommand` -> `SlotLockManager`/`MoodlesCommand` -> `GlamourerIpc`/`MoodlesIpc`, and both IPC wrappers call `Plugin.PluginInterface` directly in their constructors, which is `null` outside a real Dalamud host), so its retry/replacement logic was always going to depend on manual verification regardless; the project is gone rather than kept around for the narrower set of checks (gesture resolution, config shape, `RelayClient` transport wiring) it could still cover.
