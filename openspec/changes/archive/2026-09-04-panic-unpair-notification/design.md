## Context

`PanicHandler.Panic()` (`Safety/PanicHandler.cs`) runs a fixed sequence of local-only `RunStep`s (each isolated in its own try/catch so one failure never blocks the rest), the first of which is `pairing.EndPairingLocally()` - which sets `config.Pairing.Paired = false` but deliberately leaves `PeerName`/`PeerWorld` cached (unlike `ReleasePeer()`, the Owner-only explicit unpair, which clears both). Nothing here has ever sent a chat message.

`fix-owner-command-delivery` (already shipped) established `TryHandlePairingMessage`/`TryHandlePairingAckMessage` in `ChatCommandListener.OnChatMessage` as the pattern for recognizing a keyword-prefixed incoming tell regardless of current pairing state, and `pairing-and-ui-polish` (already shipped) established the first narrow exception to `collar/chat-transport`'s "No automated sending": `AcceptPending` composes and sends a `collarpairack` tell as a direct, synchronous consequence of one explicit Accept click. This change adds a second instance of that same exception shape, triggered by panic instead of Accept.

`CollarWindow.cs`'s `canSend` (line ~757, at time of writing) and `ChatComposer.Wrap` both currently gate on `!string.IsNullOrWhiteSpace(pairing.PeerName) && !string.IsNullOrWhiteSpace(pairing.PeerWorld)` - which was correct back when those two fields only became non-empty once genuinely paired, but stopped being correct the moment `EndPairingLocally` started leaving them populated after `Paired` flips false. `PluginConfig.PairingState.IsPaired` already exists and is the correct check (`Paired && PeerName/World non-empty`) - it's just not used at these two call sites.

See proposal.md - Why for the two problems this fixes and What Changes for the shape of the fix.

## Goals / Non-Goals

**Goals:**
- A side that has panicked its own pairing away can never again compose or send a command to the former peer without pairing again.
- The peer of a panicking side gets a best-effort, one-shot notification, using the same narrow-exception shape already established for pairing acceptance.
- The Owner-side response to that notification introduces no new capability - it only makes the existing "Release pairing" button appear with context.

**Non-Goals:**
- Any way for a Sub to unpair short of their own panic - this proposal does not touch that consent-model invariant at all.
- Detecting a peer's silent disappearance (crash, uninstall, blocked, offline) when no notification arrives - see proposal.md's explicit scope note. No chat-log scraping, no "listen while unpaired" behavior.
- Guaranteeing delivery of the notification tell in any way - it is exactly as unreliable as any other `/tell`, by design (panic's own local effects must never depend on network/relay availability).

## Decisions

**The notification is sent from inside `PanicHandler.Panic()` itself, as one more `RunStep`, first in the sequence (right after capturing identity, before `EndPairingLocally` runs)** - not from `PairingCommand`. `PairingCommand` remains a pure state-mutation class with "no network round-trip anywhere in this file" (its own existing doc comment); `PanicHandler` is already the class that orchestrates a whole sequence of independent side effects, so adding one more `RunStep` here is consistent with its existing shape, and isolating it in its own `RunStep` means a send failure (network down, no target) can never block any of panic's own local, unconditional guarantees. The peer identity (`PeerName`/`PeerWorld`) and role are captured into local variables at the very start of `Panic()`, before any other step runs, so a future change to what `EndPairingLocally` clears can't silently break this.

**Recognized via a third keyword, `collarunpair`, checked in `OnChatMessage` alongside the existing `collarpair`/`collarpairack` checks** - same reasoning as `TryHandlePairingAckMessage`'s own addition: keyword-prefix dispatch is already established and trivial to extend, and (unlike the pairing invite/ack pair) `collarunpair` doesn't share a prefix with either existing keyword, so there's no check-ordering hazard here.

**Verified by comparing the sender against the receiver's own currently-configured peer name/world - no code involved.** Establishing a *new* trust relationship needs a shared secret (the existing code-match); *ending* one doesn't - the receiver already has a specific peer it trusts, and the only question is "is this message actually from them," which FFXIV's own verified sender identity already answers. This also means a `collarunpair` tell from anyone else is simply ignored, so this doesn't create any new "the plugin responds to strangers" surface - the exact concern that ruled out the broader "notify on any incoming tell while unpaired" idea explored earlier.

**The receiving side's notice is transient, in-memory state on `ChatCommandListener`** - a new `PeerUnpairedNotice` property plus a `PeerUnpairedNoticeChanged` event, the same shape `Pending`/`PendingChanged` already use for the pairing-request flow. Not persisted to `PluginConfig`: it's a one-time "here's what just happened" banner, not durable state, and it naturally clears itself once the user acts on it (Owner clicks Release; Sub dismisses the note) or a fresh pairing cycle begins.

**Owner's response reuses `PairingCommand.ReleasePeer()` unchanged.** The header already conditionally shows a "Release pairing" button for an Owner whenever `pairing.IsPaired`; after this change, it also shows (with different framing - "your Sub's side ended via panic") when `PeerUnpairedNotice` is set for an Owner, even if `IsPaired` happens to still read true (the Owner hasn't clicked anything yet, so their own `Paired` flag is technically still on). No new release path is introduced.

**Sub's response is display-only** - a `TextColored`/`WrappedColored` note in the header, no new button, no new method on `PairingCommand`. This is the direct consequence of the Non-Goal above: introducing a Sub-side unpair-without-panic path is a real, separate consent-model decision this change deliberately does not make.

**The `canSend`/`ChatComposer.Wrap` fix is a one-line condition change at each of the two call sites** (`pairing.IsPaired` in place of the two-field null check) - no new method, no new state; `IsPaired` already exists and already means exactly the right thing.

## Risks / Trade-offs

- **A second automated-send exception makes the "no automated sending" rule read as less absolute over time.** → Mitigated by keeping both exceptions textually enumerated together in the same requirement (see the chat-transport delta), each scoped identically tightly (one send, one direct local trigger, one already-trusted recipient) - the rule stays "every send is a click, with exactly two named, narrow exceptions," not an open-ended allowance.
- **The notification can arrive after the receiving side has already taken some action based on stale state** (e.g., Owner already mid-composing a command when the notice arrives). → No special handling needed: the sending side's own `canSend` fix means a genuinely-panicked side can't act on stale state either way, and the receiving side's UI simply updates on the next frame like any other reactive state in this codebase.
- **A user could misread the Owner-side notice as meaning the Sub is safe/fine** when actually the underlying reason for panic could be anything from a misclick to a genuine emergency. → Out of scope for this UI to solve; the notice states only the verifiable fact ("their side ended via panic"), not a judgment about why, matching how the rest of this plugin's UI already avoids speculating about intent.
