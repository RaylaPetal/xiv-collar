## Context

Pairing today (`PairingCommand.cs`, `ChatCommandListener.TryHandlePairingMessage`, `ChatComposer.ComposePairing`) is a manual code exchange: each side generates `MyCode`, enters the other's as `PeerCode`, then each side independently composes and manually sends `collarpair <role> <code> <triggerPhrase>` to the other. Each side's listener checks the received code against its own `PeerCode` and, on a match, surfaces a `Pending` request naming the verified sender; each side must separately click Accept. `PairingCommand.AcceptPeer` only ever writes local config - nothing in this class or its call sites has ever sent a chat message, which is the architectural invariant `collar/chat-transport`'s "No automated sending" requirement encodes as a testable contract (only UI Send-button click handlers call `ChatSender.Send`).

`Card.Begin(id, size)` (`UI/Card.cs`) always wraps content in a fixed-size `ImGui.BeginChild`, which silently clips or internally scrolls anything past that size. `DrawScanAndExportCard` already abandoned this wrapper for the same reason this change addresses for Identity & Pairing and the ToS card - see its own comment in `SettingsWindow.cs`.

`RestraintCommand.CaptureDeviceFromItem` and `ItemPickerWindow` (from `restraint-gear-picker`, already shipped) are the direct precedent for this change's Collar-tab picker: `ItemPickerWindow.Open(ApiEquipSlot, Action<uint,string>)` is already generic over slot, so pointing one more call site at it with `ApiEquipSlot.Neck` requires no changes to the picker itself.

See proposal.md - Why/What Changes for the six individual problems and their intended fixes.

## Goals / Non-Goals

**Goals:**
- Pairing completes with one send + one click total, not two sends + two clicks.
- The "no automated sending" invariant gets exactly one explicit, narrow, documented exception - not a general loosening.
- Settings' most important cards (Identity & Pairing, ToS, Test command) are always fully visible at the window's minimum supported size.
- Exactly one local-test surface remains, with strictly better coverage than what it replaces.

**Non-Goals:**
- Changing the shared-code anti-spoof mechanism itself (still two manually-exchanged codes) - only the number of sends/clicks needed to complete pairing changes.
- Any change to how ongoing (post-pairing) command tells are matched or dispatched.
- Redesigning the collar item's stain/dye handling - the new picker follows `restraint-gear-picker`'s precedent (undyed, stain 0/0).
- Locking anything for the Owner side - only the Sub's identity configuration locks while paired, per the proposal's explicit scope.

## Decisions

**Confirmation tell is verified by echoing the initiator's own code back, not a new shared secret.** When a Sub/Owner accepts a pending request, the code that request matched was necessarily the accepting side's `PeerCode`, which equals the inviter's `MyCode`. The confirmation tell (`collarpairack <role> <code> <triggerPhrase>`) echoes that same code back. The inviter checks the incoming code against their own `config.Pairing.MyCode` - only the party who actually received and accepted the original invite could ever produce a match, so no new identity or secret needs to be introduced. Alternative considered: have the inviter remember exactly who they sent an invite to and match on sender identity instead of a code - rejected, since the inviter never learns the target's name/world programmatically today (the invite is copied to clipboard and sent via a manual `/tell` the user types themselves), so there is no stored "expected sender" to match against without new state; the code-echo approach needs none.

**The confirmation send lives in `ChatCommandListener.AcceptPending`, not `PairingCommand.AcceptPeer`.** `PairingCommand` is explicitly documented today as "no network round-trip anywhere in this file" and stays that way - it only ever writes local config. `AcceptPending` (in `ChatCommandListener`) already orchestrates the multi-step "accept" action (calling `PairingCommand.AcceptPeer`, then conditionally `CollarCommand.ForceApply`), so it is the natural place to add one more conditional step: composing the confirmation text via `ChatComposer` and sending it via `ChatSender`, addressed to the sender identity already captured on the `PendingPairingRequest`. This keeps `PairingCommand` a pure state-mutation class and keeps every actual chat-send call physically located next to the others (`ChatSender.Send` call sites), rather than scattering the one exception into a class that otherwise sends nothing.

**Incoming confirmation tells are recognized by a distinct keyword (`collarpairack`), not by shape-sniffing the existing `collarpair` message.** `TryHandlePairingMessage` already distinguishes messages purely by keyword prefix; adding a second keyword keeps that dispatch trivial and avoids any ambiguity between an invite and a confirmation that happen to have the same token count.

**Sub-side identity lock is enforced in the UI layer only (`SettingsWindow.cs`), not in the config setters.** `PluginConfig`/`PairingCommand` remain simple property/method writers with no awareness of "am I locked right now" - `SettingsWindow.DrawIdentityCard` already computes UI state (card height, warnings) from `config.Pairing.IsPaired` and `config.Role`, so gating the Role combo, code inputs, and trigger-phrase input on `!(config.Role == PluginRole.Sub && config.Pairing.IsPaired)` is a pure rendering-layer change (`ImRaii.Disabled`), consistent with how every other conditional-editability rule in this window already works (e.g. `CaptureCurrentAsDevice`'s slot-lock-active refusal is enforced at the command layer, but pairing's own "Sub can't unpair except via panic" has always been UI-only - see `PairingCommand.ReleasePeer`'s comment).

**Removing per-action Test buttons removes `LocalTestCoordinator` entirely, not just its call sites.** Every method on `LocalTestCoordinator` exists solely to back one of the per-action Test buttons being removed; once none of them are called, the class has no remaining purpose (it never gates anything else). `HideTestControls` (`PluginConfig`) is removed the same way - it only ever gated visibility of those same buttons.

**The Collar tab's picker reuses `ItemPickerWindow` unmodified**, calling `plugin.ItemPickerWindow.Open(ApiEquipSlot.Neck, (itemId, _) => ...)` exactly the way Restraints' capture UI does, storing the result via a new `CollarCommand.ConfigureFromItem(ulong itemId)` that mirrors `CaptureDeviceFromItem`'s shape (no Glamourer read, stain 0/0). `CaptureCurrentAsCollar` (the equip-first method) is removed, matching `CaptureCurrentAsDevice`'s removal in the prior change.

## Risks / Trade-offs

- **The one automated-send exception is a real, if narrow, precedent change to this plugin's core automation-risk story.** → Mitigated by scoping it as tightly as possible (exactly one tell, fired only as the direct synchronous result of one explicit Accept click, never retried, never fired from any other code path) and documenting it explicitly and prominently in the README's Automation risk section, using the same disclosure pattern already used for Gesture's temporary Penumbra activation and Restraints' Gagged chat-mangling.
- **A user who clicks Accept while genuinely offline/disconnected won't have their confirmation tell delivered**, leaving the inviter never auto-completing. → No different from today's failure mode (a manually-sent handshake tell can just as easily not arrive); either side can simply send a fresh invite.
- **Removing the per-action Test buttons is a real capability loss for a Sub who specifically wanted to test one action's underlying apply path without composing exact trigger-phrase text.** → The remaining "Test an Owner command" control covers the identical underlying action through the identical dispatch path (per `collar/chat-transport`'s existing requirement text) - the only added friction is typing the trigger phrase and alias, which is one line at most, and is worth it for the amount of the UI it declutters.
