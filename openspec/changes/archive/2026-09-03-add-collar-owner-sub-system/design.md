## Context

See `proposal.md` - Why, and `ffxiv-collar-system-design.md` at the repo root for the full feasibility research this plan is based on. Key constraint carried forward from that research: FFXIV is not server-authoritative like SL, so every command an Owner sends can only ever be *applied by the Sub's own client to the Sub's own local state* (`objectIndex 0`); visibility to third parties depends entirely on whatever Mare-successor sync tool (Snowcloak, Lightless, etc.) the Sub already has paired. Nothing in this design attempts to bypass that.

## Goals / Non-Goals

**Goals:**
- One Dalamud plugin, two roles (Owner UI / Sub UI), matching the existing pattern used by this author's other Dalamud projects.
- Consent and safety (pairing, permissions, panic) fully working end-to-end before any control-surface command can apply, matching the design doc's §5/§7 ordering.
- Each control surface (title, outfit, gesture, follow) independently permissioned and independently revertible.
- Reuse proven approaches from prior art (GagSpeak's input-hook technique for follow; the `RoleplayingVoiceDalamud`/`meowickz` pattern for gesture mod auto-toggling) rather than inventing new mechanisms where a working one already exists.

**Non-Goals:**
- Not attempting to make anything visible on a third party's screen directly — that remains the job of the Sub's existing sync tool.
- Not building a general-purpose relay product; the relay only needs to carry this plugin's command/ack traffic.
- Not shipping the follow/leash module as auto-fire-only — full automation of movement without a per-session Sub opt-in is out of scope for the first version.
- Not designing a moderation/reporting system for abuse between paired users; pairing is a two-party consent primitive, not a platform.

## Decisions

### Single plugin, dual role
One `CollarSystem.Plugin` codebase with an Owner window and a Sub window, switched by local config, rather than two separate plugins. Rationale: a single person often plays both sides across different characters/sessions, and shipping one plugin halves the update/compat burden. Alternative considered: two separate plugins — rejected, adds packaging and IPC-version-skew overhead for no real benefit.

### Relay: self-hosted minimal websocket relay (design-doc option 2), reusing the `lovense-media-hud` relay pattern (option 1) where practical
Either option 1 or 2 from the design doc satisfies the relay capability spec; both keep game-side automation limited to *applying* a command, never *transmitting* it through the game (ruling out option 3, chat-channel smuggling, entirely — that path adds its own ToS-relevant automation surface on top of everything else and gains nothing). Concretely: stand up a minimal self-hosted websocket relay (`CollarSystem.Relay`, ASP.NET Core minimal API) so the project has no third-party dependency for its core control channel, but model its auth/session pattern on the working relay in `lovense-media-hud` rather than designing one from scratch. Alternative considered: piggyback entirely on the `lovense-media-hud` infra as a second topic on shared infra — deferred rather than rejected; revisit once the relay capability's shape is proven here, since sharing infra later is easy but un-sharing later (if this project's needs diverge) is not.

### Command envelope shape
Every relay message carries `{ pairingId, category, commandId, payload, timestamp }` on the way down and `{ pairingId, commandId, status: applied|rejected|failed, detail? }` on the way back, so `collar/relay`'s ack requirement is satisfied uniformly regardless of which category's payload it wraps. Category-specific payload schemas (title text/color, outfit slot/state/key, gesture mod+emote id, follow lock/release) live with each category's command handler, not in the envelope.

### Permissions stored per-category, checked at the Sub before any IPC call
The Sub's plugin checks the relevant permission flag as the first step of handling any inbound command, before touching Glamourer/Penumbra/Honorific/input-hook state. Rejections short-circuit there and produce a `rejected` ack. This keeps the permission gate in one place per category instead of scattered through each IPC wrapper.

### Follow/leash as its own hook-based module, isolated from the IPC-based categories
Per the design doc's risk-tier note, movement-input hooking (`FFXIVClientStructs` + Dalamud `Hook<T>`) is architecturally separated from the IPC-based categories (title/outfit/gesture): it lives in its own module with its own enable/disable lifecycle, so a broken hook after a game patch degrades only the follow feature, not the whole plugin, and so it can be feature-flagged off entirely for users who don't want that risk tier at all.

### Gesture trigger flow: queue-and-confirm, not auto-fire
Matches `collar/gesture`'s spec requirement directly. The Sub's client always shows an incoming gesture prompt in its UI; the actual chat-injected emote command only fires after the Sub's own confirmation input. This is the mitigation the design doc recommends in §3 for the chat-automation ToS concern, applied consistently rather than left as a future toggle.

## Risks / Trade-offs

- **Signature-based movement hooks break on game patches** → isolate in its own module (see Decisions), budget for patch-day maintenance, and fail closed (movement lock auto-releases if the hook can't be verified working at plugin load).
- **Chat-injected emotes carry ToS risk** (per design doc §3) → mitigated by the mandatory Sub-confirmation step in `collar/gesture`; document the residual risk plainly in the README as the design doc's §5 calls for.
- **No public sync-fork API to call into directly** → this plugin only ever writes local Glamourer/Penumbra/Honorific state and depends on whatever sync tool the Sub already has running to propagate it; if the Sub has no sync tool paired, changes stay purely local/self-visible. Document this as a prerequisite, not a bug.
- **Self-hosted relay is a single point of failure for command delivery** → panic/safeword (`collar/pairing`) is explicitly designed to work with the relay down, so a relay outage degrades convenience, not safety.
- **Sync-fork landscape is still shifting post-Mare-shutdown** → the plugin has no direct dependency on any specific fork (it only writes local IPC state), so this design is insulated from which fork "wins"; revisit only if a fork ships something this plugin could call directly.

## Open Questions

- Final choice between fully sharing `lovense-media-hud`'s relay infra vs. a dedicated deployment for `CollarSystem.Relay` — deferred; doesn't change any spec or the task breakdown, since both satisfy `collar/relay` identically.
