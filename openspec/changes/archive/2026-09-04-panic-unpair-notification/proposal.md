## Why

Panic (`/collarpanic`, the hotkey, or the header button) is purely local - `PanicHandler.Panic()` flips `config.Pairing.Paired = false` on the panicking client only, and nothing is ever sent to the other side (there is no relay or API to check against). Two concrete, verified problems fall out of that:

1. **The other side never finds out.** If a Sub panics, the Owner's header keeps showing "Owns: Sub@World" indefinitely, and the Owner tab's Send buttons stay enabled - the Owner can keep clicking Send, tells go out fine, and the now-unpaired Sub's plugin silently drops every one of them. There is no error, no signal, nothing distinguishing "it worked" from "it went nowhere."
2. **A pre-existing, independent bug**: `CollarWindow`'s `canSend` and `ChatComposer.Wrap`'s `/tell` targeting both check only whether a peer name/world was ever captured (`PeerName`/`PeerWorld` non-empty) - never whether pairing is actually still active (`Paired`). `PanicHandler.EndPairingLocally()` clears `Paired` but deliberately leaves `PeerName`/`PeerWorld` cached, so **a side that panics its own pairing away can still compose and send commands afterward**, even though its own header correctly says "Not paired." This is wrong regardless of anything else in this proposal.

## What Changes

- **Fix the send-gate bug**: `canSend` and `ChatComposer.Wrap`'s `/tell` addressing both require `Pairing.IsPaired` (which already implies a captured peer name/world) instead of just checking `PeerName`/`PeerWorld` presence. A side that has panicked its own pairing away can no longer compose or send anything until it pairs again.
- **Panic sends one best-effort notification to the peer it was just paired with.** As a direct, synchronous consequence of the local panic action itself (never a reaction to incoming chat), the panicking side sends a single `collarunpair` tell to the peer identity it had cached at the moment of panic, carrying its own role. This is the second narrow, explicit exception to "no automated sending" (the first being the one-way pairing handshake's confirmation tell) - same shape: one send, fired only as a direct result of one specific local action, addressed only to an identity already trusted as the peer, never a reply to unsolicited incoming chat.
- **The receiving side shows what happened and, where it's already safe to do so, offers to clear it:**
  - If the **Owner** receives notice that their Sub panicked: the header shows an explanation, and the existing "Release pairing" button (already an unconditional, Owner-only local action) is surfaced right there with that context - no new capability, just making the existing button appear when it's actually relevant.
  - If the **Sub** receives notice that their Owner panicked: the header shows an informational note. The Sub is not offered a new way to unpair - their pairing genuinely is still active and still locked until *their own* panic, and this proposal does not introduce any new way around that (a "soft unpair" for Sub is a real, separate consent-model change this proposal deliberately does not make).
- **Explicitly out of scope**: inferring a peer's silent disappearance (crash, uninstall, blocked, offline) with no notification tell. That would require either scraping FFXIV's own localized system chat messages for delivery failures (fragile, version- and language-specific), or having an unpaired client keep listening and auto-responding to tells from people it no longer trusts (a real information-leak risk in exactly the abusive-ex scenario this plugin's consent model exists to guard against). Neither is part of this change. The notification here is strictly best-effort: if it doesn't arrive, the receiving side simply never finds out via this mechanism, same as today.

## Capabilities

### Modified Capabilities
- `collar/pairing`: panic sends a best-effort notification to the peer it was paired with; the Owner-side header surfaces "Release pairing" in response to that notice.
- `collar/chat-transport`: adds the panic-notification tell as a second narrow, explicit exception to "No automated sending"; adds a new requirement that composing/sending requires active pairing, not merely a previously-captured peer identity (closing the bug above).

## Impact

- `CollarSystem.Plugin/Safety/PanicHandler.cs` - a new `RunStep` composes and sends the `collarunpair` notification, capturing the peer identity before any other step runs.
- `CollarSystem.Plugin/Commands/ChatComposer.cs` - gains composing for the `collarunpair` message; `Wrap` requires `Pairing.IsPaired` instead of raw `PeerName`/`PeerWorld` presence.
- `CollarSystem.Plugin/Commands/ChatCommandListener.cs` - recognizes the `collarunpair` keyword (checked alongside the existing `collarpair`/`collarpairack` keywords) and records a transient "peer unpaired" notice when the sender matches the locally cached peer.
- `CollarSystem.Plugin/UI/CollarWindow.cs` - `canSend` requires `IsPaired`; the character header shows the new notice and, for an Owner, surfaces "Release pairing" alongside it.
- **No breaking change**: the send-gate fix only stops something that was already a bug (sending after your own panic); the notification is new, additive, and best-effort.
