# FFXIV "Collar" Control System — Feasibility Study & Design Plan

Analogous to SL OpenCollar (force outfit, force gesture, force follow/leash, force title). Based on reading `Penumbra.Api` and `Glamourer.Api` source directly (IpcSubscribers folders, current `main` branch), plus current ecosystem state as of Sept 2026.

## 1. The core problem: SL is server-authoritative, FFXIV is not

This is the fact that reshapes every feature below, so it goes first.

In SL, an OpenCollar attachment is a **scripted object the sim server tracks**. When it force-wears an outfit or plays a gesture, that's a real inventory/attachment change the server broadcasts — everyone's viewer shows the same thing, and RLV lets the collar restrict the *wearer's own viewer* (detach lock, camera lock, etc.) with the server backing up the state.

In FFXIV, **Penumbra and Glamourer are pure client-side rendering hacks**. They edit in-memory model/equip data on one person's machine. The game server only knows your *real* equipped gear IDs. Nobody else sees your Penumbra mods or Glamourer edits unless their own client independently re-applies the same data — which is exactly why Mare Synchronos (and its forks) exist: a relay server + client plugin pair that packages up "here's what Player X's Penumbra/Glamourer state actually is" and pushes it to everyone who has X paired, so their clients render it too.

Consequence: **there is no way to force something onto another player's screen from outside their own client.** Every feature below has to be architected as *"Player A's plugin sends a request → Player B's own plugin, running on B's own machine, applies the change to B's own local character state → existing sync tooling (or your own relay) propagates B's new state to whoever's watching."* This is consent-based by construction — B's plugin has to be installed, paired, and running for any of this to work, and B can always alt-F4 their way out of it. That's actually fine and matches OpenCollar's real-world trust model (the wearer always has the ability to detach in a pinch too) — just be upfront that this is the honest framing, not "true" force.

## 2. Ecosystem status check (as of this research)

- **Penumbra** and **Glamourer** (Ottermandias) are alive, actively maintained, and expose stable IPC surfaces (`Penumbra.Api`, `Glamourer.Api` — both confirmed current on GitHub).
- **Honorific** (Caraxi) is the de facto nameplate-title plugin, exposes `Honorific.SetCharacterTitle` / `Honorific.ClearCharacterTitle` IPC, keyed by `objectIndex`.
- **Mare Synchronos** itself shut down (the original project pulled its client/server/API in 2025 following a legal inquiry). The ecosystem forked hard: **Snowcloak Sync**, **Lightless Sync**, and others now fill the same role (Penumbra + Glamourer state sync between paired users, Honorific sync included). Square Enix's Yoshida also posted an official Lodestone statement addressing mod/third-party-tool usage — worth reading before you ship anything, since the tolerance boundaries may have shifted. I'd re-check current community consensus on which sync fork is "the" one before committing to an integration target.
- None of these forks appear to expose a public third-party plugin API for *external* tools to piggyback on their sync channel — you'd be relying on the *fact* that a paired sync tool exists and re-syncs whatever Glamourer/Penumbra/Honorific state you write locally, not calling into it directly.

## 3. Feature-by-feature feasibility

### Force outfit — ✅ Feasible (self-apply + rely on sync tool for visibility)
`Glamourer.Api` (`IGlamourerApiItems`, `IGlamourerApiState`) gives you exactly what you need:
- `SetItem(objectIndex, slot, itemId, stains, key, flags)` — set one gear slot.
- `ApplyState(jsonOrBase64, objectIndex, key, flags)` — apply a whole saved design/state blob.
- **`key` + `ApplyFlag.Lock`** — locks the state so it can't be casually reverted without the same key. This is your closest equivalent to an RLV force-wear lock. `CanUnlock` / `UnlockState` let the Dom's plugin be the only one holding the key.
- All calls target `objectIndex` (or `playerName` variants) — for this use case you always target **your own local player (objectIndex 0)**, because that's the only state whose changes will actually reach anyone else (via whatever sync tool the sub already runs).

