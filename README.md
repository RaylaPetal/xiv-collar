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

Alongside that, the same composer box also accepts a **direct override** for `title`, `outfit`, `gesture`,
`collar`, `moodle`, and `restraint` - six words reserved so a Sub alias can never be named one of them, no
separate menu needed. A tell like `command title create Good Girl` or `command outfit lock Casual Blue`
bypasses the Sub's own alias dictionary entirely and applies immediately (matching an outfit/gesture/
Moodle/restraint device by whatever name the Sub told the Owner - Settings' unified **Scan & Export**
section scans every catalog at once and exports one file the Sub can hand to their Owner, who fills every
category's Quick Commands from it in one action via the Owner tab's "Import commands" button, instead of
reciting names one by one). Title and outfit also **lock** when force-applied - the Sub's own alias-triggered clear/unlock is refused until the matching
`title clear` / `outfit unlock` override tell (or the Sub's own panic, which always works regardless)
releases it. An outfit lock only ever covers the equipment slots the applied design itself changes - never
Glamourer's own whole-character lock, and never any slot the design doesn't touch, so the rest of your
Glamourer state (any other gear slot, or anything else locked independently, e.g. your collar) stays exactly
as free to edit as if nothing were locked at all. Unlocking an outfit - by the Sub's own alias, the Owner's
`outfit unlock` override, or the Wardrobe tab's Test Unlock - reverts those slots to Glamourer's
automation-managed appearance rather than just releasing the lock, so the manually-applied design never
lingers after unlock. Gesture has nothing to
lock: when its permission is enabled, a gesture command temporarily enables the selected Penumbra animation
option, redraws, waits briefly for the redraw to visually settle, and then plays its tied trigger. `collar` only ever
has one override, `collar unlock` - the collar itself never applies through a command, only automatically
at pairing acceptance (see Consent model below). `moodle apply <preset name>` / `moodle clear` apply or
remove a status effect from the Sub's own saved Moodles presets, immediately, with no confirmation queue -
a Moodle is a visual status icon, not a real emote/animation the way Gesture is.

The Owner's window builds these into one-click **Quick Commands** per category (Title/Outfit/Gesture/
Follow/Moodles/Restraints, plus a general Alias/one-off box with its own "Add Command", and fixed
"Collar unlock"/"Restraint unlock" rows). Outfit/Gesture/Moodles/Restraints are populated together by the
centered **"Import commands"** button at the top of the Owner tab, which reads a file the Sub exported from
Settings' unified Scan & Export section and fills all four in one action - see Automation risk below for
what the Send button on each Quick Command actually does.

## Consent model

- **Two-way code handshake, then a locked identity.** Both sides generate their own code, share it out of
  band, and enter the other's - a message with the wrong (or missing) code is silently ignored, so a
  coincidental "collarpair ..." tell from someone who doesn't actually know your code never produces a
  pairing prompt. A matching code only ever produces a *Pending* request naming the verified sender;
  accepting it is the one and only consent action, and it is never auto-enabled. Once accepted, pairing is
  **locked** - there is no checkbox to uncheck. The only way to unpair is `/collarpanic` below.
- **Scoped, revocable permissions.** A Sub independently enables or disables each command category
  (title, outfit, gesture, follow, collar, moodles) at any time. A command in a disabled category is
  silently ignored, even while paired.
