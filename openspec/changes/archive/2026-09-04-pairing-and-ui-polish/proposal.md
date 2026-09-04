## Why

Six independent rough edges accumulated across pairing, Settings, and a couple of UI modules:

1. **Pairing takes four actions across two people** (each side sends their own handshake, each side accepts the other's) when the actual consent needed is one deliberate action per side.
2. **Settings' Identity & Pairing card scrolls internally**, hiding the pairing status/Accept-Reject controls unless the window is stretched unusually large - the exact "can't reach the bottom" bug this project already fixed once for Scan & Export (see `SettingsWindow.cs`'s comment on `DrawScanAndExportCard`), recurring in a different card.
3. **The automation-risk acknowledgement sits at the very bottom of Settings**, effectively hidden, despite gating three permission categories (Gesture, Follow, Restraints).
4. **Restraint rule checkboxes stack one per line**, taking more vertical space than the seven-rule list needs.
5. **Every configurable Sub action has its own local Test button**, scattered across four tabs plus Settings - `fix-owner-command-delivery` already added one consolidated "Test an Owner command" control that exercises the exact same dispatch path with more realism (trigger phrase, permission gates, and all), making the per-action buttons redundant clutter.
6. **The Collar tab's Neck-slot capture still requires physically equipping the item first**, the same equip-first pattern `restraint-gear-picker` just replaced for Restraints with a searchable item-by-slot picker.

## What Changes

- **One-way pairing handshake.** Either side sends one invite tell (still gated by the existing shared-code check - both sides still generate and exchange codes exactly as today). The receiving side's Pending prompt and explicit Accept button work exactly as now. Accepting **additionally** sends one confirmation tell back to the inviter automatically - a direct, singular consequence of that one Accept click, not a background reaction - carrying the accepting side's own role, trigger phrase, and the code that matched. The inviting side, on receiving a confirmation tell whose code matches its own currently-configured code, completes pairing automatically with no further click: the inviter's own deliberate act of sending the invite is their consent action, exactly as clicking Accept is the receiver's. Net result: one send + one click completes pairing for both sides, instead of two sends + two accepts.
  - **This is the one narrow, explicit exception to this plugin's "no automated sending" rule** (see `collar/chat-transport`'s "No automated sending" requirement) - everywhere else in the plugin, sending remains only ever a direct button click on visible text. The exception is scoped to exactly one tell, fired only as the direct result of the accepting user's own explicit Accept click.
- **Sub's pairing identity locks while paired.** While a Sub is paired, Role, "their code," "regenerate my code," and the trigger phrase all become read-only in Settings - consistent with how the Pairing.Paired flag itself already only unlocks for a Sub via `/collarpanic`, never a Settings toggle. The Owner side is unaffected (it can already `ReleasePeer()` locally at any time).
- **Settings' Identity & Pairing card, the Automation risk acknowledgement card, and the new Test-an-Owner-command card all stop being fixed-height scrolling children** - the same fix already applied to Scan & Export, generalized to the rest of Settings' top section.
- **Automation risk acknowledgement moves up**, rendered immediately after Identity & Pairing instead of after Scan & Export, so it's visible without scrolling in the common case.
- **Restraint rule checkboxes render two per row** instead of one, in both the Sub's device-capture editor and every Owner rule editor (per-quick-command and the new ad-hoc "define device" editor).
- **Removes every per-action local Test button** (title/outfit/gesture/collar/moodle/follow apply-clear-lock-unlock-play-etc., one per tab) and the "hide local Test controls" setting that existed only to manage their clutter. The one remaining Test surface is Settings' existing "Test an Owner command" free-text control, which already exercises the same underlying action through the real dispatch path with more coverage (trigger phrase and permission gates included, not bypassed).
- **Collar tab gains the same item-by-slot picker `restraint-gear-picker` built**, locked to the Neck slot only, replacing the equip-first "capture what's equipped" flow for configuring the collar item.

## Capabilities

### Modified Capabilities
- `collar/pairing`: one-way handshake completion; Sub-side identity/role/trigger-phrase lock while paired.
- `collar/chat-transport`: one narrow, explicit exception to "No automated sending" for the pairing-acceptance confirmation tell.
- `collar/ui-organization`: removes per-action local Test controls and the control to hide them (superseded by the existing single "Test an Owner command" control); adds non-scrolling layout and reordering requirements for Settings' top cards; adds two-per-row layout for restraint rule checkboxes.
- `collar/collaring`: collar-item configuration moves from equip-first capture to a Neck-locked item picker.

## Impact

- `CollarSystem.Plugin/Commands/PairingCommand.cs` - `AcceptPeer` gains the automatic confirmation-send step; a new method validates and applies an incoming confirmation tell.
- `CollarSystem.Plugin/Commands/ChatCommandListener.cs` - `TryHandlePairingMessage` recognizes a new confirmation-tell shape alongside the existing invite shape; `AcceptPending` composes and sends the confirmation tell via `ChatSender`.
- `CollarSystem.Plugin/Commands/ChatComposer.cs` - gains composing for the confirmation tell.
- `CollarSystem.Plugin/UI/SettingsWindow.cs` - Identity & Pairing card locks its fields for a paired Sub; card ordering changes (ToS moves up); Identity/ToS/Test-command cards stop using a fixed-height `Card.Begin`, following `DrawScanAndExportCard`'s existing pattern.
- `CollarSystem.Plugin/UI/CollarWindow.cs` - every per-action "Test ..." button removed; restraint rule checkbox blocks laid out two per row; Collar tab's capture control replaced with an `ItemPickerWindow.Open(ApiEquipSlot.Neck, ...)` call.
- `CollarSystem.Plugin/Commands/LocalTestCoordinator.cs` and the "hide local Test controls" config flag - removed along with their now-unused call sites.
- `CollarSystem.Plugin/Commands/CollarCommand.cs` - gains a picker-based configure method alongside (or replacing) the equip-first one, mirroring `RestraintCommand.CaptureDeviceFromItem`.
- **BREAKING**: the per-action local Test buttons and the setting to hide them are gone entirely, not just hidden by default - Settings' "Test an Owner command" control is the only local test surface going forward. The equip-first Collar capture flow is replaced by the picker, the same breaking-change shape `restraint-gear-picker` already used for Restraints.
