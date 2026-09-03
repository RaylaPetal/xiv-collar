## 1. Relay Connection Status & Reconnect

- [x] 1.1 Add a `ConnectionState` enum (`Disconnected`, `Connecting`, `Connected`, `Reconnecting`) and a public property on `RelayClient` that tracks it through `ConnectAsync`/`Disconnect`/the receive loop, and verify the property reflects each transition via a local test harness (websocket connect/disconnect against the relay, same approach used to verify the relay earlier).
- [x] 1.2 Add an `intentionalDisconnect` flag to `RelayClient`, set before `Disconnect()` runs, and verify `ConnectionLost` does not fire a reconnect attempt when `Disconnect()` was called deliberately.
- [x] 1.3 Implement the background reconnect loop (exponential backoff: 1s/2s/4s/8s, capped at 30s) triggered only by an unexpected `ConnectionLost`, reusing the last-connected `Uri`, and verify it stops cleanly on `Dispose()` and on a subsequent intentional `Disconnect()`.
- [x] 1.4 Verify `PairingCommand.EndPairingLocally()` (called by panic and manual unpair) still completes correctly with the reconnect loop active, and does not trigger a reconnect after ending the pairing.

## 2. Command-Outcome Notifications

- [x] 2.1 Inject `INotificationManager` into `Plugin.cs` and subscribe to `RelayClient.AckReceived` (rejected/failed), `DeliveryFailed`, and `ConnectionLost`/reconnected transitions, mapping each to a toast per design.md's severity mapping, and verify each event path produces a call into `AddNotification` (unit-level, mocking the event args) since the actual toast rendering can't be observed outside the game.
- [x] 2.2 Verify no existing log-only error handling is removed - notifications are additive, the Dalamud log entries stay as the detailed diagnostic trail.

## 3. Pairing Identity from Character Name

- [x] 3.1 Update `PairingCommand.RequestPairingAsync` and `ExplicitAcceptAsync` to source the peer's own display name from `Plugin.ClientState.LocalPlayer`'s name instead of a passed-in string parameter, and verify the method signatures no longer require a name argument.
- [x] 3.2 Remove the `ownerName`/`subDisplayName` free-text `ImGui.InputText` fields from `DomWindow` and `SubWindow`, and verify the pairing request/accept buttons still compile and call the updated `PairingCommand` methods correctly.
- [x] 3.3 Add a copy-to-clipboard button (`ImGui.SetClipboardText`) next to the displayed pairing code in `SubWindow`, and verify it builds and calls the correct code string.

## 4. Settings Window: Pre-Pairing Setup

- [x] 4.1 Move the gesture folder allowlist editor (list, add, remove) from `SubWindow`'s Gesture section into `SettingsWindow`, and verify it still reads/writes `Configuration.GestureFolderAllowlist` correctly and works whether or not a pairing is active.
- [x] 4.2 Move the ToS-acknowledgement checkbox into `SettingsWindow`, and verify `SubWindow`'s Gesture/Follow permission checkboxes still correctly gate on `Configuration.TosAcknowledged` after the move.
- [x] 4.3 Add a one-line inline hint in `SubWindow` near the Gesture/Follow checkboxes when `TosAcknowledged` is false, pointing the user at Settings, and verify it disappears once the ack is set.

## 5. Title-Bar Settings Gear

- [x] 5.1 Add a `Window.TitleBarButtons` entry (`FontAwesomeIcon.Cog`) to `DomWindow` and `SubWindow` that calls `plugin.ToggleSettingsUi()`, and verify both windows build with the gear button registered.
- [x] 5.2 Remove the full-width "Settings" text button from both windows' `Draw()` methods, and verify no other code path depended on that specific button existing.

## 6. Connection Status Indicator

- [x] 6.1 Add a small status strip (colored dot + text: green/"Connected", amber/"Reconnecting", gray-red/"Disconnected") near the top of both `DomWindow` and `SubWindow`, reading `RelayClient.ConnectionState`, and verify the color mapping matches design.md's "distinct from panic-red" decision.

## 7. DomWindow Tab Restructure

- [x] 7.1 Replace the four `ImGui.CollapsingHeader` sections (Title/Outfit/Gesture/Follow) with `ImGui.BeginTabBar`/`BeginTabItem`, reusing each existing `DrawXSection` method's body unchanged inside its tab, and verify the plugin builds with pairing status and the connection indicator still rendered above the tab bar.
- [x] 7.2 Add a `FontAwesomeIcon` prefix to each tab label (Title/Outfit/Gesture/Follow) and to `SubWindow`'s equivalent section headers, and verify the icons render from a valid `FontAwesomeIcon` enum member (build-verified; visual confirmation requires the game running).

## 8. Documentation

- [x] 8.1 Update the README's "Activating in-game" / usage section to reflect the new gear-icon settings entry point and the relocated allowlist/ToS setup, and verify it accurately describes the shipped flow.
