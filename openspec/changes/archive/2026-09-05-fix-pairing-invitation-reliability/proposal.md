## Why

Relay-assisted pairing has two silent failure modes that make "click Accept" or "send an invite" appear to fail for no visible reason: sending a second invitation silently orphans the first (so an earlier Accept never activates and the accepter waits ~2 minutes for a timeout), and a transient relay hiccup while the inviter is processing the acknowledgement tell is swallowed into a log line with no retry and no user-facing error. Both push users into repeated invite attempts, which then exhausts the relay's per-device rate limit and reads as "the relay is broken." Separately, hand-typing the invite `/tell` target is a plausible, invisible cause of invites never arriving at all, and the invitation/acknowledgement tell payload is larger than it needs to be.

## What Changes

- Warn and require explicit confirmation before replacing an outstanding outgoing invitation, instead of silently overwriting it (`PairingService.CreateAndSendInvitationAsync`) - the previous invitation is invalidated with the user's knowledge rather than becoming an orphan that never activates.
- Add a bounded retry with backoff around the inviter's fetch/verify/consume steps in `HandleAcknowledgementTellAsync`, matching the accepter's existing `AwaitActivationAsync` poll pattern; surface a `LastError` if every attempt fails instead of only logging it.
- Add a one-click "Send Invite" action in Settings that composes and sends the `/tell` directly via `ChatSender`, rather than requiring the user to hand-type or paste the composed text into chat themselves.
- Shorten the wire encoding of the invitation id and acceptance proof digest carried in the `collarinvite`/`collarpairack` tells, reducing tell length and typo surface while keeping equivalent entropy/collision resistance.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `collar/pairing`: adds requirements that (a) starting a new invitation while one is already outstanding requires explicit user confirmation naming what is being replaced, (b) the inviting side retries a bounded number of times before giving up on an acknowledgement tell and surfaces an error if it never succeeds, and (c) the UI offers a one-click action to send the invite tell directly instead of requiring it to be hand-typed.

## Impact

- `Oathbound.Plugin/Relay/PairingService.cs` - outstanding-invitation replacement confirmation; bounded retry/backoff and error surfacing in `HandleAcknowledgementTellAsync`.
- `Oathbound.Plugin/UI/SettingsWindow.cs` - confirmation prompt before replacing a pending invitation; one-click "Send Invite" action.
- `Oathbound.Plugin/Commands/ChatSender.cs` / `ChatComposer.cs` - invoked directly from the new one-click send action.
- `Oathbound.Plugin/Relay/RelayCrypto.cs`, `RelayEnvelopes.cs` - shorter invitation id / proof digest encoding.
- `protocol/` (`constants.json`, `schemas/*.schema.json`, `vectors/*.json`) and `worker/src/routes/invitations.ts`, `worker/src/lib/capability.ts` - the invitation id / proof digest shape is part of the cross-runtime wire contract and must change on both sides together, per the existing byte-for-byte agreement requirement.
- No changes to the identity-binding model itself: both handshake tells remain required (see `openspec/changes/archive/2026-09-04-add-cloudflare-pairing-catalog-relay/design.md` - "Character identity cannot be proven to an HTTP service").
