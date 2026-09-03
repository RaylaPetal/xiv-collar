## Why

The relay (self-hosted websocket server) is the one piece of this plugin that costs money and requires ongoing hosting to keep working - a VPS bill, or a free-tier tunnel/service someone has to keep running. In-game chat already reaches both players' clients for free, via infrastructure Square Enix already runs; the only reason it wasn't used originally was automation risk (a plugin auto-sending chat is ToS-relevant). Splitting the design so the *Owner always types the trigger message themselves* - the plugin composes/copies it, never sends it - removes that risk entirely for the send side, while the Sub's client passively reading incoming tells is an established, uncontroversial pattern. That reframing is what makes chat viable as the transport, and it eliminates hosting, cost, and the pairing-code handshake (a game server-verified chat sender already proves identity; no shared secret needed).

## What Changes

- **`CollarSystem.Relay` is removed entirely** - no server, no hosting, no cost, no `RelayClient`/websocket transport.
- **Owner -> Sub commands travel as `/tell` messages the Owner types themselves.** A configurable trigger phrase (default e.g. `command`) precedes a short alias (`command strip`, `command leash-off`). The plugin never sends chat on the Owner's behalf - it can compose the exact text and put it on the clipboard for the Owner to paste, but the Owner's own keypress is what sends it.
- **The Sub's client listens for incoming tells** (`IChatGui.ChatMessage`, `TellIncoming`) from a *configured* sender name, matches the trigger phrase, resolves the alias against a locally-defined dictionary, and applies it - the same IPC calls (Glamourer/Penumbra/Honorific) as today, just triggered by a chat match instead of a deserialized relay payload.
- **Pairing becomes configuration, not a handshake.** The Sub types their Owner's exact character name into Settings and flips an explicit "Paired" toggle (the toggle *is* the consent action - nothing reacts to any tell until it's on). The Owner separately configures the Sub's name (used to pre-fill the `/tell` target when composing). No pairing code, no code exchange, no relay round-trip. Identity enforcement no longer relies on a shared secret - FFXIV's own server guarantees a tell's sender name can't be forged.
- **No live acknowledgements or connection status.** There is no persistent connection to reflect ("Connected/Reconnecting" disappears), and no ack channel to carry "applied/rejected/failed" back. A command's outcome is observed directly (the outfit changes, or it doesn't); an offline Sub causes FFXIV's own "cannot find character" system message, which is not something Collar's UI intercepts.
- **Gesture/Wardrobe catalog auto-sharing is dropped.** Without a live channel, the Sub's locally-scanned catalog (Penumbra gesture mods, Glamourer designs) can no longer be automatically pushed to the Owner's window. The Sub still scans locally (unchanged) to help decide what to name each alias; telling the Owner what aliases exist becomes a manual/negotiated step, same as sharing the character name during pairing.
- **BREAKING**: Existing pairings (relay-based) do not carry over - anyone using the current build re-pairs under the new model. No wire-protocol compatibility is attempted or needed, since the whole wire protocol is being removed.

## Capabilities

### New Capabilities
- `collar/chat-transport`: trigger-phrase-based command delivery over in-game tells - sender-identity gating, alias resolution, and the human-types-the-send-step guarantee that keeps this off the automation-risk list.

### Modified Capabilities
- `collar/pairing`: "Explicit pairing handshake" removed and replaced by a new "Configured-identity pairing consent" requirement - configured name+world plus an explicit consent toggle, no code, no relay round-trip. "Scoped, revocable permissions" and "Local panic/safeword" are unchanged - neither ever depended on relay-specific wording once re-read against the actual spec text, so no delta is needed for them.
- `collar/gesture`: "Catalog shared with paired Owner" requirement removed (see Removed Capabilities below - it is the one gesture requirement that named the old transport directly).

### Removed Capabilities
- `collar/relay`: retired entirely. All four requirements (command delivery channel, acknowledgement/current-state reply, delivery-to-connected-clients, automatic reconnection/connection status) described relay-specific mechanics that no longer exist under the chat transport. Superseded by `collar/chat-transport`.

## Impact

- Deleted: `CollarSystem.Relay/` (the entire project), `CollarSystem.Plugin/Relay/RelayClient.cs`, `Relay/Protocol.cs`'s websocket wire types.
- Rewritten: `CollarSystem.Plugin/Commands/PairingCommand.cs` (configured-name + consent toggle, no code/handshake), `CommandDispatcher.cs` (dispatches from parsed chat matches, not deserialized relay envelopes), every `*Command.cs` handler's entry point (still call the same IPC wrappers, just triggered differently).
- New: a chat listener (`IChatGui.ChatMessage` subscriber) and an alias-resolution layer on the Sub's side; a trigger-composer (with clipboard copy) on the Owner's side.
- UI impact: `DomWindow`'s per-category "send" actions become "compose and copy to clipboard" instead of direct network sends; the connection-status strip and Owner-side gesture/wardrobe catalog browsing (added in the last two changes) are removed since there's nothing live to show; `SettingsWindow` gains the Owner-name/Sub-name fields and the alias-definition editors (replacing the pairing-code/relay-URL fields).
- No server, dependency, or hosting changes needed going forward - `Penumbra.Api`, `Glamourer.Api`, `ECommons`, and Dalamud's own `IChatGui` service are the only integration points left.
