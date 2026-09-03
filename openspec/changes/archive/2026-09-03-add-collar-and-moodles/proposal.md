## Why

The plugin has no persistent "collaring" moment - outfit is just another swappable, re-lockable alias like title or gesture, with nothing that marks the start of a contract the way a physically-applied collar does in the roleplay this plugin is modeled on. Separately, the plugin has no way to apply a visible status-effect ("debuff") to a Sub at all - Moodles is the established FFXIV plugin for that, and it isn't integrated.

## What Changes

- New **Collar** tab: a Sub captures whatever item is currently equipped in their own Neck slot as their configured collar item (no manual item-ID entry), gated behind its own new "Collar" permission toggle, off by default like every other category.
- **Pairing's Accept action gains a side effect**: if the accepting Sub has both a configured collar item and the "Collar" permission enabled, accepting a handshake applies that item to the Neck slot and locks it via Glamourer (`GlamourerIpc.SetItem`, a freshly generated key - same precedent as the existing outfit force-lock), immediately, as part of Accept.
- **The collar lock behaves exactly like the existing outfit force-lock**: refuses the Sub's own casual removal (Glamourer's own lock, and this plugin's alias/UI paths), but `/collarpanic` (the safeword) always releases it - no exception - and a new `collar unlock` reserved-keyword override command lets the Owner release it deliberately without panic, mirroring `outfit unlock`.
- New **Moodles** integration: a Sub's own saved Moodles presets are enumerated locally (same local-scan pattern as Gesture's Penumbra-mod scanning, via Moodles' own IPC), gated behind a new "Moodles" permission. The Owner applies or clears one by name through a new `moodle` reserved-keyword override command (`moodle apply <preset name>` / `moodle clear`) - applied immediately, with no Sub-confirmation gate (unlike Gesture), since a Moodle is a non-consequential status icon, not a real emote/animation being fired.
- New Permissions: **Collar** and **Moodles**, alongside the existing Title/Outfit/Gesture/Follow, same independent opt-in-per-category pattern.
- CollarWindow gains a **Collar** nav tab (capture/clear the collar item, same as Wardrobe/Gesture's own-config tabs) and a **Moodles** quick-command section in the Owner tab (mirrors Outfit/Gesture's "Add from clipboard" -> one-click Quick Commands, populated from the Sub's scanned preset names).

## Capabilities

### New Capabilities
- `collar/collaring`: the Sub's configured Neck-slot collar item, its auto-apply-and-lock at pairing Accept, and its release paths (panic, or the Owner's `collar unlock` override).
- `collar/moodles`: the Sub's locally-scanned Moodles preset catalog, its permission gate, and the Owner's `moodle` override command to apply/clear a preset by name.

### Modified Capabilities
- `collar/pairing`: the Accept action (see "Configured-identity pairing consent") gains a conditional side effect - applying and locking the Sub's configured collar, when one is configured and enabled - without changing the identity-consent requirements already in place.

## Impact

- New `CollarSystem.Plugin/Ipc/MoodlesIpc.cs` (wraps Moodles' own IPC - preset enumeration, apply-by-player, clear-by-player; exact call signatures verified against the installed Moodles plugin during implementation, not guessed).
- `GlamourerIpc` gains a "read the currently-equipped Neck item" query (via Glamourer's `GetState`), used only to capture the Sub's chosen collar item - no new write-side API beyond the already-wrapped `SetItem`.
- `ChatCommandListener`'s reserved-keyword override grammar gains two words: `collar` (`collar unlock`) and `moodle` (`moodle apply <name>` / `moodle clear`) - both validated the same way `title`/`outfit`/`gesture` already are (reserved from Sub alias names).
- `PluginConfig` gains a `CollarState` (configured item id/stains, lock key, force-locked flag - same shape as the existing outfit lock state) and a `MoodlesMapping` (scanned preset catalog, folder/name allowlist if Moodles' own organization supports one), plus two new `PermissionSet` fields (`Collar`, `Moodles`).
- `PairingCommand.AcceptPeer` (or the caller immediately after it) gains the conditional collar-apply side effect described above.
- `CollarWindow` gains a Collar nav tab and a Moodles Quick Command section (Owner tab); `SettingsWindow` gains the Moodles scan/allowlist card (mirrors the existing Wardrobe/Gesture scan cards) and the Collar capture control.
- `SubRuntimeState`/`PanicHandler` gain the collar lock key and its unconditional release on panic, mirroring `OutfitLockKey`/`OutfitForceLocked` exactly.
