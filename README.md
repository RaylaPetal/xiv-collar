# Collar System

A consent-based Owner/Sub control plugin for FINAL FANTASY XIV, built on Dalamud. An Owner sends outfit,
title, gesture, and follow/leash commands to a paired Sub; the Sub's own client applies them locally via
Glamourer, Honorific, and Penumbra, and whatever Mare-successor sync tool (Snowcloak, Lightless, etc.) the
Sub already has paired propagates the result to everyone else. See `ffxiv-collar-system-design.md` at the
repo root for the full feasibility research this plugin is built from.

**There is no way to force something onto another player's screen from outside their own client.** Every
feature here only ever works because the Sub's own plugin is installed, paired, and running on the Sub's
own machine. That is a real, load-bearing constraint, not a limitation to work around - see the Consent
model below.

## Consent model

- **Explicit pairing handshake.** A Sub generates a one-time pairing code and shares it with an Owner out
  of band (voice, chat, whatever channel you already trust). Nothing an Owner sends is ever applied until
  the Sub's own client explicitly accepts that specific pairing request. Pairing is never auto-accepted.
- **Scoped, revocable permissions.** A Sub independently enables or disables each command category
  (title, outfit, gesture, follow) at any time. A command in a disabled category is rejected, even while
  the pairing itself stays active.
- **Panic button, always available.** `/collarpanic` (and an optional configurable hotkey) immediately
  unpairs, reverts any Glamourer state, clears any Honorific title, and releases any active movement lock
  - all from local state only, so it works even if the relay is unreachable.
- **Uninstalling the plugin is always the ultimate safeword.** Since nothing can be applied to a Sub's
  character without the Sub's own plugin running, uninstalling (or simply disabling) it ends all collar
  control immediately. This is the honest FFXIV equivalent of SL OpenCollar's "detach," and there is no
  way around it existing - don't rely on any in-plugin control as a substitute for it.

## Automation risk / ToS disclosure

Two features here go beyond purely cosmetic IPC calls, and carry real risk:

- **Gesture** fires an emote by injecting a chat command (`ECommons.Automation.Chat.SendMessage`).
  Automating chat/input on your own behalf this way is called out by other plugin authors (e.g.
  EmoteReactor) as against FFXIV's ToS. To reduce that risk, a gesture prompt from an Owner is **never**
  auto-fired - it is only ever queued on the Sub's client, and the Sub must explicitly confirm it before
  anything happens.
- **Follow/leash** hooks the game's own movement-input functions to block WASD input and suppress
  auto-unfollow while engaged. This is a heavier automation footprint than cosmetic rendering changes, and
  the hook signatures are version-specific reverse-engineering artifacts that can break on any game patch
  (see `MovementLockService.cs`) - if they fail to resolve on load, the movement lock stays disabled
  rather than silently doing nothing while claiming to work.

Both are gated behind their own permission toggle, and both require the Sub to check an in-UI
acknowledgement of this section before either toggle can be enabled at all. Make an informed choice before
turning them on.

## Project layout

```
CollarSystem.Plugin/     the Dalamud plugin (Owner and Sub share one codebase, switched by role)
  Ipc/                   thin wrappers around Glamourer.Api, Penumbra.Api, and Honorific's IPC calls
  Commands/              one file per command category, plus pairing and the command dispatcher
  Relay/                 the websocket client and wire protocol
  Config/                persisted plugin configuration
  UI/                    DomWindow (Owner) and SubWindow (Sub)
  Safety/                panic handler and in-memory "what's currently applied" state
CollarSystem.Relay/      minimal self-hosted websocket relay (Owner <-> Sub command channel)
```

The relay only ever forwards opaque command/ack frames between the two sockets in a pairing session - it
never inspects payload contents, and it is not a dependency of any single sync fork.

## Prerequisites

* XIVLauncher, FINAL FANTASY XIV, and Dalamud installed, with the game run at least once with Dalamud.
* A .NET 10 SDK.
  * If a custom path is required for Dalamud's dev directory, set the `DALAMUD_HOME` environment variable
    (e.g. `~/.xlcore/dalamud/Hooks/dev` on XIVLauncher-on-Linux/XLCore installs).
* Penumbra, Glamourer, and Honorific installed and enabled in-game (required for the Sub role; the Owner
  role only needs the relay reachable).
* A Mare-successor sync tool (Snowcloak, Lightless, or similar) paired on the Sub's account, if you want
  changes visible to anyone other than the Sub - this plugin only ever writes local state.

## Building

```
dotnet build CollarSystem.slnx
```

This builds both `CollarSystem.Plugin` (the Dalamud plugin, `CollarSystem.Plugin/bin/x64/Debug/CollarSystem.Plugin.dll`)
and `CollarSystem.Relay` (a standalone ASP.NET Core app).

The plugin always builds to `bin/x64/Debug/` regardless of how you invoke it - the csproj forces
`Platform=x64`, so a bare `dotnet build CollarSystem.Plugin/CollarSystem.Plugin.csproj`, an IDE's default
build task, or building via the `.slnx` all land in the same place.

## Running the relay

```
dotnet run --project CollarSystem.Relay
```

Point the Relay URL at wherever you host it, e.g. `ws://your-host:5099/collar` (see "Activating in-game"
below for where to set it).

## Activating in-game

1. `/xlsettings` (chat) or `xlsettings` (console) -> `Experimental` -> add the full path to
   `CollarSystem.Plugin.dll` to Dev Plugin Locations.
2. `/xlplugins` (chat) or `xlplugins` (console) -> `Dev Tools > Installed Dev Plugins` -> enable
   `Collar System`.
3. Open **Settings** - the gear icon in either window's title bar, or `/collarsettings` - and set your
   **Role** (Owner/Sub) and **Relay URL**. If you're the Sub, this is also where you configure the gesture
   mod folder allowlist and check the ToS acknowledgement (required before the Gesture/Follow permission
   toggles can be enabled) - all independent of whether a pairing exists yet.
4. `/collar` opens the Owner or Sub window depending on the configured role; `/collarpanic` always works.
   A connection-status indicator (connected / reconnecting / disconnected) is always visible at the top of
   both windows, and the plugin auto-reconnects to the relay if the connection drops.

All participation in this repository is governed by the [Dalamud Code of Conduct](https://dalamud.dev/code-of-conduct).
If you used AI tooling at any point, review the [AI Usage Policy](https://dalamud.dev/plugin-publishing/ai-policy)
and disclose your level of AI use.
