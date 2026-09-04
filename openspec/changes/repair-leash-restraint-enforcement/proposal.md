## Why

Leash and restraint commands currently report success without reliably enforcing their promised gameplay effects: a leashed Sub is neither made to follow nor fully prevented from breaking movement, and forced-pose, full-body, walk-only, and action-block rules have enforcement gaps. Owner command tells also expose opaque identifiers and raw Moodles markup, while the Owner's restraint animation picker reads the Owner's local mods instead of the Sub-exported library.

## What Changes

- Make leash engage start and maintain follow of the paired Owner while suppressing every player-controlled movement path that could cancel or override it; release remains immediate on unleash, panic, or unpair.
- Replace the incomplete shared movement blocker with a capability-checked, continuously asserted enforcement layer informed by GagSpeak's input detours, mouse-movement interception, unfollow protection, and full-freeze state.
- Make forced pose and Full Body Cuffed genuinely immobilize the Sub, make Walk Only continuously prevent running, and make Action Block reliably reject supported skill/action invocation paths.
- Refuse activation and surface a specific diagnostic when any required enforcement hook or target state is unavailable, instead of displaying success for an unenforced restriction.
- Use human-readable command selectors and sanitized labels in composed tells so gestures do not expose opaque numeric/hash-like IDs and Moodles do not expose `[color]`/`[glow]` markup, while retaining deterministic receiver-side resolution and compatibility with existing saved/imported commands.
- Populate Owner restraint animation choices exclusively from the paired Sub's imported gesture library, preserve the Sub's stable animation identity in the restraint payload, and clearly handle missing or stale imports.
- Add focused runtime and parser tests for keyboard, mouse, autorun/follow cancellation, walk/run state, action use, command rendering, and Owner/Sub catalog separation.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `collar/follow`: Leash now establishes actual follow, covers all relevant manual movement paths, prevents unfollow, and fails visibly when enforcement cannot be guaranteed.
- `collar/restraints`: Movement, walk-only, action-block, and pose enforcement become continuous and fail-closed; Owner animation selection uses the Sub-shared library.
- `collar/gesture`: Gesture command references gain a readable, deterministic selector with legacy identifier compatibility.
- `collar/moodles`: Outbound Moodle command text uses a markup-free human-readable status name without weakening exact local resolution.
- `collar/catalog-sync`: Imported Sub gesture metadata becomes the authoritative Owner-side source for restraint animation selection and readable command construction.
- `collar/chat-transport`: Reserved command tells become human-readable while remaining unambiguous, locally diagnosable, and compatible with previously saved payloads.

## Impact

Affected areas include low-level input/action hooks and per-frame enforcement, follow target lifecycle, restraint rule activation and rollback, chat command parsing/composition, quick-command migration, catalog export/import models, and the Owner restraint UI. The design will reuse concepts verified in the upstream Project-GagSpeak client but keep Oathbound's consent gates, panic teardown, local tell transport, and no-background-send policy intact. Native hook signatures and game-structure offsets are patch-sensitive and require explicit availability checks plus regression tests.
