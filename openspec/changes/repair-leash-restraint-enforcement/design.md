## Context

See `proposal.md` for motivation. The current movement service hooks three `InputId` queries and omits GagSpeak's fourth/unknown query, complete-movement state, mouse movement path, and explicit unfollow protection. Follow engagement claims a lock but does not establish a follow target. Walk-only writes movement state but needs continuous correction; action block already uses the same `ActionManager.UseAction` entry point as GagSpeak but must participate in availability/atomic activation instead of silently leaving a rule unenforced.

The current Owner gesture quick commands retain imported Sub identity metadata, but restraint rule pickers call the shared local `AnimationPickerWindow`, whose source is `GestureMapping.LocalCatalog` on the Owner. Wire commands also place gesture IDs and raw Moodles names directly into visible tells.

## Goals / Non-Goals

**Goals:**

- Give every enforcement rule an explicit readiness contract and transactional activation.
- Separate immobilization, follow preservation, and walk enforcement while allowing reference-counted claims.
- Keep a stable machine identity without making ordinary tells look like serialized internals.
- Establish a distinct imported-Sub animation view model reusable by Gesture and Restraint Owner UI.

**Non-Goals:**

- Pathfinding, teleporting a leashed Sub, or following an Owner who is not a valid in-world target.
- Blocking operating-system input, actions outside supported game entry points, or bypasses from other plugins modifying the same native state.
- Live network catalog synchronization or automated background chat sends.
- Rendering Moodles markup inside Oathbound.

## Decisions

### Use a layered movement controller modeled on GagSpeak

Introduce one coordinator with independent owner tokens for `Immobilize` and `PreserveFollow`. At startup it resolves all required hooks/pointers and publishes granular readiness. Immobilization covers all known movement `InputId` queries (including the unknown query GagSpeak calls essential), the game's complete-movement-disable state asserted each framework tick, mouse-button movement, and autorun initiation. Follow preservation additionally protects the game's unfollow path while allowing follow-produced movement. This matches the defense-in-depth shape verified in Project-GagSpeak rather than assuming three key-query hooks cover every control scheme.

Alternatives considered: adding only the missing input ID hook leaves mouse and internal movement paths open; using only the complete-disable state would also stop leash-driven follow.

### Make leash engagement a transaction

Resolve the configured peer to an in-world target, start game follow, verify follow state, then acquire preservation claims and publish runtime active state. Any failed step unwinds prior steps. Release drops claims, cancels follow owned by Oathbound, and clears runtime state. Panic and unpair continue through the same idempotent teardown.

### Make rule activation capability-aware and atomic

Each enforcer exposes readiness and an `TryEngage` result. Restraint application preflights the slot, every requested rule, pose/animation resolution, and temporary Penumbra activation before committing active-device bookkeeping. On failure it unwinds temporary settings, locomotion state, rule claims, and slot changes. Walk-only saves the prior walk state on first claim and reasserts both normal and automove walking every framework update. Action block keeps the supported `UseAction` detour and becomes a hard preflight dependency.

### Store identity and presentation separately

Extend quick-command/imported metadata with a structured selector: stable ID plus sanitized display components. A single codec builds and parses quoted selectors with explicit escaping. New tells prefer readable composite selectors; receiver resolution normalizes readable fields and requires exactly one match. A short stable suffix is included only when needed to disambiguate. Parsers retain legacy ID/raw-name branches, and saved commands are lazily rewritten only after successful resolution so rollback remains possible.

Alternatives considered: Base64/JSON remains visually noisy; labels without identity become ambiguous; Unicode zero-width metadata is fragile and misleading in chat.

### Separate imported Sub catalogs from local catalogs

Retain imported gesture entries in an Owner-side shared catalog keyed by stable Sub identity. Refactor the animation picker to accept an explicit data source and selection DTO instead of reaching into global local configuration. Sub alias editing passes the local catalog; Owner restraint editing passes the imported Sub catalog. No Owner-side rescan control appears in the latter mode.

### Sanitize Moodles only at presentation boundaries

Keep raw names/IDs for IPC matching and legacy parsing, but strip known markup when producing labels and new wire selectors. Collision detection adds stable disambiguation and rejects ambiguous input rather than guessing.

## Risks / Trade-offs

- [Game patches invalidate signatures or offsets] → Treat readiness as false, reject dependent commands, log the missing capability, and isolate signatures behind one service with runtime probes.
- [The complete movement-disable state conflicts with another plugin] → Track only Oathbound's claims, reassert while claimed, and avoid clearing state known to be held independently where ownership can be detected.
- [Follow target disappears or zones] → Terminate the Oathbound-owned follow safely, release movement claims, and report the interruption locally rather than leaving the Sub frozen.
- [Readable selectors increase tell length] → Use the shortest unique component set and enforce the game's message-length limit before exposing Send.
- [Compatibility parser broadens accepted grammar] → Parse legacy forms only after sender/trigger/permission validation and remove no existing safety gate.

## Migration Plan

1. Add structured imported-catalog fields and tolerate absent fields when loading existing configuration.
2. Introduce codecs that read both legacy and readable forms; keep existing commands unchanged on load.
3. Switch newly imported/created commands and restraint selections to structured metadata and readable output.
4. Replace enforcement services behind existing consent and panic interfaces, with readiness diagnostics enabled before activation.
5. After successful legacy command resolution, optionally persist its readable replacement; rollback remains compatible because legacy parsing and original stable identities are retained.