- **The collar is the one thing that applies itself.** Everything else in this plugin only ever happens
  because a command arrived; the collar is different on purpose - if a Sub has captured a Neck-slot item
  (Collar tab) and enabled the "Collar" permission, accepting a pairing request applies and locks that item
  automatically, as the persistent marker that a contract is active, not just another swappable alias. It
  locks only the Neck slot (the Sub's own casual removal of that one slot is refused) - every other slot
  stays completely free to edit throughout, including while an outfit is separately locked at the same
  time - and `/collarpanic` always releases it, no exception - the Owner also has a `collar unlock` override
  for releasing it without the Sub needing to panic.
- **Panic is a typed safeword, not a button.** The main character header always exposes the safeword
  setting, whether paired or not, but there's no panic button anywhere in the UI on purpose -
  `/collarpanic` (and an optional configurable hotkey) immediately disables pairing, reverts any Glamourer
  state, clears any Honorific title, and releases any active movement lock, all from local state only. Set
  a safeword in the header and `/collarpanic` requires it as an argument (`/collarpanic red`);
  leave it blank and plain `/collarpanic` keeps working unconditionally - a forgotten safeword must never
  be the reason panic stops working. Safewords are masked by default and can be deliberately revealed.
  Outfit and Collar locks are never held through Glamourer's own lock at all - each is this plugin's own
  per-slot tracking, persisted to your plugin configuration and re-asserted automatically if anything
  changes a locked slot. A plugin reload or game restart between locking and unlocking can't strand you
  locked with no way to recover - panic (and the ordinary Sub/Owner unlock paths) can always release it
  afterward, with nothing to lose track of.
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
- **Gesture** temporarily applies the selected animation's complete Penumbra option state, redraws the
  Sub, briefly waits for the redraw to visually settle, and then fires its tied emote or supported
  sit/ground-sit/doze pose after a valid trigger tell. The Sub's automation-risk acknowledgement and live
  Gesture permission are the consent gates; disabling Gesture rejects later commands without changing
  Penumbra or animation state. The temporary Penumbra activation doesn't persist indefinitely: the Gesture
  tab has a manual **Reset active gesture** button that reverts it on demand, and it's also reverted
  automatically after roughly 30 seconds with no further gesture play - a new gesture play before then
  restarts the timeout instead of stacking, and playing a different mod's gesture first reverts whatever
  was previously active.
- **Follow/leash** hooks the game's own movement-input functions to block WASD input and suppress
  auto-unfollow while engaged. This is a heavier automation footprint than cosmetic rendering changes, and
  the hook signatures are version-specific reverse-engineering artifacts that can break on any game patch
  (see `MovementLockService.cs`) - if they fail to resolve on load, the movement lock stays disabled
  rather than silently doing nothing while claiming to work.
- **Restraints** ties a Glamourer design to one or more restriction rules (Restraints tab): forced pose
  (blocks movement, same mechanism as Follow/leash), walk-only (forces walking, blocks running, leaves
  directional input untouched), action block (hooks `ActionManager`'s own action-use entry point to
  suppress hotbar/skill execution), and gag chat mangling. **Gag chat mangling is a materially different
  automation surface from everything else in this plugin**: it intercepts your own outgoing chat message
  after you press Enter but before it reaches the server, and replaces the actually-transmitted text with a
  muffled/nonsense variant - not just your own local display of it. Every other feature here either applies
  a cosmetic/state change to your own character or blocks an input; this one rewrites content you yourself
  typed. It only ever runs while a gag-rule device is applied (an explicit, reversible opt-in you or your
  Owner toggle the same way as any other device), never unconditionally, and it never touches slash
  commands. See `ChatGagService.cs`.

Gesture, Follow, and Restraints are gated behind their own permission toggle, and all three require the Sub
to check an in-UI acknowledgement of this section (Settings) before any of the three toggles can be enabled
at all. Make an informed choice before turning them on.

## Testing locally, before pairing

Every configurable Sub action has its own action-specific **Test** button (e.g. "Test Lock", "Test Unlock",
"Test Apply", "Test Clear", "Test Play", "Test Engage", "Test Release" - the label alone identifies what it
does, no tooltip needed), right next to where it's configured: title apply/clear (Title tab), outfit
apply/unlock (Wardrobe tab), gesture playback (Gesture tab), collar lock/unlock and leash/unleash (Collar
tab), and Moodles apply/clear (Settings' Scan & Export section). A Test button runs the action through the
exact same local code path an accepted Owner's command would use - `LocalTestCoordinator` calls straight
into the same `TitleCommand`/`OutfitCommand`/`GestureCommand`/`FollowCommand`/`CollarCommand`/
`MoodlesCommand` methods `ChatCommandListener` calls for a real trigger tell - so a passing test is a real
guarantee the configuration works, not a simulation.

**Testing never touches pairing or chat.** No pairing (active or pending) is required to test, and no test
ever composes or sends a `/tell` - `ChatComposer`/`ChatSender` are never involved. Testing only changes your
own local game state (title, outfit, collar, animation, or movement lock), exactly like accepting the
matching command would.

**The normal gates still apply.** A Test button still requires that action's category permission
(Permissions tab) to be enabled, and Gesture/Leash tests additionally require the automation-risk
acknowledgement (Settings) - a disabled permission or missing acknowledgement makes the test a no-op and
shows why, right next to the button, instead of silently doing nothing. Every test shows a success or
failure result next to its button naming the action that was attempted, which clears itself automatically a
few seconds later instead of persisting until the next test overwrites it - so a failed Glamourer/Penumbra/
Moodles integration is easy to tell apart from a gating failure, without stale results cluttering the UI.

**Hiding Test controls.** Settings has a "Hide local Test controls" checkbox (off by default) that removes
every Test button from the Sub-facing interface without disabling local testing itself or affecting any
other control - a one-click toggle for a cleaner UI once you've verified everything works.

## Project layout

```
CollarSystem.Plugin/     the Dalamud plugin (Owner and Sub share one codebase and one window)
  Ipc/                   thin wrappers around Glamourer.Api, Penumbra.Api, Honorific's IPC, and Moodles' IPC
  Commands/              one file per command category, the chat listener, and the trigger composer/sender
  Config/                persisted plugin configuration, including the Sub's alias definitions
  UI/                    CollarWindow (Title/Wardrobe/Gesture/Collar/Owner/Permissions tabs), SettingsWindow
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
* Moodles installed and enabled in-game if you want the Moodles category - optional, the rest of the
  plugin works without it.
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
   * Check the ToS acknowledgement, then use Settings' unified **Scan & Export** section: optionally select
     Penumbra animation mods or wardrobe folders to restrict scanning (empty animation selection and empty
     wardrobe folder scope both mean **scan everything available**; folder/text search fields only filter
     the visible picker), then hit **Scan all** to rescan Wardrobe, Gesture, and Moodles together. Define
     aliases in the main window's Title/Wardrobe/Gesture/Restraints tabs, which stay available regardless
     of Role. Once you've scanned (and tagged any Restraints devices you want), hit **Export...** to save a
     single file covering every category - hand that file to your Owner however you like (Discord, a
     shared folder), and they fill every Quick Command list from it in one action via the Owner tab's
     **Import commands** button.
   * If you want a collar: equip the item you want in your Neck slot, then capture it from the main
     window's **Collar** tab. Enable the **Collar** permission (Permissions tab) - configuring an item
     alone does nothing without it. The collar applies and locks automatically the next time you accept a
     pairing, not before.
   * Once both sides have entered each other's code, either side copies the pairing message from Settings
     and sends it as a `/tell` to the other. The receiving side gets a Pending request naming the verified
     sender and their declared role - click **Accept**. Pairing is then locked for a Sub; an Owner can
     Release it any time (both in the character header, or in Settings).
   * Optionally set a **Safeword** in the always-visible main character header - if set,
     `/collarpanic` requires it as an argument; if left blank, plain `/collarpanic` keeps working.
4. `/collar` opens the one main window; `/collarpanic` (with your safeword as its argument, if you set
   one) always works from anywhere. The header shows your live character name, home world, optional Free
   Company tag, and an explicit Not paired/Owns/Owned by/pending relationship state.
   Title/Wardrobe/Gesture/Permissions are where a Sub sets up what they'll accept. The **Collar** tab also
   owns the Sub's leash trigger words, defaulting to `leash` and `unleash`. The visually separated,
   far-right **Owner** tab groups each command category into an independent collapsible section where you
   build one-click Quick Commands or compose
   a one-off - each has a Send button (fires immediately) and a Copy button (paste it yourself instead).
   Gesture entries show the mod's human-readable animation option and tied trigger; permitted commands
   temporarily activate that option and play it immediately.
   Each configured action also has its own **Test** button so you can verify it works locally before you've
   even paired - see Testing locally, before pairing below.

All participation in this repository is governed by the [Dalamud Code of Conduct](https://dalamud.dev/code-of-conduct).
If you used AI tooling at any point, review the [AI Usage Policy](https://dalamud.dev/plugin-publishing/ai-policy)
and disclose your level of AI use.
