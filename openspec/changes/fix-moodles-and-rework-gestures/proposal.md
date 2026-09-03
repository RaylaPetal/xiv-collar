## Why

Moodles discovery currently returns an empty catalog even when the local user has saved presets because the plugin invokes Moodles' zero-argument preset-list IPC with a character-name argument and models the result with an unverified object shape. Gesture discovery and playback likewise lose the animation option names exposed by Penumbra mods and permanently enable a whole mod before queuing a Sub confirmation, rather than following the working PoseKit flow the project already has available.

## What Changes

- Correct the Moodles integration to enumerate the local user's actual saved Moodles presets through the verified current IPC signature and payload, never through collar-owned preset data.
- Surface scan failures separately from a legitimate zero-preset result, and make the Moodles “Copy names” action export the names returned by the latest successful local scan.
- Replace the flat gesture mod/emote catalog with PoseKit-equivalent discovery of selected Penumbra mods, their animation groups/options, and each option's detected slash-emote or pose trigger.
- Show recognizable animation option names together with the gesture/pose that triggers them throughout Sub selection, clipboard sharing, Owner quick commands, and pending-command UI.
- Change gesture execution to the PoseKit sequence: apply the chosen mod and complete option selections as a scoped Penumbra temporary override for the Sub's effective collection, redraw the local player, then immediately play the tied gesture/pose.
- Remove the per-command Sub confirmation queue from gesture execution; the Sub's existing revocable Gesture permission and automation-risk acknowledgement remain the consent gates.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `collar/moodles`: Require reliable discovery and export of the Sub's own saved Moodles presets through the supported local preset-list IPC, including visible failure handling.
- `collar/gesture`: Replace generic changed-item discovery and confirmation-queued playback with PoseKit-equivalent named animation-option discovery and permission-gated temporary-mod activation followed by immediate trigger playback.

## Impact

- Affects `MoodlesIpc`, `MoodlesCommand`, Moodles scan feedback, and Moodles clipboard export/import behavior.
- Affects the gesture catalog/config model, Penumbra IPC wrapper, scanner and trigger-resolution logic, gesture command handler, Settings and Gesture/Owner UI, chat-command behavior, and persisted gesture aliases/quick commands.
- Reuses/adapts PoseKit's Penumbra manifest parsing, Lumina-backed animation lookup, pose-trigger handling, full-selection temporary settings payload, and redraw sequencing.
- Changes externally visible gesture consent behavior: an accepted Owner command plays immediately when Gesture permission is enabled instead of waiting for a separate confirmation click.