### Force gesture/emote — ⚠️ Feasible, but bounded by what's already installed
This is the one that doesn't map cleanly, and matches your intuition. Two separate mechanisms have to work together:

1. **Which animation plays** is determined by Penumbra: a mod replaces the `.pap`/animation files behind a specific vanilla emote slot for whoever's active Penumbra collection has that mod enabled. You cannot ship or force-install someone else's custom animation file onto their machine remotely — that's arbitrary file delivery to another person's game folder, which is both a security red flag and outside what any of these APIs do. The sub has to have already downloaded/consented to the specific gesture pack.
2. **Triggering the emote** is just running the game's own emote command (`/emote lockon`, `/dote`, etc.) via chat-box injection — commonly done through the community helper library **ECommons** (`ECommons.Automation.Chat.SendMessage(...)`). There's real prior art for the exact "auto-toggle a Penumbra mod, then play the mapped emote" pattern — e.g. `RoleplayingVoiceDalamud`/Artemis Roleplaying Kit ships "automated mod switching for animation mods, enter the mod name to trigger" as a shipped feature, and `meowickz/emotes` explicitly does "associate Penumbra mods that auto-toggle when an emote plays."

So the realistic version of "force gesture" is an **automatic scan-and-cache model** — and this can genuinely be non-manual, because Penumbra already does the mod→emote resolution work internally:

**Why manual tagging isn't needed.** Penumbra's own `GetChangedItems(modDir, modName)` doesn't just return raw file paths — it runs each mod's redirected files through Penumbra's internal identification pipeline, which includes a purpose-built `.pap`/`.tmb` → game `Emote` lookup (`Penumbra.GameData`'s `DictEmote`, built by parsing every entry in the game's own Emote sheet and its animation timelines at startup). Confirmed directly in source: when a mod's file list matches a known emote animation path, `ObjectIdentification` tags the changed item as `"Emote: <actual emote name from the game sheet>"` (e.g. `"Emote: Doze"`) automatically — no per-mod configuration by the sub required. So:

1. **Scan**: sub's plugin calls `GetModList()` for the mod catalog, then `GetChangedItems(modDir, modName)` per mod. Any mod that touches emote animation files comes back pre-labeled with the real emote name(s) it affects — that's your gesture list, generated for free.
2. **Filter/organize**: sub can still use Penumbra's native mod folders (the sub already organizes mods into folders/groups in the normal Penumbra UI for their own browsing) to scope which folders your plugin should scan — e.g. only an "Approved for Dom" folder — rather than exposing every installed mod. This is the "filter" part of what you described: a folder-path allowlist, not manual per-mod tagging.
3. **Relay & cache**: the resulting catalog — mod name, folder path, and the auto-resolved emote name(s) it maps to — is sent to Dom's plugin and cached locally, so Dom can browse/enable entries offline.
4. **Trigger**: Dom enables a cached mod entry, picks the matching gesture from the (auto-populated) list Penumbra already told you it maps to, and sends back the mod ID + emote name. Sub's client ensures that mod/collection is active (`TrySetMod`/`SetCollectionForObject`, or `AddTemporaryMod` for a clean scoped/reverting swap) and fires the matching emote command through chat injection. Only opaque IDs cross the wire — file resolution and identification all happen locally on each side.

**Edge case worth designing for:** not every animation mod redirects a file Penumbra's dictionary recognizes (e.g. some replace generic motion files that aren't uniquely tied to one emote, or a mod might affect multiple emotes at once — `GetChangedItems` can return several `"Emote: X"` entries for one mod). Surface those as multi-select or "unresolved — assign manually" in the sub's UI rather than silently dropping them, so the automatic path covers the common case and manual tagging becomes the fallback, not the default.

