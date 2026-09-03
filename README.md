# Collar System

A consent-based Owner/Sub control plugin for FINAL FANTASY XIV, built on Dalamud. An Owner sends outfit,
title, gesture, and follow/leash commands to a paired Sub as ordinary in-game tells - no server, no
hosting, no cost. The Sub's own client watches for those tells and applies them locally via Glamourer,
Honorific, and Penumbra; whatever Mare-successor sync tool (Snowcloak, Lightless, etc.) the Sub already has
paired propagates the result to everyone else. See `ffxiv-collar-system-design.md` at the repo root for the
original feasibility research this plugin is built from.

**There is no way to force something onto another player's screen from outside their own client.** Every
feature here only ever works because the Sub's own plugin is installed, paired, and running on the Sub's
own machine. That is a real, load-bearing constraint, not a limitation to work around - see the Consent
model below.

## How commands travel

There is no relay, no websocket, no account to create. Pairing itself is a one-time, two-way **code
handshake** sent as ordinary tells; every command after that is an ordinary tell too. Composing a message
never sends it by itself - sending is always a separate, explicit step (either your own paste, or the
one-click Send button), see Automation risk below.

```
Pairing (once, either order):
  Each side generates its own short code in Settings, shares it out of band
  (voice, DM, etc), and enters the other's code as "Their code."
  Each side then sends a tell:  /tell TheirName@World  collarpair owner <my code>
                                                  (or "collarpair sub <my code>")
                                                |
                                                v
                [ FFXIV's own server delivers it - no infrastructure of ours involved ]
                                                |
                                                v
  Receiving plugin: does the embedded code match "Their code" I configured?
     -> yes: show a Pending request naming the verified sender (from FFXIV's own
        sender field, unforgeable) and what role they say they'll be - explicit
        Accept required, never auto-paired. Both sides set to the same role gets
        flagged, since nothing would ever trigger.
     -> Accept captures that name+world as the trusted peer and locks pairing on.

Ongoing commands (after pairing):
  Owner types (or pastes a plugin-composed):
    /tell SubName@World  command strip
                      |
                      v
  Sub's plugin: sender matches the captured, trusted peer?
     -> "command" trigger phrase matched?
     -> "strip" found in the Sub's own locally-defined aliases?
     -> apply it locally (Glamourer / Penumbra / Honorific)
```

Codes only ever gate the one-time handshake; once accepted, ongoing commands are matched purely by the
server-verified sender identity that handshake captured - the same unforgeable check as before, just
established by a manual two-way exchange instead of typing an exact name/world into Settings.

### Two ways to command: alias, or direct override

Every command still reduces to a short **alias** the Sub defines ahead of time (Title/Wardrobe/Gesture tabs
in the one shared window - see UI below) - `strip` -> a specific Glamourer design, locked; `bow` -> a
specific gesture mod's emote. Only the alias *name* crosses chat; what it actually does never leaves the
Sub's own client. The Owner learns alias names the same way they'd learn anything else about a scene: the
Sub tells them, out of band.

Alongside that, the same composer box also accepts a **direct override** for `title`, `outfit`, and
`gesture` - three words reserved so a Sub alias can never be named one of them, no separate menu needed. A
tell like `command title create Good Girl` or `command outfit lock Casual Blue` bypasses the Sub's own
alias dictionary entirely and applies immediately (matching an outfit/gesture by whatever name the Sub
told the Owner from their own scan results - Settings has a "Copy names" button next to each scan so the
Sub can hand over the exact list instead of reciting it). Title and outfit also **lock** when
force-applied - the Sub's own alias-triggered clear/unlock is refused until the matching `title clear` /
`outfit unlock` override tell (or the Sub's own panic, which always works regardless) releases it. Gesture
has nothing to lock - a
force-queued gesture still only ever queues, and the Sub still has to confirm it themselves before it
plays, exactly like an alias-queued one.

The Owner's window builds these into one-click **Quick Commands** per category (Title/Outfit/Gesture/
Follow, plus a general Alias/one-off box with its own "Add Command"), auto-populated for Outfit/Gesture
straight from the Sub's clipboard-exported name list - see Automation risk below for what the Send button
on each one actually does.

## Consent model

- **Two-way code handshake, then a locked identity.** Both sides generate their own code, share it out of
  band, and enter the other's - a message with the wrong (or missing) code is silently ignored, so a
  coincidental "collarpair ..." tell from someone who doesn't actually know your code never produces a
  pairing prompt. A matching code only ever produces a *Pending* request naming the verified sender;
  accepting it is the one and only consent action, and it is never auto-enabled. Once accepted, pairing is
  **locked** - there is no checkbox to uncheck. The only way to unpair is `/collarpanic` below.
- **Scoped, revocable permissions.** A Sub independently enables or disables each command category
  (title, outfit, gesture, follow) at any time. A command in a disabled category is silently ignored, even
  while paired.
- **Panic is a typed safeword, not a button.** There's no panic button anywhere in the UI on purpose -
  `/collarpanic` (and an optional configurable hotkey) immediately disables pairing, reverts any Glamourer
  state, clears any Honorific title, and releases any active movement lock, all from local state only. Set
  a safeword in Settings and `/collarpanic` requires it as an argument (`/collarpanic red`); leave it blank
  and plain `/collarpanic` keeps working unconditionally, same as before - a forgotten safeword must never
  be the reason panic stops working.
- **Uninstalling the plugin is always the ultimate safeword.** Since nothing can be applied to a Sub's
  character without the Sub's own plugin running, uninstalling (or simply disabling) it ends all collar
  control immediately. This is the honest FFXIV equivalent of SL OpenCollar's "detach," and there is no
  way around it existing - don't rely on any in-plugin control as a substitute for it.

## Automation risk / ToS disclosure

