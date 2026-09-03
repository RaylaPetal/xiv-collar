## 1. Remove Relay Infrastructure

- [x] 1.1 Delete the `CollarSystem.Relay/` project entirely and remove its reference from `CollarSystem.slnx`, and verify the solution still builds with only `CollarSystem.Plugin` remaining.
- [x] 1.2 Delete `CollarSystem.Plugin/Relay/RelayClient.cs` and the websocket-specific parts of `Relay/Protocol.cs` (`CommandEnvelope`, `AckEnvelope`, `DeliveryFailedEnvelope`, `RelayFrame`), and verify no remaining file references them (build will surface any missed reference).
- [x] 1.3 Remove `RelayUrl` from `PluginConfig` and delete the Dockerfile/fly.toml/README sections describing relay hosting, and verify the README no longer instructs anyone to deploy or run a relay.

## 2. Config Model for Chat-Based Pairing (`collar/pairing`)

- [x] 2.1 Replace `PairingState` (`PairingId`/`PeerName`/`Confirmed`) with a configured-identity shape (`PeerName`, `PeerWorld`, `Paired: bool`) on both Owner and Sub sides of `PluginConfig`, and verify the new shape round-trips through `SavePluginConfig`/`GetPluginConfig` correctly (save, reload, confirm values persist).
- [x] 2.2 Add a configurable trigger phrase field (`PluginConfig.TriggerPhrase`, sensible default) and verify it persists and is used consistently by both the Sub's parser and the Owner's composer.
- [x] 2.3 Add the alias dictionary config shape (category -> alias name -> the local action it maps to: title text/color, outfit design id or slot/item, gesture mod/emote, follow engage/release), and verify it persists correctly.

## 3. Sub-Side Chat Listener (`collar/chat-transport`)

- [x] 3.1 Subscribe to `IChatGui.ChatMessage`, filter to `XivChatType.TellIncoming` only, and verify (via a local test harness or logged output) that non-tell channels (party/say) are never passed to the parser, satisfying "Non-tell channels are never processed."
- [x] 3.2 Verify the incoming sender's name+world against the Sub's configured Owner (`PeerName`/`PeerWorld`) and the `Paired` flag before any parsing occurs, and verify a mismatched sender or `Paired = false` results in the message being fully ignored, satisfying "Unconfigured or unmatched sender cannot command."
- [x] 3.3 Parse the message for the configured trigger phrase followed by an alias (case-insensitive, trimmed), look the alias up in the local alias dictionary, and verify a known alias resolves to its mapped action while an unknown alias produces no game-state-changing action, satisfying "Alias resolution against a locally-defined dictionary."
- [x] 3.4 Wire resolved aliases to the existing IPC wrappers (`GlamourerIpc`, `HonorificIpc`, `PenumbraIpc`, `MovementLockService`) and existing safety logic (gesture still requires Sub confirmation before firing, outfit lock/key model unchanged, movement-lock permission still gates follow separately), and verify each category still enforces the same local safety behavior it did under the relay transport.

## 4. Owner-Side Composer (`collar/chat-transport`'s "No automated sending")

- [x] 4.1 Build the exact `/tell <SubName>@<SubWorld> <trigger> <alias>` text for a selected command in `DomWindow`, and verify the plugin only ever copies this text to the clipboard (`ImGui.SetClipboardText`) and never calls any chat-send API, satisfying "Composing a trigger message does not send it."
- [x] 4.2 Verify there is no code path in the Owner-side UI that results in an automated send under any configuration - a deliberate negative test, since this is the core ToS-mitigation property the whole design leans on.

## 5. Pairing & Panic Updates

- [x] 5.1 Rewrite `PairingCommand.cs` around the new configured-identity model: setting the Owner/Sub name+world, and an explicit toggle for `Paired` that is the sole consent action, and verify enabling `Paired` without a configured peer name is rejected/disabled in the UI (can't accidentally pair with nothing).
- [x] 5.2 Update `PanicHandler.cs` to disable `Paired` (instead of calling the removed `relay.Disconnect()`) alongside the existing Glamourer revert / Honorific clear / movement-lock cancel, and verify the full panic sequence still completes correctly using only local state.

## 6. UI Rework

- [x] 6.1 Update `SettingsWindow` with Owner/Sub name+world fields, the trigger-phrase field, and per-category alias editors (replacing the pairing-code and relay-URL fields), and verify each field persists and reflects the current config on reopen.
- [x] 6.2 Update `SubWindow`: remove the connection-status strip and pairing-code UI, replace with the configured-Owner display and the `Paired` toggle; keep the panic button and permission toggles unchanged, and verify the window still builds/renders with the new pairing section in place of the old one.
- [x] 6.3 Update `DomWindow`: remove the connection-status strip, the pairing-code entry flow, and the auto-synced gesture/wardrobe catalog browsing (nothing pushes it anymore); replace per-category "send" actions with the compose-and-copy flow from task 4.1, and verify each category (title/outfit/gesture/follow) can still compose a correct trigger message.
- [x] 6.4 Remove the now-unused `ConnectionStatusView.cs`/`NavBar` connection-state wiring tied to `RelayClient.ConnectionState`, and verify the UI project builds clean with no dangling references to the deleted relay types.

## 7. Documentation

- [x] 7.1 Rewrite the README's setup/"Activating in-game" sections to describe the chat-based pairing flow (configure names, enable Paired, set up aliases) with no relay-hosting instructions at all, and the automation-risk disclosure updated to reflect that only the Owner's own typed messages leave the client, and verify it accurately describes the shipped flow.
