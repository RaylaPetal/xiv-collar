## Why

The first working build surfaced real UX friction: pairing requires typing your own character name twice (once as Owner, once as Sub) for information the game already has; gesture/relay setup is buried inside accordions with no indication of *why* something is empty; and command failures (permission rejections, delivery failures, a dropped relay connection) currently go nowhere but the Dalamud log — the plugin windows show no sign anything went wrong. None of this blocks the four control categories from working, but all of it makes the plugin feel unfinished and hard to operate correctly. This change closes those gaps and gives the UI a visual pass before any further control-surface integration work.

## What Changes

- **Pairing identity uses the character name automatically.** Both Owner and Sub identify themselves with `ClientState.LocalPlayer`'s name instead of a free-text field — removes two redundant text inputs and closes a spoofing gap (a free-text name could currently claim to be anyone).
- **Pairing code gets a copy-to-clipboard button** next to its display on the Sub's side.
- **Settings window becomes the single place for pre-pairing setup**: role, relay URL, gesture folder allowlist, and the ToS acknowledgement all move there (or are added there, for the allowlist/ToS which currently live in the Sub window's Gesture accordion) — configurable independent of whether a pairing exists yet.
- **Command outcomes are surfaced to the user, not just logged**: a rejected command (permission disabled), a failed delivery (peer offline), and a lost relay connection each produce an in-game toast via `INotificationManager`, instead of being visible only in the Dalamud log.
- **Automatic relay reconnection** with a visible connection-status indicator (connected / reconnecting / disconnected) shown in both the Owner and Sub windows, replacing the current "connect once, silently stop working if it drops" behavior.
- **DomWindow restructured from stacked accordions to tabs** (Title / Outfit / Gesture / Follow), each labeled with an icon; a matching visual pass (icons, connection-status color language) applied to SubWindow.
- **Settings gear icon** in each window's title bar (`Window.TitleBarButtons`, `FontAwesomeIcon.Cog`) replaces the current full-width "Settings" text button in both DomWindow and SubWindow.
- **BREAKING**: none — no wire-protocol or IPC contract changes; this is UI/UX and reliability behavior only.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `collar/pairing`: identity establishment during the handshake now comes from the local character name rather than free-text entry, closing a spoofing gap in how a peer's displayed name is derived.
- `collar/relay`: strengthens the existing "surface a delivery failure to the Owner" and rejection-ack requirements to mean *visible to the user*, not just returned over the wire; adds automatic reconnection with a visible connection-status requirement.

## Impact

- `CollarSystem.Plugin/UI/DomWindow.cs`, `SubWindow.cs`, `SettingsWindow.cs` — UI restructuring (tabs, icons, gear title-bar button, status indicators, allowlist/ToS relocation).
- `CollarSystem.Plugin/Commands/PairingCommand.cs` — drop free-text name parameters in favor of `ClientState.LocalPlayer` name.
- `CollarSystem.Plugin/Relay/RelayClient.cs` — add reconnect-with-backoff logic around the existing `ConnectionLost` event.
- `CollarSystem.Plugin/Plugin.cs` — wire `RelayClient.ConnectionLost` / `AckReceived` (rejected) / `DeliveryFailed` to `INotificationManager`, and expose connection status to the UI layer.
- No relay server (`CollarSystem.Relay`), IPC wrapper, or wire-protocol changes.
- Out of scope for this change (recorded as an assumption, not a rejection): outfit item selection remains raw item-ID entry rather than a design-string paste or item picker, and the outfit lock key remains manually entered rather than auto-generated — both are real usability gaps noted during exploration but are new-feature-shaped rather than "fixing what's broken," and were not part of what was asked for here.
