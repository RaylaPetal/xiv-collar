## Context

See proposal.md - Why. Confirmed directly against this project's installed Dalamud build (not assumed): `Dalamud.Interface.Windowing.Window.TitleBarButtons`, `FontAwesomeIcon`, and `INotificationManager.AddNotification` all exist and are usable as-is. `RelayClient` already exposes `ConnectionLost`, `AckReceived`, and `DeliveryFailed` events (see `Relay/RelayClient.cs`); nothing currently subscribes to them - this change wires them up rather than inventing new plumbing.

## Goals / Non-Goals

**Goals:**
- Every command outcome and connection-state change that currently only reaches the Dalamud log becomes visible inside the plugin's own UI.
- Pre-pairing setup (role, relay URL, gesture allowlist, ToS ack) is discoverable in one place before a Sub ever generates a pairing code.
- Visual structure (tabs, icons, status color) makes it obvious at a glance what state the plugin is in.

**Non-Goals:**
- No wire-protocol, IPC, or relay-server changes - this is client-side UI and reliability behavior only (see proposal.md - Impact).
- Not attempting outfit item-picker or lock-key-generation UX (deferred per proposal.md's noted assumption).
- Not changing the permission-toggle model itself (collar/pairing's "Scoped, revocable permissions" requirement is unchanged) - only where in the UI the Gesture/Follow permission checkboxes' *prerequisite* (ToS ack) lives.

## Decisions

### Connection status as a polled enum, not an event-driven UI model
Add `ConnectionState { Disconnected, Connecting, Connected, Reconnecting }` as a public property on `RelayClient`, updated internally as connect/reconnect/disconnect transitions happen. The UI reads it directly each `Draw()` call rather than subscribing to a status-changed event. Rationale: ImGui immediate-mode rendering already redraws every frame regardless, so a polled property is simpler and has no risk of a UI-side subscription leak; an event would add lifecycle complexity (subscribe/unsubscribe per window) for no benefit here.

### Auto-reconnect lives in `RelayClient`, gated by an explicit intentional-disconnect flag
`RelayClient` gets a background reconnect loop (exponential backoff: 1s, 2s, 4s, 8s, capped at 30s) that only starts when `ConnectionLost` fires from an *unexpected* drop. `Disconnect()` (called by `PairingCommand.EndPairingLocally()` during panic/unpair) sets an `intentionalDisconnect` flag first, so panic and deliberate unpairing never trigger a reconnect attempt - a reconnect loop fighting an intentional disconnect would be a real safety regression for the panic path. Alternative considered: put reconnect logic in `Plugin.cs` instead - rejected, because `RelayClient` already owns the connection lifecycle and the last-used `Uri`; duplicating that context in `Plugin.cs` would be redundant.

### Notification severity mapping
`INotificationManager` toasts, mapped by cause: rejected/failed ack -> Warning, delivery failure -> Warning, connection lost -> Error, reconnected -> Success (brief). Plugin.cs subscribes to `RelayClient`'s three existing events once, in the same place `PairingCommand`/command handlers are already wired up.

### Connection-status color distinct from the panic-red
The panic button's saturated red is deliberately the plugin's single "highest consequence" visual signal. A "disconnected" status badge uses a muted amber/gray instead of the same red, so a dropped relay connection never visually competes with the panic button for attention.

### Settings window scope: setup state, not live session state
Gesture folder allowlist and the ToS acknowledgement move into `SettingsWindow` alongside Role/RelayUrl - all four are "configure once, independent of an active pairing." The Title/Outfit/Gesture/Follow permission toggles themselves **stay in `SubWindow`**, not Settings: they are live, moment-to-moment consent controls a Sub may want to flip mid-session (e.g., disabling "follow" quickly), and moving them behind the settings gear would add friction to exactly the control that most needs to stay fast to reach. `SubWindow`'s Gesture/Follow checkboxes keep reading `Configuration.TosAcknowledged` directly (same field, now set from a different window - no coupling change), and gain a one-line hint pointing at Settings when the ack is still false, so the dependency stays discoverable without requiring the user to already know where it lives.

### DomWindow: tabs replace stacked accordions
`ImGui.BeginTabBar`/`BeginTabItem` replaces the four `CollapsingHeader` calls; each existing `DrawXSection` method's body is reused unchanged inside its tab. Pairing status and the new connection-status indicator stay above the tab bar, matching the current pattern where pairing state is always visible regardless of which category is being worked with.

### Gear icon via `Window.TitleBarButtons`
Both `DomWindow` and `SubWindow` register a single title-bar button (`FontAwesomeIcon.Cog`, click -> `plugin.ToggleSettingsUi()`) in their constructors, replacing the current full-width "Settings" text button in each window's `Draw()`.

## Risks / Trade-offs

- **Auto-reconnect could mask an intentional disconnect** if the intentional/unexpected distinction has a bug → mitigated by the explicit `intentionalDisconnect` flag set *before* any code path that calls `Disconnect()` for panic/unpair, checked before the reconnect loop is scheduled.
- **Reconnect loop could spin indefinitely against a relay that's gone for good** (e.g., a deleted Fly.io deployment) → capped exponential backoff keeps retry frequency low, and the visible "Reconnecting" status makes the state legible; the user can still panic/unpair to stop it (that path calls `Disconnect()`, which is intentional and short-circuits the loop).
- **Tabs restructure touches `DomWindow`'s control flow** more than a pure styling change would → mitigated by reusing each `DrawXSection` method's body verbatim inside its new tab, so the diff is mostly container-level, not logic-level.
- **Splitting ToS-ack/allowlist (Settings) from the permission toggles they gate (SubWindow) could read as inconsistent** → mitigated by the inline hint in SubWindow when the ack is missing, and documented here as a deliberate choice (see Decisions) rather than an oversight.

## Migration Plan

No persisted `PluginConfig` schema changes - `TosAcknowledged` and `GestureFolderAllowlist` already exist as config fields today; this change only relocates which window edits them. No data migration, no version bump needed beyond the plugin's own `<Version>`. Rollback is a plain revert of the UI/RelayClient changes; nothing to undo in saved state.
