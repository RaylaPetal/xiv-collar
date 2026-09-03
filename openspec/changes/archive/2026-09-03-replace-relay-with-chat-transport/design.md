## Context

See proposal.md - Why for the motivation (eliminate relay hosting cost) and the reframing that makes it viable (automated *sending* is the ToS-relevant part, not automated *receiving/reacting* - see `collar/chat-transport`'s "No automated sending" requirement). Confirmed against this project's installed Dalamud build: `IChatGui.ChatMessage` is a real, standard event carrying sender, channel type, and message content for every incoming chat line, including tells (`XivChatType.TellIncoming`). `ECommons.Automation.Chat.SendMessage` (already used for gesture-triggering today) remains available but is deliberately *not* used for trigger messages under this design - see Decisions.

## Goals / Non-Goals

**Goals:**
- Zero hosting, zero cost, zero third-party account, for the life of the plugin.
- The Owner's own keypress is what sends every trigger message - the plugin never calls a chat-send function for a trigger.
- Preserve every existing local safety property unchanged: panic is still local-only, gesture still requires explicit Sub confirmation before firing, outfit locks still require the locking key to release, movement-lock permission is still separate and opt-in.

**Non-Goals:**
- Not attempting to preserve live acknowledgements, connection status, or automatic catalog sync - these depended on a persistent channel that no longer exists, and design.md's Risks/Trade-offs explains why that's an acceptable loss rather than something to work around.
- Not supporting multiple simultaneous Owners per Sub, or multiple Subs per Owner - unchanged from today's 1:1 pairing assumption, just re-expressed as configured names instead of a paired session.
- Not attempting backward compatibility with the relay-based wire protocol - proposal.md already marks this BREAKING; there is nothing to migrate on the wire, only the pairing state itself needs re-establishing.

## Decisions

### Sub listens, Owner only composes - not symmetric
Only the Sub's client subscribes to `IChatGui.ChatMessage`. The Owner's client has no listening responsibility at all under this design, because there's nothing for it to listen *for* - acknowledgements are gone (Non-Goals), so the Owner's plugin's job shrinks to: hold the Sub's configured name, build the exact `/tell <name>@<world> <trigger> <alias>` text for whatever command the Owner is composing, and put it on the clipboard. This is a meaningful scope reduction from the relay-based `DomWindow`, and it's why "No automated sending" (collar/chat-transport) only needs to be enforced on the composer, not on a second listener.

### Alias-based, not raw-payload - uniformly across all four categories
Every command (title, outfit, gesture, follow) reduces to a short alias the Sub predefines locally (e.g. `strip` -> apply Glamourer design X locked with key Y; `bow` -> trigger gesture mod Y's Doze emote; `leash-on` / `leash-off` -> fixed built-ins with no Sub-defined variant needed). Alternative considered: let title carry literal text after the trigger (`command title Good Girl`) since title text isn't sensitive - rejected for consistency; one mental model ("everything is a predefined alias") is easier to reason about and document than "titles are raw text but everything else is an alias," and it keeps color/prefix/glow configurable without needing to encode them in a chat string.

### Identity: configured name (+ world), not a shared secret
The Sub configures the Owner's exact character name **and home world** in Settings (world is required, not optional, to avoid a same-name-different-world false match); the Owner separately configures the Sub's name+world (used only to address the `/tell`, and pre-fill the composer). `IChatGui`'s incoming-tell sender is authoritative and cannot be spoofed by another player - this is strictly stronger identity proof than the removed pairing code ever provided (a code could be shared with the wrong person by mistake; a forged sender name cannot happen at all). "Paired" is a separate explicit boolean the Sub must enable after configuring the name - configuring the name alone does not enact consent, preserving collar/pairing's "never auto-accept" guarantee.

### No formal ack/delivery-confirmation UI
Alternative considered: have the Sub's client auto-print a local system-chat echo ("applied: strip") after acting, purely to its own client, not sent anywhere - this would be zero-automation-risk (nothing is transmitted) and could be a real quality-of-life addition. Deferred rather than rejected: it's additive and doesn't affect the wire/consent model, so it can be a follow-up rather than blocking this change. For now, the Owner's confirmation is watching the effect happen in-game, matching how OpenCollar itself is generally used.

### Gesture/Wardrobe catalog stays local-scan-only; sharing the alias list is manual
The existing Penumbra/Glamourer local scanning (unchanged, still automatic and unmanaged-tagging-free per collar/gesture's surviving requirements) continues to help the Sub decide what to name each alias. What's removed is only the automatic *push* of that catalog to the Owner's window - the Sub now tells the Owner what aliases exist the same way they'd share their character name during pairing: directly, out of band.

## Risks / Trade-offs

- **Typos silently do nothing.** An alias or trigger-phrase typo produces no error visible to the Owner (by design - nothing is sent back). Mitigated by exact-match plus a reasonable normalization pass (case-insensitive, trimmed whitespace) in the parser, and by the Sub being able to see a local log of recognized/unrecognized incoming aliases in their own window for troubleshooting.
- **No delivery confirmation if the Sub is offline.** FFXIV's own client shows the Owner a "player not found" system message for a tell to an offline character - real feedback, just not something Collar's UI can intercept or restyle. Acceptable: it's still *more* informative than the relay's silent queuing ever was for that case.
- **Tell rate limiting.** FFXIV throttles rapid repeated tells from one sender as an anti-spam measure. Irrelevant at this plugin's actual usage pattern (a person manually typing occasional commands), but worth remembering if a future "queue several aliases at once" convenience feature is ever considered.
- **Same-name-different-world collisions** are why World is a required part of both configured identities, not just a nice-to-have.
- **Losing acks/connection-status is a real UX step back from the previous change's work**, not merely a neutral trade - name it plainly rather than undersell it: the Owner will no longer see "Connected/Reconnecting" or a "command rejected" toast. The trade being made is that UX polish for zero hosting cost and a materially safer automation posture.

## Migration Plan

1. Remove `CollarSystem.Relay/` entirely and its reference from `CollarSystem.slnx`.
2. Existing installs: `PluginConfig.Pairing` (the old `PairingState` shape: `PairingId`/`PeerName`/`Confirmed`) is replaced by the new configured-name-plus-toggle shape - this is a breaking config shape change with no migration path attempted (matches proposal.md's BREAKING note); users simply reconfigure the Owner/Sub name once after updating, which takes seconds.
3. No server-side rollback needed since there is no server after this change - reverting is a plain code revert plus redeploying the relay if anyone still wants it.

## Open Questions

- Exact default trigger phrase (e.g. `command` vs something less likely to appear in ordinary RP chat) - doesn't change the requirements, approach, or task breakdown; can be picked at implementation time and left user-configurable regardless.
