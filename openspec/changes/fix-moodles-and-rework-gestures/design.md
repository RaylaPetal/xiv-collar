## Context

See `proposal.md` for motivation. The installed Moodles 1.1.3.5 runtime reports that `Moodles.GetPresetsInfoListV2` has zero inputs, while the collar currently subscribes with a character-name input and consequently logs `IpcLengthMismatchError: 1 != 0`. Its locally declared result DTO is also explicitly unverified. The scan catches that failure and returns an empty list, making a broken call indistinguishable from a real empty library.

The current gesture implementation asks Penumbra for whole-mod changed-item labels, flattens results to mod/emote pairs, permanently enables a mod with `TrySetMod`, and queues a confirmation. The sibling `../plugin-test/PoseKit` implementation preserves the useful identity stored in Penumbra's `default_mod.json` and `group_*.json`: mod, group, option name, complete selections, and one or more triggers. It applies that state through `SetTemporaryModSettings`, redraws the player, and only then invokes the tied command or pose.

## Goals / Non-Goals

**Goals:**

- Make Moodles scanning accurately reflect the local user's saved presets and expose actionable failures.
- Port the proven PoseKit animation discovery and playback semantics rather than maintaining a second approximate scanner.
- Carry a stable, human-readable animation identity from Sub scan through clipboard transfer and Owner command back to Sub execution.
- Keep all Penumbra writes temporary and scoped to this plugin's source/session.

**Non-Goals:**

- Reading Moodle presets from the Owner, bundled defaults, or any remote service.
- Copying PoseKit's offset editor, preset library, emote synchronization, or animation-conflict UI.
- Installing animation mods or transferring mod files between users.
- Changing pairing, chat transport, or the existing Gesture/Moodles permission model.

## Decisions

### Use Moodles' zero-input preset catalog and real tuple payload

Subscribe to `GetPresetsInfoListV2` with no input argument and model the exact tuple returned by the installed/current Moodles IPC, extracting its preset GUID and display title. Confirm apply/clear delegate shapes against the same IPC source while touching this wrapper, since the existing declarations were all marked best-effort. Return a result type that separates success-with-items, success-empty, unavailable, and invocation/shape failure; update the saved catalog only on successful scans so a transient IPC outage does not silently erase the last known names.

Passing the local character name was rejected because preset enumeration is library-wide and the runtime proves the endpoint accepts no inputs. Scanning collar presets was rejected because Moodles remains the source of truth.

### Port PoseKit's option-aware scanner as shared behavior

Adapt PoseKit's scanner, animation reverse index, and trigger heuristics into the collar codebase with the same manifest interpretation:

- read only explicitly selected installed mods;
- parse top-level `default_mod.json` redirects as a synthetic, non-configurable Default option;
- parse each `group_*.json` as a single- or multi-select group while preserving group and option display names;
- reject malformed/non-game redirect keys without failing the entire scan;
- derive slash commands from explicit `(/command)` hints and Lumina Emote/ActionTimeline reverse lookup;
- derive supported sit, ground-sit, and doze pose identifiers from their redirected animation paths.

The collar will adopt PoseKit's explicit per-mod selection, with Penumbra sort-folder and text filters used only to make selection manageable. Retaining a folder prefix as the sole persisted allowlist was rejected because it does not reproduce PoseKit's deliberate per-mod scan set and can unexpectedly include newly installed mods.

### Persist a structured animation command identity

Replace the flat `mod directory + mod name + emote name` entry with a structured record containing the mod identity, animation option display name, complete group selections required to reproduce it, and exactly one selected trigger identity per commandable entry. If one option exposes multiple triggers, it produces multiple command entries with the same option name and distinct trigger labels.

Clipboard exchange must use a versioned, line-safe representation that carries this structured identity while rendering a friendly label such as `Mod — Animation Option — /highfive` or `Mod — Animation Option — Ground Sit Pose 2`. Import should continue to reject malformed entries visibly. Plain old name-only imports cannot encode option selections; they should not be guessed.

The main Gesture tab keeps alias naming and saved aliases concise. Its “Add animation” action opens a dedicated picker window modeled on PoseKit's Animation Library: a toolbar with search/rescan, one collapsible section per mod, nested group sections where useful, and rows labeled with the animation option plus each tied trigger. Selecting a row returns it to the alias form and closes the picker. This keeps the full Penumbra hierarchy visible without squeezing it into the collar card.

### Execute enable → redraw → trigger atomically from the command path

After chat authentication and Gesture permission checks, resolve the structured entry against the Sub's current scanned catalog. Obtain the effective local-player collection, call Penumbra's temporary-settings API with `inherit: false`, `enabled: true`, all real group selections, and a collar-specific source tag, then redraw object index 0. Only after those steps succeed, invoke either the slash-emote command or PoseKit-equivalent supported pose trigger.

Permanent `TrySetMod` writes are rejected because a remote command must not rewrite the Sub's saved Penumbra configuration. Playing after a failed activation is rejected because it can visibly run a different animation than the Owner selected. The confirmation queue is removed because permission plus acknowledgement now authorizes immediate execution.

### Treat legacy gesture data as best-effort migration input

On configuration load or the first new scan, map an old alias only when its mod and emote uniquely identify one new option/trigger entry. Drop no data silently: mark ambiguous/unmatched aliases invalid in the UI and require recreation. Owner name-only quick commands likewise need fresh structured import because their text lacks the option selections required for deterministic playback.

## Risks / Trade-offs

- [Directly reading Penumbra mod manifests couples discovery to their on-disk schema] → Isolate DTOs/parsing, tolerate unknown fields, skip only malformed files/options, and cover real PoseKit fixture shapes.
- [A mod can change or disappear after clipboard sharing] → Resolve commands against the current local catalog and fail visibly without playing when identity/selections are stale.
- [Temporary overrides can collide with another plugin's overrides] → Use a unique source tag, send the complete selection map, and document ownership; never fall back to permanent writes.
- [Redraw may be visually disruptive] → Redraw only after a successful settings change and immediately before the trigger, matching the known working PoseKit order.
- [Immediate chat/pose automation increases ToS sensitivity] → Preserve the existing explicit acknowledgement and live Gesture permission gate, and update UI/README language so the behavior is unambiguous.
- [Existing gesture aliases and Owner quick commands may not migrate] → Perform only unique mappings and clearly prompt for rescan/re-export/re-import when deterministic conversion is impossible.

## Migration Plan

1. Introduce the new catalog/trigger model and a configuration migration for uniquely resolvable legacy aliases.
2. Replace Moodle scan transport/result handling and validate against the installed Moodles version with zero, one, and multiple local presets.
3. Switch scan and UI paths to the option-aware catalog, then switch command serialization/import to the structured form.
4. Replace queued gesture execution with temporary activation, redraw, and immediate trigger; remove obsolete queue UI/state.
5. Update user-facing guidance and run build plus targeted scanner, migration, clipboard, permission, and execution tests.

Rollback is a code/config rollback. Temporary Penumbra settings are session/source-scoped; no permanent mod settings require repair. Preserve a pre-migration configuration backup or tolerate ignored new fields when rolling back.
