## Why

1. **Moodles has no Sub-side alias menu.** Every other category with a self-apply concept (Title, Outfit, Gesture, Restraints) lets the Sub define a short alias word that applies a specific target when triggered - `AliasBook.cs` holds `Titles`/`Outfits`/`Gestures`/`Restraints`, each with their own tab in `CollarWindow`. Moodles has none of this: confirmed by reading `ChatCommandListener.ResolveAlias` fully (8 branches: clear-title, unlock-outfit, follow engage/release, Titles, Outfits, Gestures, Restraints - no Moodles branch at all) and `MoodlesCommand.cs`'s own doc comment, which states plainly "No alias dictionary involved at all... Moodles only ever exist as this one reserved-keyword command." A Sub can capture their own Moodles catalog but has no way to self-trigger a favorite status the way they can self-trigger a title, outfit, or gesture.
2. **There's no way to combine several actions behind one trigger.** Every alias today maps to exactly one action in exactly one category. A Sub who wants "one word that puts on this outfit, this title, and this restraint device together" has no way to define that - they'd need the Owner to send three separate commands.

## What Changes

- **A new Moodles Sub-alias tab**, mirroring Title/Outfit/Gesture/Restraints exactly: the Sub picks a status from their own scanned catalog and gives it a short alias word (plus an optional dedicated "clear Moodle" alias, matching `ClearTitleAlias`/`UnlockOutfitAlias`'s existing pattern). Reuses `MoodlesCommand.ForceApply(name)`/`ForceClear()` unchanged - the alias path is new, the underlying apply/clear logic is not.
- **A new "Custom Triggers" capability**: the Sub can define a named trigger that bundles multiple actions - any combination of Title, Outfit, Gesture, Moodle, Restraint, and a raw chat message - fired together as one alias, resolved through the exact same `ResolveAlias` mechanism every other alias already uses (no new wire syntax for this path - the Owner just sends the alias word, exactly like today).
- **The Owner can also author an ad-hoc Custom Trigger directly**, without any Sub-side name - mirroring Restraints' existing `wear` ad-hoc pattern (Sub-named devices *and* Owner-authored ad-hoc gear already coexist there). The Owner composes the same bundle of actions in the Owner tab and sends it as one self-contained command; the Sub's client executes whatever's encoded, subject to the same per-category permission gates as everything else.
- **Every sub-action inside a Custom Trigger is still gated by its own category's existing permission** (a Title action needs Title permission, a Restraint action needs Restraints permission, etc.) - bundling into one trigger never bypasses a permission a Sub hasn't granted; an ungranted sub-action is simply skipped while the rest of the bundle still fires.
- **Chat-message actions get their own new, explicit consent gate.** Per your explicit choice (not the safer default), a chat action can send *any* text to *any* channel - not restricted to Gesture's existing closed set of self-targeting pose/emote commands. This is a materially larger automation-risk surface than anything in this plugin today: it can make the Sub's character say arbitrary things in public chat (party/say/yell/etc.), driven remotely by the Owner. It gets its own permission (independent of Title/Outfit/Gesture/Follow/Collar/Moodles/Restraints) and its own explicit acknowledgement, separate from the existing general automation-risk ToS checkbox - disclosed prominently in the README's Automation risk section, the same way Gagged chat-mangling and Restraints' ad-hoc gear got their own explicit call-outs.

## Capabilities

### Modified Capabilities
- `collar/moodles`: adds a Sub-side alias path (self-apply/clear by alias), alongside the existing Owner-only override.

### New Capabilities
- `collar/custom-triggers`: a Sub-defined or Owner-authored bundle of actions across every other category plus a raw chat message, fired as one trigger.

## Impact

- `CollarSystem.Plugin/Config/AliasBook.cs` - new `MoodlesAliasDefinition` and `ClearMoodleAlias`; new `CustomTriggerDefinition`/`CustomTriggerAction` types.
- `CollarSystem.Plugin/Commands/MoodlesCommand.cs` - gains `Apply(MoodlesAliasDefinition)`/`Clear()` Sub-alias methods, thin wrappers over the existing `ForceApply`/`ForceClear`.
- `CollarSystem.Plugin/Commands/CustomTriggerCommand.cs` (new) - applies a `CustomTriggerDefinition`'s actions in sequence, checking each sub-action's own category permission independently and reporting which sub-actions fired vs. were skipped.
- `CollarSystem.Plugin/Commands/ChatCommandListener.cs` - `ResolveAlias` gains a Custom Triggers branch (name-referenced, same shape as every other alias category); a new sub-action for the Owner's ad-hoc bundle (mirroring Restraints' `wear`).
- `CollarSystem.Plugin/Config/PluginConfig.cs` - new `Permissions.CustomChatMessages` (or similarly named) permission, independent of every existing category permission, gating chat-message sub-actions specifically; a new config flag for the chat-message-specific acknowledgement, separate from the existing general `TosAcknowledged`.
- `CollarSystem.Plugin/UI/CollarWindow.cs` - new "Moodles" Sub tab; new "Custom Triggers" Sub tab (define a trigger, add actions to it); new Owner-side ad-hoc Custom Trigger authoring section (mirroring `DrawAdHocRestraintSection`).
- **No breaking change**: both additions are new, opt-in capabilities - nothing existing changes behavior. The new chat-message permission defaults to disabled, so no existing pairing gains the ability to send arbitrary chat until the Sub explicitly enables it and completes its own acknowledgement.
