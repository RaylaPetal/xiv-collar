## Context

See proposal.md - Why/What Changes. Relevant existing structure this builds on:

- `collar/outfit`'s `OutfitCommand` (wardrobe scan + allowlist, Sub-alias apply, Owner `ForceApply`/`ForceUnlock` "joker" override via `ChatCommandListener`'s reserved-keyword grammar) is the pattern this change repeats for restraint devices.
- `collar/slot-locking`'s `SlotLockManager` (per-owner claims on equipment slots, overlap refusal, panic-releases-all) is the pattern this change generalizes from equipment slots to restriction rules.
- `MovementLockService` already hooks `IsInputIdPressed`/`IsInputIdDown`/`IsInputIdHeld` to fully suppress movement for `/follow`'s leash. Forced-pose reuses this service as-is (full suppression); walk-only and action-block need their own, narrower hooks.
- `PanicHandler.Panic()` runs each teardown step independently via `RunStep`, so a new "release all restrictions" step slots in the same way `slotLocks.ReleaseAllForPanic` and `movementLock.Release()` already do.
- `collar/chat-transport`'s "no automated sending" rule governs the Owner→Sub *trigger* channel specifically (tells carrying the trigger phrase); it says nothing about the Sub's own free-typed chat, so gag mangling doesn't modify that spec - see Decisions below for why it's still called out as a distinct risk.

## Goals / Non-Goals

**Goals:**
- Reuse the existing wardrobe-scan/alias/force-apply plumbing rather than building a parallel device catalog mechanism.
- Keep each restriction rule's enforcement isolated per rule kind, so a broken hook signature after a game patch degrades only that rule kind (same fail-closed posture as `MovementLockService.IsAvailable`).
- Model rule conflicts the same way slot conflicts are modeled today: refuse the new claim, never silently override or merge.

**Non-Goals:**
- No new relay/network transport - device apply/release rides the existing `collar/chat-transport` tell-based command channel exactly as Outfit does.
- No arbitrary Sub-authored gag word-lists or garbling algorithms in this change - a single built-in garbling transform is enough to satisfy the spec; a configurable/pluggable garbler can be a later change.
- No changes to `collar/slot-locking` itself; equipment-slot locking and restriction-rule tracking stay as two separate ownership tables that happen to share a design pattern.

## Decisions

**One new capability, four rule kinds, one device model.** A device is a Glamourer design ID (from Restraints' own scan - see below) plus a set of assigned rule kinds and their parameters (e.g. which pose for forced-pose). This mirrors `OutfitAliasDefinition` closely enough that `RestraintDeviceDefinition` can live next to it in `PluginConfig` rather than inventing a different shape.

**Restraints scans independently of Wardrobe, with its own folder allowlist.** Bondage/restriction-themed designs and everyday outfits are different content a Sub organizes into different Glamourer design-browser folders in practice, so reusing Wardrobe's allowlist would force one shared scope for both. `RestraintMapping` gets its own `ScannedDesigns` catalog and `PluginConfig.RestraintFolderAllowlist`, scanned via `RestraintCommand.Rescan()` - same `GlamourerIpc.GetDesigns()` source and "empty allowlist = everything" semantics `OutfitCommand.Rescan` already uses, just a separate filter and separate result set. Tagging a device still just picks from this Restraints-scoped catalog, not Wardrobe's.

**`RestrictionRuleManager` generalizes `SlotLockManager`'s pattern to rule kinds instead of equipment slots.** Instead of `Dictionary<ApiEquipSlot, (Owner, Value)>`, it tracks `Dictionary<RuleKind, (Owner, Parameters)>` for the pose-conflict case (forced-pose is the only rule kind where two simultaneously active instances can actually disagree - a walk-only, action-block, or gag rule from a second device is redundant but never contradictory). Concretely: forced-pose claims a single conflict slot keyed by rule kind; walk-only/action-block/gag are reference-counted per rule kind (N devices asserting the same rule kind is fine, and the rule stays active until the last one is released) rather than exclusively owned. This is a deliberate divergence from `SlotLockManager`'s strict one-owner-per-slot model, called out explicitly because the spec's "non-conflicting duplicate rule kinds coexist" requirement would otherwise be unsatisfiable under a strict-ownership model.

**Forced-pose reuses `MovementLockService` directly; walk-only and action-block get their own services.** `MovementLockService.Engage()`/`Release()` already do exactly what forced-pose needs (full input suppression) - `RestrictionRuleManager` just calls it when a forced-pose rule count goes from 0→1 and releases when it goes back to 0, the same reference-counting `Engage`/`Release` already tolerates since they're idempotent. Walk-only and action-block are new, narrower hook targets:
- **Walk-only**: force the game's internal `IsWalking`/auto-run state and suppress whatever toggles it back to running (the same `FFXIVClientStructs` signature-hook approach as `MovementLockService`, targeting the walk/run state instead of raw movement input IDs), so directional input keeps working per the spec's requirement.
- **Action-block**: hook `ActionManager`'s action-use entry point (`UseAction`/equivalent) and return failure while the rule is active, mirroring `MovementLockService`'s `IsAvailable` fail-closed pattern - if the signature doesn't resolve on the current game version, the rule silently fails closed rather than claiming to block and not doing so, and this is logged the same way `MovementLockService` logs a resolution failure.

Both are isolated modules for the same reason `MovementLockService` already is one (design.md precedent in the original project design: signature hooks are a distinct, higher-maintenance risk tier from IPC calls) - a broken action-block signature after a patch shouldn't take down forced-pose or gag mangling.

**Gag mangling intercepts at the same single choke point `ChatSender` already is for outbound trigger messages, but for the Sub's own typed chat instead.** The existing `ChatSender.Send` only ever fires from a direct one-click UI action; gag mangling is different in kind - it must intercept chat the Sub types and sends through the game's own normal chat box, on any channel, which means hooking the game's chat-message-send pipeline (Dalamud's `IChatGui`/process-chat-box hook surface), not going through `ChatSender` at all. This is called out as its own risk tier in Risks/Trade-offs below, separate from and heavier than the `MovementLockService`-style signature hooks, because it intercepts and rewrites content the player authored themselves rather than blocking an input or command.