**⚠️ ToS flag:** automating chat-box/emote input on someone's behalf is explicitly called out by plugin authors as against FFXIV's ToS (see EmoteReactor's own disclaimer) — distinct from Penumbra/Glamourer's purely-cosmetic, no-input-automation profile, which is more tolerated. Worth deciding deliberately whether the sub always presses a hotkey to "accept" a queued gesture (removing the automation-on-your-behalf risk) vs. fully auto-fires. I'd lean toward "Dom sends a *prompt*, sub's client visibly queues it and the sub confirms" for the emote/follow pieces specifically.

### Force follow/leash — ✅ Feasible, but needs real movement locking, not just `/follow`
`/follow <target>` alone isn't a leash — the sub can cancel it by just pressing a movement key, and there's no IPC for "disable someone's WASD." **GagSpeak** (an existing FFXIV kink/collar Dalamud plugin, cloned and read directly) already solves this, and it's worth building on their proven approach rather than reinventing it:

- **Input blocking**: hook the game's own `IsInputIdPressed`/`IsInputIdDown`/`IsInputIdHeld` functions (via `FFXIVClientStructs` signatures + Dalamud's `Hook<T>` detouring API — a standard, documented Dalamud capability, just a heavier one than plain IPC) for the movement input IDs (`MOVE_FORE`, `MOVE_BACK`, `MOVE_STRIFE_L/R`, `MOVE_LEFT/RIGHT`, etc.), and force them to report "not pressed" while a lock flag is active. That's the actual leash.
- **Keep `/follow` from self-cancelling**: separately hook the "movement cancels auto-move / cancels follow" logic so nudging a key doesn't break the leash once it's engaged (GagSpeak calls these `NoAutoMoveActive` / `NoUnfollowingActive`).
- **Task-based movement** (optional, more advanced): rather than one-shot commands, queue steppable movement tasks (walk-to-point, hold-position) instead of firing raw input.

**Risk tier note:** this is a materially different risk category from Penumbra/Glamourer IPC calls. Signature-based hooks break on every game patch and need active maintenance (tracking `FFXIVClientStructs` releases), and blocking a player's own input is a heavier automation footprint than cosmetic rendering changes. Treat this as its own module with its own patch-maintenance budget, and gate it behind the same "sub explicitly enables hardcore/movement-lock permission" toggle discussed in §5 — don't bundle it into the same risk tier as outfit/title.

### Force title (honorific) — ✅ Feasible
`Honorific.SetCharacterTitle(objectIndex, jsonTitleData)` / `Honorific.ClearCharacterTitle(objectIndex)`. Call it against your own `objectIndex 0`. Title text, color, glow, prefix/suffix are all in the `TitleData` payload. Whatever sync tool is paired will propagate it, since Honorific ships with built-in sync-tool integration.

## 4. Recommended architecture

Two plugin roles sharing one codebase (like PoseKit/your other Dalamud projects — one Dalamud plugin, different UI/behavior per configured role), plus a thin relay:

```
┌────────────────────┐        pairing code / relay          ┌────────────────────┐
│   Dom's client      │ ───────────────────────────────────▶ │   Sub's client      │
│  (Dalamud plugin)   │      "outfit:X" / "gesture:Y" /       │  (Dalamud plugin)   │
│  sends commands      │      "follow:on" / "title:Z"          │  receives + applies  │
└────────────────────┘ ◀─────────────────────────────────── └────────────────────┘
                                ack / current-state                      │
                                                                          ▼
                                                        Glamourer.Api / Penumbra.Api /
                                                        Honorific IPC — all targeting
                                                        the SUB's own objectIndex 0
                                                                          │
                                                                          ▼
                                                     Existing sync fork (Snowcloak/Lightless/etc.)
                                                     picks up the sub's new local state and
                                                     propagates it to everyone paired with the sub
```

**Communication layer options** (Dom → Sub command channel):
1. **Reuse your existing XToys/webhook relay pattern from `lovense-media-hud`.** You already have a working external relay + auth model; standing up a second small websocket channel ("collar-control" topic) on the same infra is the least new surface area and keeps a consistent architecture across your SL and FFXIV projects.
2. **Self-hosted minimal relay** (websocket or long-poll HTTP) purpose-built for this — simplest, full control, no dependency on a third party's terms.
3. **In-game chat channel smuggling** (tell/party chat with an encoded payload) — avoid. Fragile, easy to leak into public chat, and adds its own ToS surface for message spam/automation on top of everything else.

