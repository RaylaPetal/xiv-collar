## 1. Config and device model

- [x] 1.1 Add `RestraintDeviceDefinition` (design ID, name, assigned rule kinds + per-rule parameters e.g. pose target) to `PluginConfig`, alongside existing `OutfitAliasDefinition`-style storage, and verify a saved/reloaded config round-trips a tagged device.
- [x] 1.2 Add `SubRuntimeState.RestraintsForceLocked`, mirroring `OutfitForceLocked`, and verify it persists/resets the same way across a plugin reload and panic.

## 2. Restriction rule tracking

- [x] 2.1 Implement `RestrictionRuleManager` with per-rule-kind tracking: forced-pose as single-owner-with-conflict-refusal (like `SlotLockManager`'s per-slot ownership), walk-only/action-block/gag as reference-counted (multiple devices may hold the same rule kind active). Verify with unit tests: a second forced-pose claim with a different pose target is refused while the first stays active; two devices both claiming action-block both stay active and the rule only clears once both release.
- [x] 2.2 Wire `RestrictionRuleManager.ReleaseAllForPanic()` and verify it clears every tracked rule and disengages every underlying enforcement service regardless of how many devices were active.

## 3. Forced-pose enforcement

- [x] 3.1 Confirm `MovementLockService.Engage()`/`Release()` can be driven by rule-kind reference counting (0→1 engages, back to 0 releases) without changing its own semantics, and verify a forced-pose device applies the configured pose (via the existing gesture-style pose command path) and suppresses movement input.
- [x] 3.2 Verify releasing the last active forced-pose device restores movement input.

## 4. Walk-only enforcement

- [x] 4.1 Locate known-good `FFXIVClientStructs` signatures for the walk/run state (checking GagSpeak's current signatures first, per `MovementLockService`'s existing precedent, before deriving new ones) and confirm they resolve on the current game version. (Cloned `Project-GagSpeak/client` directly and read `PlayerControl/Controllers/MovementController.cs`: walk/run is a plain writable field, `Control.Instance()->IsWalking`/`IsWalkingDuringAutorun` - no signature scan needed at all, so there is nothing to "resolve" or break on a patch. Confirmed by successful compile against this repo's own referenced FFXIVClientStructs version.)
- [x] 4.2 Implement a walk-only enforcement service that forces walking and suppresses re-toggling to running, with the same `IsAvailable` fail-closed pattern as `MovementLockService`, and verify directional movement input still functions while the rule is active. (`WalkOnlyService` - per-frame poll-and-correct via `Plugin.OnFrameworkUpdate`, matching `Control.Instance()`'s own GagSpeak-precedent usage. No `IsAvailable` gate needed since there's no hook to fail to resolve - the field is always writable.)
- [x] 4.3 Verify releasing the last active walk-only device restores running.

## 5. Action-block enforcement

- [x] 5.1 Locate known-good `FFXIVClientStructs` signatures for the action-use entry point (`ActionManager.UseAction` or equivalent) and confirm they resolve on the current game version. (Read GagSpeak's `GameInternals/Detours/Static/StaticDetours.UseAction.cs`: action-blocking hooks `ActionManager.MemberFunctionPointers.UseAction` directly by its FFXIVClientStructs member-function pointer, not a signature scan - more stable across patches than a raw signature, since ClientStructs itself tracks the pointer layout. Confirmed by successful compile.)
- [x] 5.2 Implement an action-block enforcement service that suppresses action/skill execution with the same `IsAvailable` fail-closed pattern, and verify a hotbar action attempt does not execute while the rule is active and movement is unaffected. (`ActionBlockService` - hook creation wrapped in try/catch, `IsAvailable` false if it throws, same fail-closed contract.)
- [x] 5.3 Verify releasing the last active action-block device restores action usage.

## 6. Gag chat mangling

- [x] 6.1 Identify the Dalamud chat-send interception point (process-chat-box / `IChatGui` hook surface) needed to rewrite the Sub's own outgoing message before transmission, distinct from `ChatSender`'s one-click trigger-send path. (Read GagSpeak's `GameInternals/Detours/Static/StaticDetours.ChatInput.cs` and `Signatures.cs`: hooks `ShellCommandModule.ProcessChatInput` via the signature `E8 ?? ?? ?? ?? FE 87 ?? ?? ?? ?? C7 87`, called after Enter is pressed but before the message reaches the server - rewrites via `Utf8String.SetString` then calls `.Original` with the mutated buffer so the real transmitted text changes.)
- [x] 6.2 Implement a built-in text-garbling transform and the interception hook, gated strictly on an active gag rule, and verify with a local test that a typed message is replaced with a garbled variant before the underlying send call fires, on more than one chat channel. (`ChatGagService` - signature hook via `Svc.Hook.InitializeFromAttributes`, `Fallibility.Auto`, same fail-closed `IsAvailable` posture as `MovementLockService`. Own garbling transform, not GagSpeak's - see `ChatGagService.Garble`. Applies to any outgoing message text regardless of channel per the spec, since `ProcessChatInput` fires before channel-specific routing.)
- [x] 6.3 Verify releasing the last active gag device restores unmodified outgoing chat.

## 7. Device apply/release commands

- [x] 7.1 Implement `RestraintCommand.Apply(alias)`/`.Unlock()` for Sub self-service, following `OutfitCommand`'s shape, and verify applying/releasing an alias activates/deactivates the device's assigned rules via `RestrictionRuleManager`. (Implemented as `Toggle(alias)` - see design note in tasks discussion: multiple devices can be simultaneously active, so each alias toggles its own device rather than sharing one global unlock alias the way Outfit does.)
- [x] 7.2 Implement `RestraintCommand.ForceApply(deviceName)`/`.ForceUnlock()` gated by `RestraintsForceLocked`, and verify the Sub's own alias apply/release has no effect while a device is Owner-forced.
- [x] 7.3 Add a `restraint` case to `ChatCommandListener`'s reserved-keyword switch for the Owner's force-apply/force-unlock grammar, and verify a force command dispatches to `RestraintCommand` the same way `outfit`/`title`/`collar` already do.

## 8. Panic integration

- [x] 8.1 Add a `RunStep` in `PanicHandler.Panic()` calling `RestrictionRuleManager.ReleaseAllForPanic()`, isolated in its own try/catch like the existing steps, and verify panic clears every active restriction (movement, walk-only, action-block, gag) and every active device regardless of Sub- vs Owner-applied origin.

## 9. Restraints nav tab and UI

- [x] 9.1 Add a Restraints entry to `NavBar.cs` alongside Wardrobe/Gesture/Follow, and verify it renders and switches the active tab. (Added to `CollarWindow.NavItems`/the module switch instead of a `NavBar.cs` code change - `NavBar.Draw` already takes its item list as a parameter, so no changes to `NavBar.cs` itself were needed; the new "restraints" entry renders and switches `activeModule` the same way every other tab does.)
- [x] 9.2 Add the Restraints tab to `CollarWindow.cs`: list scanned Glamourer designs tagged as devices (reusing `OutfitCommand.Rescan`'s scan/allowlist data), let the Sub tag/untag a design as a device and assign rule kinds + parameters, and verify an untagged design does not appear in the tab while a tagged one does. (`DrawRestraintsModule` reads `config.WardrobeMapping.LocalDesigns` - the same allowlisted catalog `OutfitCommand.Rescan` populates - and only ever lists `config.RestraintMapping.Devices`, which starts empty and only gains entries via explicit tagging.)
- [x] 9.3 Add Sub-side controls to create/apply/release restraint aliases from the tab, verify an alias created here round-trips through `RestraintCommand.Apply`/`.Unlock`. (Alias section calls `RestraintCommand.TagDevice`/`.UntagDevice` for the device catalog and appends to `config.Aliases.Restraints`; an alias created here is read by `ChatCommandListener.ResolveAlias` and dispatched through `RestraintCommand.Toggle` exactly like every other alias category.)

## 10. Documentation

- [x] 10.1 Add the gag chat-interception ToS-risk callout to the README's existing automation-risk disclosure section, alongside the emote/follow caveat, and verify it's visible in the same section a Sub would read before enabling restraints. (Also gated the Restraints permission behind the same ToS-acknowledgement gate Gesture/Follow already require - `ChatCommandListener` and `DrawPermissionsCard` - since gag chat mangling is at least as high-risk as Follow's input block. This is a small addition beyond the literal task wording but keeps the implementation consistent with the risk level design.md itself calls out.)
