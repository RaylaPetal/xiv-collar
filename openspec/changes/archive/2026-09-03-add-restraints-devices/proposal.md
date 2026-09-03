## Why

Collar and Outfit each apply a single design and lock the slots it touches, but neither one attaches *behavior* to what's worn - there's no way for a Glamourer design to mean "you can no longer run" or "you are now on the ground until released." GagSpeak proves this pattern works in this exact niche (restraint items that carry movement/action/chat restrictions), and `collar/slot-locking`'s own spec already names "Restraints" as a future category sharing its lock model. This change adds that category: a Restraints nav tab over the Sub's scanned Glamourer designs, where each design is tagged as a device carrying one or more restriction rules, with the same Sub-alias / Owner-force-apply tiers Outfit already has.

## What Changes

- New **Restraints** nav tab (alongside Wardrobe/Gesture/Follow) listing the Sub's scanned Glamourer designs filtered to those tagged as restraint devices, mirroring Outfit's wardrobe-scan-and-allowlist flow.
- Per-device rule assignment: each restraint device carries one or more restriction rules from a fixed set:
  - **Forced pose**: applies a specific pose (e.g. `/groundsit`) and fully suppresses movement input until released.
  - **Walk-only**: forces the Sub's walk/run state to walking and suppresses whatever re-enables running, without blocking movement input entirely.
  - **Action block**: suppresses action/skill (hotbar) execution until released.
  - **Gag chat mangling**: intercepts the Sub's own outgoing chat text before it is transmitted and replaces it with a muffled/nonsense variant, so the actually-sent message - not just the Sub's local display - is garbled.
- Device apply/release follows Outfit's two-tier model: the Sub can self-apply/release a device via their own aliases, and the Owner has a separate force-apply/force-release override that locks out the Sub's own controls while active.
- Multiple devices may be active at once as long as their rule sets don't conflict (e.g. two devices both claiming "action block" is fine; two devices both claiming conflicting pose targets is refused), reusing `collar/slot-locking`'s per-owner conflict model generalized to restriction rules rather than just equipment slots.
- Panic/safeword releases every active restriction rule alongside its existing Glamourer/title/follow reverts.

## Capabilities

### New Capabilities
- `collar/restraints`: restraint devices (Glamourer designs tagged with restriction rules), the Restraints nav tab, and the four restriction rule types (forced pose, walk-only, action block, gag chat mangling), including Sub-alias and Owner-force apply/release.

### Modified Capabilities
- (none - `collar/slot-locking`'s existing per-slot model is reused, not changed; restriction-rule conflict tracking is new territory `collar/restraints` owns itself)

## Impact

- New `CollarSystem.Plugin/Commands/RestraintCommand.cs` (device apply/release, mirroring `OutfitCommand.cs`).
- New `CollarSystem.Plugin/Safety/RestrictionRuleManager.cs` (tracks active rules per owner/device, conflict refusal, panic release - mirrors `SlotLockManager.cs`'s structure but for rules instead of equipment slots).
- New input-suppression services alongside `MovementLockService.cs`: a walk-only enforcer and an action/skill-use blocker, both requiring new `FFXIVClientStructs`/hook signatures (same risk tier as `MovementLockService`).
- New outgoing-chat interception mechanism for gag mangling - a genuinely new automation surface distinct from `collar/chat-transport`'s "no automated sending" command channel (that rule governs Owner→Sub trigger messages, not the Sub's own free-typed chat); needs its own explicit ToS-risk callout in design.md.
- `CollarSystem.Plugin/UI/NavBar.cs` gains a Restraints entry; `CollarSystem.Plugin/UI/CollarWindow.cs` gains the Restraints tab UI.
- `CollarSystem.Plugin/Config/PluginConfig.cs` gains restraint device/rule config and alias storage.
- `CollarSystem.Plugin/Safety/PanicHandler.cs` gains a release-all-restrictions call.