Go with **option 1 or 2**; both keep game-side automation limited to *applying* a command locally, not *transmitting* it through the game.

## 5. Consent & safety (build this before the fun features)

- **Pairing handshake**: sub generates/shares a one-time code with dom; nothing applies until sub's plugin explicitly accepts. Never auto-accept a first-time pairing.
- **Panic/safeword**: a always-available local hotkey or command on the sub's side that immediately unpairs, `RevertState`s Glamourer, `ClearCharacterTitle`s Honorific, and cancels follow — independent of network state, so it works even if the relay is down.
- **Scoped, revocable permissions**: let the sub toggle which categories (outfit/gesture/follow/title) are currently allowed, not just a single on/off.
- **Uninstall is always the ultimate safeword** — document this plainly in your README; it's the honest FFXIV equivalent of SL's "detach" and there's no way around it existing.
- **ToS disclosure**: put the automation-risk caveat (emote/follow chat injection) front and center in your own docs, same as EmoteReactor does, so the sub is making an informed choice.

## 6. Suggested VS Code project layout

```
CollarSystem/
├── CollarSystem.sln
├── CollarSystem.Plugin/              # the Dalamud plugin itself
│   ├── CollarSystem.Plugin.csproj
│   ├── Plugin.cs                     # entry point, DI setup, role switch (Dom/Sub UI)
│   ├── Ipc/
│   │   ├── GlamourerIpc.cs           # thin wrapper around Glamourer.Api calls you use
│   │   ├── PenumbraIpc.cs            # mod/collection lookups + temp-mod assignment
│   │   └── HonorificIpc.cs
│   ├── Commands/
│   │   ├── CommandDispatcher.cs      # receives relay messages, routes to handlers
│   │   ├── OutfitCommand.cs
│   │   ├── GestureCommand.cs
│   │   ├── FollowCommand.cs
│   │   └── TitleCommand.cs
│   ├── Relay/
│   │   └── RelayClient.cs            # websocket client, reuse pattern from lovense-media-hud
│   ├── Config/
│   │   ├── PluginConfig.cs           # pairing state, permission toggles, gesture map
│   │   └── GestureMapping.cs         # sub's local {gestureId → Penumbra mod + emote slot}
│   ├── UI/
│   │   ├── DomWindow.cs              # command panel
│   │   └── SubWindow.cs              # incoming request / panic button / permission toggles
│   └── Safety/
│       └── PanicHandler.cs
├── CollarSystem.Relay/                # only if you go with option 2 (self-hosted)
│   └── (minimal websocket relay, e.g. ASP.NET Core minimal API)
└── README.md                          # ToS caveats, consent model, setup
```

Reference the two API repos as NuGet/project deps the same way any Penumbra/Glamourer-integrating plugin does (`Penumbra.Api`, `Glamourer.Api` packages), plus `DalamudPackager` for the plugin build, and `ECommons` if you want its chat-injection + IPC helper utilities rather than hand-rolling them.

## 7. Suggested build order

1. **Title (Honorific)** — smallest surface area, no ToS ambiguity, good end-to-end pairing/relay test.
2. **Outfit (Glamourer)** — proves out lock/key model and state application.
3. **Panic/safety layer** — before touching anything automation-adjacent.
4. **Gesture** — requires the sub-side gesture-mapping config UI (their own "gesture folder" equivalent) before it's usable.
5. **Follow/leash** — last, since it's the most ToS-sensitive; consider shipping it opt-in/experimental with the "sub confirms via hotkey" mitigation rather than full auto-trigger.

---

If you want, next step I can draft the actual `PluginConfig`/`GestureMapping` data model and the `CommandDispatcher` skeleton in C# to drop straight into VS Code.