**Two-tier apply/release reuses `OutfitCommand`'s exact shape.** `RestraintCommand.Apply(alias)` / `.Unlock()` for Sub self-service, `.ForceApply(deviceName)` / `.ForceUnlock()` for the Owner override, gated by a new `SubRuntimeState.RestraintsForceLocked` flag alongside the existing `OutfitForceLocked`. `ChatCommandListener`'s reserved-keyword switch gains a `restraint` case next to `outfit`/`collar`/`title`/`gesture`/`moodle`.

**Panic releases restrictions before it resets runtime state**, as a new `RunStep` calling `restrictionRules.ReleaseAllForPanic()` (clears every tracked rule, releases `MovementLockService`, disables the walk-only/action-block/gag hooks) - inserted in `PanicHandler.Panic()` the same way `slotLocks.ReleaseAllForPanic` and `movementLock.Release()` already are, independent try/catch per step.

## Risks / Trade-offs

- **[Signature hooks break on game patches]** → Same mitigation `MovementLockService` already uses: each hook module reports its own `IsAvailable`, fails closed (never claims a restriction is enforced when the signature didn't resolve), and logs loudly. Walk-only and action-block are separate modules from forced-pose so one breaking doesn't take the others down.
- **[Gag chat interception is automation over content the player authored, a materially different ToS-risk shape than anything else in this plugin]** → This is the one piece of this change that doesn't have a "no automated sending" escape hatch the way `collar/chat-transport` does: the Sub explicitly opts in by tagging a device with the gag rule and applying it (self- or Owner-forced, both requiring the same pairing/permission gates every other category uses), and the transform only ever runs while a gag device is actively applied - never unconditionally. Document this plainly as its own risk category in the README's existing ToS-disclosure section, separate from the emote/follow automation caveat already there.
- **[Rule-kind reference counting diverges from `SlotLockManager`'s strict single-owner model]** → Deliberate, and documented above; a reviewer expecting the same one-owner-per-key invariant as slot locking should read the "non-conflicting duplicate rule kinds coexist" requirement first.
- **[Action-block or walk-only hook resolves to the wrong internal function and misfires on unrelated gameplay]** → Same signature-provenance discipline `MovementLockService`'s docstring already calls out (build on GagSpeak's proven, actively-maintained signatures where equivalents exist, rather than deriving new ones from scratch) - a task in tasks.md should confirm known-good signatures exist before implementation starts, not discover it mid-implementation.

## Migration Plan

Additive only - new config sections (`RestraintDeviceDefinition` list, `RestraintsForceLocked` flag), new nav tab, new services. No existing config shape changes, so no migration step is needed for existing installs; a fresh config simply has an empty device list until the Sub scans and tags designs.
