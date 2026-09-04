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
  Each side then sends a tell:  /tell TheirName@World  collarpair owner <my code> <my trigger phrase>
                                                  (or "collarpair sub <my code> <my trigger phrase>")
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
     -> Accept captures that name+world as the trusted peer, along with their
        declared trigger phrase, and locks pairing on.

Ongoing commands (after pairing):
  Owner types (or pastes a plugin-composed, using the Sub's captured trigger phrase):
    /tell SubName@World  ray strip
                      |
                      v
  Sub's plugin: sender matches the captured, trusted peer?
     -> "ray" (the Sub's own configured trigger phrase) matched?
     -> "strip" found in the Sub's own locally-defined aliases?
     -> apply it locally (Glamourer / Penumbra / Honorific)
```

Codes only ever gate the one-time handshake; once accepted, ongoing commands are matched purely by the
server-verified sender identity that handshake captured - the same unforgeable check as before, just
established by a manual two-way exchange instead of typing an exact name/world into Settings.

> **Trigger phrase auto-sync**: each side's trigger phrase (Settings) used to be a purely local setting with
> no way for the two sides to confirm they matched - if the Owner never separately set theirs to match the
> Sub's, every command silently failed to be recognized, with no visible error. The handshake above now
> carries each side's own trigger phrase, and composing to a paired peer uses *their* captured phrase
> automatically - Settings shows which phrase is actually in effect. This only takes effect when **both**
> sides are on this version or later; a pairing formed with an older peer falls back to the previous
> manual-matching behavior (Settings will show "your own - peer hasn't sent theirs" in that case).

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
section scans Wardrobe, Gesture, and Moodles at once (Restraints devices are captured individually in the
Restraints tab, by picking a slot and an item, not scanned) and exports one file covering all four categories the Sub can hand to their
Owner, who fills every category's Quick Commands from it in one action via the Owner tab's "Import
commands" button, instead of reciting names one by one). Title and outfit also **lock** when force-applied - the Sub's own alias-triggered clear/unlock is refused until the matching
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
at pairing acceptance (see Consent model below). `moodle apply <status name>` / `moodle clear` apply or
remove a status effect from the Sub's own registered Moodles statuses (individual buffs/debuffs), immediately,
with no confirmation queue - a Moodle is a visual status icon, not a real emote/animation the way Gesture is.

> **Breaking change:** Moodles scanning switched from reading the Sub's saved *presets* to reading their
> individual registered *statuses* (buffs/debuffs) directly. Existing Owner Moodles Quick Commands built
> from preset names will no longer resolve - rescan Moodles on the Sub's side and re-import on the Owner's
> side to rebuild them from status names.

The Owner's window builds these into one-click **Quick Commands** per category (Title/Outfit/Gesture/
Follow/Moodles/Restraints, plus a general Alias/one-off box with its own "Add Command", and fixed
"Collar unlock"/"Restraint unlock" rows). Outfit/Gesture/Moodles/Restraints are populated together by the
centered **"Import commands"** button at the top of the Owner tab, which reads a file the Sub exported from
Settings' unified Scan & Export section and fills all four in one action - a "Reset imports" button next to
it clears those same four import-populated lists back to empty in one action, without touching Title/Leash/
Alias commands built by hand - see Automation risk below for what the Send button on each Quick Command
actually does.

> **Restraints import carries every captured device name** - the Sub captures each device by picking a slot
> and an item from a searchable picker (Restraints tab, no need to own or equip the item first), so every
> imported entry is already a real device name. The Owner can also add a Restraints Quick Command manually
> by typing a name, without importing first (mirroring Title's own "Add Command"). Either way, each
> Restraints Quick Command needs the Owner to configure its own restriction rules (forced pose, walk-only,
> action block, Gagged, Arms Cuffed, Legs Cuffed, Full Body Cuffed - the last three each with a chosen
> animation from the Sub's Gesture catalog) via the "Configure rules" control on that entry, the same rule
> set the Sub's own device-capture UI uses. Those Owner-assigned rules travel with the `restraint lock`
> command and take effect on the Sub's side regardless of whatever rules (if any) the Sub separately
> assigned to that same device - Send stays disabled on an entry until rules are assigned.
>
> **The Owner can also define a device's gear directly**, with no Sub-side name needed at all - a "define a
> device's gear directly" control sits alongside the name-based "Add Command", letting the Owner pick a slot
> and an item from the same picker, give it their own local label, and assign rules, then send it as one
> self-contained `restraint wear` command. This means the Owner can put *any* equippable item on the Sub in
> any lockable slot without the Sub ever having reviewed that specific item first - a deliberately broader
> grant than every other Owner-forced action in this plugin, gated the same way (Restraints permission + the
> automation-risk acknowledgement below) rather than by per-item review.

## Consent model

- **One-way handshake, still code-gated, then a locked identity.** Both sides still generate their own code
  and share it out of band beforehand - a message with the wrong (or missing) code is silently ignored, so
  a coincidental "collarpair ..." tell from someone who doesn't actually know your code never produces a
  pairing prompt. Only one side needs to actually send the handshake: whoever does, the other side gets a
  *Pending* request naming the verified sender, and accepting it is their one and only consent action. That
  accept automatically sends one confirmation tell back to the sender - the one narrow, explicit exception
  to this plugin's "no automated sending" rule (see Automation risk below) - which completes the sender's
  own side with no further action from them; sending the original invite was their consent action. Once
  paired, a Sub's Role, code, and trigger phrase all lock in Settings and pairing itself is **locked** -
  there is no checkbox to uncheck. The only way to change any of it is `/collarpanic` below.
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
- **One narrow, explicit exception: accepting a pairing request sends one confirmation tell automatically.**
  This is the only place in the entire plugin where a chat message is sent without you personally clicking
  a Send button in that exact moment. It exists so pairing completes for both sides from one invite instead
  of requiring both people to separately send a handshake - see Consent model above. It fires at most once
  per Accept click, is always addressed as a `/tell` back to the exact character whose invite you just
  accepted, and carries nothing but your role, trigger phrase, and the code that was already matched to
  show you that Pending request in the first place. It is still a direct, singular consequence of your own
  explicit Accept click - not a background reaction to observed chat or game state - so it doesn't fit the
  *autonomously reacting* pattern described above, but it is a deliberate, real exception to "every send is
  a click on visible text," and it's called out here on its own rather than folded silently into the
  paragraph above.
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
- **Restraints** ties a single gear piece - one item in one chosen slot (a bracelet, a chest harness, a
  specific pair of cuffs), picked from a searchable item-by-slot picker rather than a whole Glamourer design
  - to one or more restriction rules (Restraints tab): forced pose (blocks movement, same mechanism as Follow/leash),
  walk-only (forces walking, blocks running, leaves directional input untouched), action block (hooks
  `ActionManager`'s own action-use entry point to suppress hotbar/skill execution), Gagged (chat mangling),
  and Arms Cuffed / Legs Cuffed / Full Body Cuffed - each of the last three holds you in a chosen animation
  from your own installed mods (the same picker Gesture uses) for as long as the device is applied, with
  Full Body Cuffed additionally blocking movement like forced pose does. **Gagged is a materially different
  automation surface from everything else in this plugin**: it intercepts your own outgoing chat message
  after you press Enter but before it reaches the server, and replaces the actually-transmitted text with a
  muffled/nonsense variant - not just your own local display of it. Every other feature here either applies
  a cosmetic/state change to your own character or blocks an input; this one rewrites content you yourself
  typed. It only ever runs while a Gagged-rule device is applied (an explicit, reversible opt-in you or your
  Owner toggle the same way as any other device), never unconditionally, and it never touches slash
  commands. See `ChatGagService.cs`.

  > **Breaking change:** restraint device capture switched from "equip the piece, then capture what's
  > equipped" to picking a slot and an item directly from a searchable picker - the item no longer needs to
  > be equipped or owned. Already-captured devices are unaffected; capturing a *new* device always goes
  > through the picker now. The Owner can also now define a device's gear directly (slot + item + rules),
  > without needing the Sub to have captured or named anything first - see the Restraints Quick Command
  > section above.

Gesture, Follow, and Restraints are gated behind their own permission toggle, and all three require the Sub
to check an in-UI acknowledgement of this section (Settings) before any of the three toggles can be enabled
at all. Make an informed choice before turning them on.

## Testing locally, before pairing

Settings' **"Test an Owner command"** card is the one local-test surface: type the exact raw text an Owner
would send (trigger phrase included, e.g. `ray outfit lock kagome`) and it runs through the *real* dispatch
code (`ChatCommandListener.Resolve`, `TestIncomingCommand`) - the same trigger-phrase/permission/
reserved-word parsing a real incoming tell goes through, not a separate reimplementation or a bypass of it -
so a passing result is a genuine guarantee that text would work from a real paired Owner. It requires no
pairing and sends or receives no chat message - the one difference from a real tell is it can't verify
sender identity, since there's no real sender in a local test.

**Testing never touches pairing or chat.** No pairing (active or pending) is required, and it never composes
or sends a `/tell` - `ChatComposer`/`ChatSender` are never involved for this. Testing only changes your own
local game state, exactly like accepting the matching command would.

**The normal gates still apply.** A test still requires the tested category's permission (Permissions tab)
to be enabled, and Gesture/Restraints additionally require the automation-risk acknowledgement (Settings) -
a disabled permission or missing acknowledgement makes the test a no-op and reports why, instead of silently
doing nothing. Every test shows a success or failure result naming what was attempted, which clears itself
automatically a few seconds later.

There used to be a separate action-specific Test button next to every configured alias/setting across every
tab. Those are gone - the single command-text control above exercises the exact same underlying actions with
strictly more coverage (it also catches a bug in the trigger-phrase/permission parsing layer, which the old
per-action buttons bypassed entirely by calling an action's method directly). If you're used to a dedicated
"Test Apply" or "Test Lock" button next to a specific alias, use this card instead: type your trigger phrase
followed by that alias or override command.

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
     of Role. Once you've scanned (and captured any Restraints devices you want, by picking a slot and an
     item from the Restraints tab's picker), hit **Export...** to save a single file covering every category - hand that
     file to your Owner however you like (Discord, a
     shared folder), and they fill every Quick Command list from it in one action via the Owner tab's
     **Import commands** button.
   * If you want a collar: pick a Neck-slot item from the main window's **Collar** tab's item picker (it
     doesn't need to be equipped or owned) and save it. Enable the **Collar** permission (Permissions tab) -
     configuring an item alone does nothing without it. The collar applies and locks automatically the next
     time you accept a pairing, not before.
   * Once both sides have entered each other's code, only one of you needs to copy the pairing message from
     Settings and send it as a `/tell` to the other. The receiving side gets a Pending request naming the
     verified sender and their declared role - click **Accept**. That automatically completes pairing on
     the sender's side too, with no further action from them. Pairing is then locked for a Sub (Role, code,
     and trigger phrase all become read-only in Settings) - the only way to change any of it is
     `/collarpanic`. An Owner's pairing is never locked and can be Released any time (both in the character
     header, or in Settings).
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
   Settings' "Test an Owner command" card lets you verify any configured alias works locally before you've
   even paired - see Testing locally, before pairing below.

All participation in this repository is governed by the [Dalamud Code of Conduct](https://dalamud.dev/code-of-conduct).
If you used AI tooling at any point, review the [AI Usage Policy](https://dalamud.dev/plugin-publishing/ai-policy)
and disclose your level of AI use.
