## Context

`ChatCommandListener.ResolveAlias` (confirmed by tracing it in full) checks exactly 8 fixed branches in sequence - `ClearTitleAlias`, `UnlockOutfitAlias`, `Follow.EngageAlias`, `Follow.ReleaseAlias`, then `Titles`/`Outfits`/`Gestures`/`Restraints` - each a one-alias-to-one-action mapping, returning on first match. `MoodlesCommand.cs`'s own doc comment states plainly there is "No alias dictionary involved at all" for Moodles - it only exists as the Owner's reserved-keyword override (`ForceApply(string statusName)`/`ForceClear()`, both already `bool`-returning and directly reusable).

Each existing self-apply method has a different return shape: `TitleCommand.Apply` returns `void` (silently no-ops when `TitleForceLocked`); `OutfitCommand.Apply`, `RestraintCommand.Toggle`, `GestureCommand.Apply`/`ForceApply`, and `MoodlesCommand.ForceApply`/`ForceClear` all return `bool`. None share per-frame or per-message state, so calling several in sequence from one dispatch point is mechanically safe - each already independently checks its own force-lock flag (`SubRuntimeState.TitleForceLocked`/`OutfitForceLocked`/`RestraintsForceLocked`) and no-ops/returns false rather than throwing or corrupting shared state.

`GestureCommand.Play` already calls `ECommons.Automation.Chat.SendMessage(...)` directly and unconditionally today for a closed set of self-targeting commands (`/sit`, `/groundsit`, `/doze`, or one specific slash-emote), gated only by the Gesture permission + the existing general `TosAcknowledged`. Per the archived `add-restraints-devices` design doc, this plugin's "no automated sending" invariant is explicitly scoped to the Owner→Sub *trigger tell* channel - it says nothing about the Sub's own client sending its own local chat/emote commands. Custom Triggers' chat action is a deliberate, explicit expansion of that existing local-chat precedent from "a closed set of self-targeting commands" to "arbitrary text, any channel" - a materially bigger step, per your explicit choice, which is why it gets its own new permission and acknowledgement rather than riding on the existing Gesture/TosAcknowledged gates.

`RestraintCommand`'s existing `wear`/`BuildWearCommand`/`TryParseWearCommand` (a quoted label + a `rules:`-prefixed token list, `RulesToken = "rules:"`) is the direct precedent for Custom Triggers' own ad-hoc wire encoding and for a heterogeneous-action list encoding in general.

See proposal.md - Why/What Changes for full motivation and the explicit chat-scope decision.

## Goals / Non-Goals

**Goals:**
- Moodles gets the exact same Sub-alias shape every other category already has - no new UI pattern, no new underlying apply logic.
- A Custom Trigger reuses every existing per-category action's own apply method and permission check - it's an orchestrator, not a reimplementation of Title/Outfit/Gesture/Moodle/Restraint logic.
- The Sub-defined path needs zero new wire syntax (resolved through the existing alias dictionary, like everything else); only the Owner's ad-hoc path needs a new command shape, mirroring Restraints' existing `wear`.
- The chat action's expanded risk surface is explicit, gated by its own dedicated permission and acknowledgement, and prominently disclosed - never silently folded into an existing gate.

**Non-Goals:**
- Editing a Custom Trigger's bundle after creation beyond add/remove-action - no reordering UI, no conditional logic between actions (they always all attempt to apply, independently, in the order added).
- Any chat-message safety filtering (profanity, length limits, rate limiting) - the Sub's own explicit configuration of the message and channel, gated by the new permission/acknowledgement, is the only safeguard; this mirrors how nothing else in this plugin second-guesses a Sub's own configured text (e.g. a Title's text is never filtered either).
- Making Moodles' new alias path support the Owner's existing reserved-keyword override any differently than today - `moodle apply <name>`/`moodle clear` are untouched; only a new, separate Sub-alias path is added alongside them.

## Decisions

**`CustomTriggerAction` is one flat class with a `Kind` enum and per-kind optional fields**, mirroring `RestraintRuleAssignment`'s existing exact shape (`Kind` + `PoseModeId`/`AnimationId`, only the relevant fields used per kind) rather than a class hierarchy or discriminated union. Consistent with this codebase's established pattern for "one of several kinds of thing in a list," and trivial to serialize with the same `System.Text.Json` behavior every other config type already uses.

**`CustomTriggerCommand.Apply` iterates actions and dispatches to each category's own existing method** (`title.Apply`, `outfit.Apply`, `gesture.Apply`/`ForceApply`, `moodles.ForceApply`/`ForceClear`, `restraints.Toggle`), checking that action's own category permission (`config.Permissions.Title`/`Outfit`/`Gesture`/`Moodles`/`Restraints`, or the new `CustomChatMessages` permission for a chat action) immediately before calling it - never bypassing a permission check that action would already require if triggered on its own. Each `void`-returning call (`Title.Apply`) is treated as "attempted" for reporting purposes; each `bool`-returning call's result is used to report success/failure per action.

**The chat action sends via the same `ECommons.Automation.Chat.SendMessage` call `GestureCommand.Play` already uses** - no new chat-transport mechanism, just a new, far less restricted call site gated by its own permission/acknowledgement pair. The configured text is sent completely unmodified (no channel-prefix parsing/validation beyond what FFXIV's own client already does with a raw `/command` string) - the Sub is responsible for typing a valid channel prefix themselves when configuring the action, the same way they're responsible for typing valid title text today.

**The Owner's ad-hoc bundle is encoded as a new `customtrigger cast` wire command**, structurally following `restraint wear`'s exact shape: a quoted label, then one segment per included action type (`title=...`, `outfit=...`, `gesture=...`, `moodle=...`, `restraint=...`), with the chat action's raw text always last in the command (consuming the remainder of the line) since arbitrary chat text can't be reliably delimited alongside other tokens the way short ids/names can. A bundle with no chat action needs no special positioning. This is a deliberate, pragmatic encoding choice for the common case, not a fully general escaping scheme - noted here rather than glossed over.

**Sub-defined Custom Triggers need no new wire syntax at all** - `ResolveAlias` gains one more branch (`aliases.CustomTriggers`) checked after `Restraints`, applied via `CustomTriggerCommand.Apply`, exactly like every other alias category. The Owner never sees or needs to know a Custom Trigger's contents, same as any other alias.

## Risks / Trade-offs

- **The chat action is a real, non-trivial expansion of what an Owner can make a Sub's client do.** → Mitigated as far as this plugin's own conventions allow: its own dedicated permission, its own dedicated acknowledgement (not reusing the general ToS checkbox), and prominent README disclosure - the Sub retains full control over exactly what text/channel is configured, and can revoke the permission at any time, same as every other category.
- **A partially-permitted bundle (some actions skipped) could surprise a Sub who forgets which categories they've disabled.** → The per-action skip behavior matches how every individual category already silently no-ops when its own permission is disabled (a plain `outfit lock` today doesn't error when Outfit permission is off, it just does nothing) - Custom Triggers extend that same expectation to a bundle rather than introducing a new failure mode.
- **The `customtrigger cast` ad-hoc encoding's "chat text must be last" rule is easy to get wrong when extending this later** (e.g. adding an eighth action kind that also needs free text). → Documented explicitly here and should be called out again in that hypothetical future change, not assumed obvious.