- **Every send is one explicit, individual human click - never a reaction to observed state.** The Owner's
  window can compose a command and copy it to your clipboard (paste and send it yourself), or send it
  directly with a Send button - one click, one `/tell`, to the one partner you've mutually paired with.
  That's the same shape as pressing an FFXIV hotbar macro that sends a `/tell`: a human decides, in the
  moment, to fire one specific message. What FFXIV's ToS and other plugin authors (e.g. EmoteReactor) call
  out as risky is a materially different pattern - *autonomously reacting* to observed chat/game state and
  sending without a human in the loop per message (auto-replies, bots, retry/resend loops). This plugin
  never does that: `ChatSender` (the one place in the codebase that can transmit anything) is only ever
  called from a direct button click, and it refuses to send anything that isn't an addressed `/tell` - a
  command composed before pairing captures a peer identity has no `/tell` prefix and Send is disabled for
  it, so nothing can ever leak into local/say chat.
- **Gesture** still fires an emote via `ECommons.Automation.Chat.SendMessage` once triggered - but a
  gesture alias is **never** auto-fired even after a valid trigger tell arrives (or a direct Send). It is
  only ever queued on the Sub's client, and the Sub must explicitly confirm it before anything happens -
  that confirm click is what keeps this in the same "human decides, in the moment" category as everything
  else here.
- **Follow/leash** hooks the game's own movement-input functions to block WASD input and suppress
  auto-unfollow while engaged. This is a heavier automation footprint than cosmetic rendering changes, and
  the hook signatures are version-specific reverse-engineering artifacts that can break on any game patch
  (see `MovementLockService.cs`) - if they fail to resolve on load, the movement lock stays disabled
  rather than silently doing nothing while claiming to work.

Gesture and Follow are gated behind their own permission toggle, and both require the Sub to check an
in-UI acknowledgement of this section (Settings) before either toggle can be enabled at all. Make an
informed choice before turning them on.

## Project layout

```
CollarSystem.Plugin/     the Dalamud plugin (Owner and Sub share one codebase and one window)
  Ipc/                   thin wrappers around Glamourer.Api, Penumbra.Api, and Honorific's IPC calls
  Commands/              one file per command category, the chat listener, and the trigger composer
  Config/                persisted plugin configuration, including the Sub's alias definitions
  UI/                    CollarWindow (Title/Wardrobe/Gesture/Owner/Permissions tabs), SettingsWindow
  Safety/                panic handler and in-memory "what's currently applied" state
```

There is nothing to host and no second project - the whole plugin is `CollarSystem.Plugin`.

## Prerequisites

* XIVLauncher, FINAL FANTASY XIV, and Dalamud installed, with the game run at least once with Dalamud.
* A .NET 10 SDK.
  * If a custom path is required for Dalamud's dev directory, set the `DALAMUD_HOME` environment variable
    (e.g. `~/.xlcore/dalamud/Hooks/dev` on XIVLauncher-on-Linux/XLCore installs).
* Penumbra, Glamourer, and Honorific installed and enabled in-game (required for the Sub role; the Owner
  role needs none of these - composing a trigger message doesn't touch any of them).
* A Mare-successor sync tool (Snowcloak, Lightless, or similar) paired on the Sub's account, if you want
  changes visible to anyone other than the Sub - this plugin only ever writes local state.

## Building

```
dotnet build CollarSystem.slnx
```

The plugin always builds to `bin/x64/Debug/` regardless of how you invoke it - the csproj forces
`Platform=x64`, so a bare `dotnet build CollarSystem.Plugin/CollarSystem.Plugin.csproj`, an IDE's default
build task, or building via the `.slnx` all land in the same place.

## Activating in-game

1. `/xlsettings` (chat) or `xlsettings` (console) -> `Experimental` -> add the full path to
   `CollarSystem.Plugin.dll` to Dev Plugin Locations.
2. `/xlplugins` (chat) or `xlplugins` (console) -> `Dev Tools > Installed Dev Plugins` -> enable
   `Collar System`.
3. Open **Settings** - the gear icon in the main window's title bar, or `/collarsettings`:
   * Set your **Role** (Owner/Sub) - it only affects whether incoming tells apply locally and what the
     pairing handshake declares; it doesn't hide anything else.
   * Share your generated code with your pair out of band, and enter theirs as **Their code**.
   * Check the ToS acknowledgement, set your gesture/wardrobe folder allowlists, and define your aliases
     (each one maps a short name to a title/outfit/gesture action) - the main window's Title/Wardrobe/
     Gesture tabs handle that, and stay available regardless of Role.
   * Once both sides have entered each other's code, either side copies the pairing message from Settings
     and sends it as a `/tell` to the other. The receiving side gets a Pending request naming the verified
     sender and their declared role - click **Accept**. Pairing is then locked for a Sub; an Owner can
     Release it any time (both in the pairing card, or in Settings).
   * Optionally set a **Safeword** - if set, `/collarpanic` requires it as an argument; if left blank,
     plain `/collarpanic` keeps working.
4. `/collar` opens the one main window; `/collarpanic` (with your safeword as its argument, if you set
   one) always works from anywhere. Title/Wardrobe/Gesture/Permissions are where a Sub sets up what
   they'll accept; the **Owner** tab is where you build one-click Quick Commands per category, or compose
   a one-off - each has a Send button (fires immediately) and a Copy button (paste it yourself instead).
   The Gesture tab also shows pending gesture confirmations that need
   your explicit click before anything plays.

All participation in this repository is governed by the [Dalamud Code of Conduct](https://dalamud.dev/code-of-conduct).
If you used AI tooling at any point, review the [AI Usage Policy](https://dalamud.dev/plugin-publishing/ai-policy)
and disclose your level of AI use.
