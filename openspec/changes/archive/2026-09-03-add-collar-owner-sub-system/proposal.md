## Why

`ffxiv-collar-system-design.md` establishes that an SL OpenCollar-style control system is feasible in FFXIV only as a consent-based, client-applied system: an Owner's plugin sends commands, and a Sub's own plugin (paired and running) applies them locally via Glamourer/Penumbra/Honorific IPC, relying on existing sync tooling to make the result visible to others. No such plugin exists yet in this repo. This change turns that feasibility study into an implementable plan for the first end-to-end version: pairing/consent, a command relay, and the four control surfaces (title, outfit, gesture, follow).

## What Changes

- Add a Dalamud plugin with two roles (Owner/Sub) sharing one codebase, per the design doc's recommended architecture.
- Add a **pairing & consent system**: one-time pairing code/handshake, per-category permission toggles (outfit/gesture/follow/title), and a local panic/safeword that unpairs and reverts all state independent of network connectivity. Built first, before any control surface, per the design doc's explicit ordering.
- Add a **command relay layer**: Owner → Sub command channel (outfit/gesture/follow/title commands plus ack/current-state replies), built on a websocket/relay pattern rather than in-game chat smuggling.
- Add **title control**: Owner sends a title command; Sub's plugin applies it via `Honorific.SetCharacterTitle`/`ClearCharacterTitle` against the Sub's own `objectIndex 0`.
- Add **outfit control**: Owner sends an outfit command; Sub's plugin applies it via `Glamourer.Api` (`SetItem`/`ApplyState`), using `key` + `ApplyFlag.Lock` so only the Owner's key can unlock it.
- Add **gesture control**: Sub's plugin scans Penumbra mods (`GetModList`/`GetChangedItems`) to auto-build a mod→emote catalog (optionally scoped to an allowlisted mod folder), relays that catalog to the Owner, and on trigger ensures the mapped mod/collection is active and fires the emote via chat injection — gated behind an explicit sub-side confirmation step, not full auto-trigger, per the design doc's ToS mitigation.
- Add **follow/leash control**: Sub's plugin hooks movement input (`FFXIVClientStructs` + Dalamud `Hook<T>`) to block movement and suppress auto-unfollow while a lock is active, gated behind a separate "hardcore/movement-lock" permission toggle from §5 of the design doc, reflecting its higher risk tier.
- **BREAKING**: N/A — net-new plugin, no existing behavior changes.

## Capabilities

### New Capabilities
- `collar/pairing`: pairing handshake, scoped/revocable permission toggles, and the panic/safeword safety layer.
- `collar/relay`: the Owner→Sub command transport (send command, ack/current-state reply), independent of any single command's payload semantics.
- `collar/title`: Owner-issued title commands applied via Honorific on the Sub's client.
- `collar/outfit`: Owner-issued outfit commands applied via Glamourer, including the lock/key model.
- `collar/gesture`: Penumbra-backed gesture cataloging, relay, and sub-confirmed triggering.
- `collar/follow`: movement-lock (leash) enforcement via input hooking, as an opt-in higher-risk module.

### Modified Capabilities
(none — no existing specs in this repo)

## Impact

- New Dalamud plugin project (`CollarSystem.Plugin`) plus optional self-hosted relay (`CollarSystem.Relay`), per the design doc's suggested project layout.
- New dependencies: `Penumbra.Api`, `Glamourer.Api`, Honorific IPC contracts, `ECommons` (chat injection + IPC helpers), `FFXIVClientStructs`, `DalamudPackager`.
- Runtime dependency on the sub having a Mare-successor sync tool (Snowcloak/Lightless/etc.) paired and running, since that tool — not this plugin — propagates the sub's changed local state to other players.
- ToS-sensitive surfaces (chat-injected emotes, input-blocking hooks) require the disclosure and opt-in gating called out in the design doc's §5 and the gesture/follow sections above.
