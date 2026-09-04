## Context

`CollarCommand`/`SlotLockManager` (see `collar/slot-locking`) already give the collar's Neck-slot item a real enforcement loop: `SlotLockManager` subscribes to `GlamourerIpc.LocalPlayerStateChanged` and reapplies a locked slot the instant Glamourer reports it changed. Moodles' own exposed IPC surface (`MoodlesIpc.cs`) has no equivalent change-notification event wired up in this codebase today - only `GetRegisteredMoodlesV2` (catalog), `AddOrUpdateMoodleByPlayerV2` (apply), and `ClearStatusManagerByPlayerV2` (clear all). Confirmed via `strings` against the installed `Moodles.dll` that the Moodles plugin internally uses `[color=N]`, `[glow=N]`, and `[i]` as its only markup tags (found its own tag-matching regex embedded in the binary), and that its IPC surface also exposes `GetStatusManagerByPlayerV2`/`GetActiveStatusInfo`-shaped members beyond what `MoodlesIpc.cs` currently wraps - but wrapping either of those without a documented signature would be guesswork, and the proposal's actual requirement (the status comes back within a short window) doesn't need them.

See proposal.md - Why/What Changes for full motivation. The Moodles-formatting decision (strip tags, don't attempt to reproduce colors) was confirmed directly with the user rather than assumed.

## Goals / Non-Goals

**Goals:**
- The collar's assigned Moodle follows the exact same lock/apply/release lifecycle the Neck-slot item already has - one mental model, not two parallel lock systems.
- Markup-stripping is one shared helper applied at every existing display site, not reimplemented per call site.
- The Aliases export fix is additive to the existing `ExportAliasNames`/import path - no change to the file format's section structure, just what feeds the existing Aliases section.

**Non-Goals:**
- No true tamper-detection via a Moodles change-notification IPC event - re-assertion is a periodic timer, not an instant reaction to the status being removed. A brief window (see Decisions) where the status is visibly absent before it returns is accepted.
- No attempt to reproduce Moodles' actual `[color=N]`/`[glow=N]` visual styling - confirmed with the user as out of scope; tags are stripped, not rendered.
- No change to how a Moodle applies when triggered as an ordinary alias/Custom-Trigger/Owner-override action (`collar/moodles`, `collar/custom-triggers`) - this change only adds the collar's own always-on assignment as a new, separate application path.

## Decisions

**The collar-assigned Moodle re-asserts on a periodic `IFramework.Update`-driven timer, not a Moodles change-notification event.** `SlotLockManager`'s instant reassertion works because `GlamourerIpc` already exposes a real `LocalPlayerStateChanged` event this codebase actively subscribes to; no equivalent is currently wired for Moodles, and guessing at an unverified IPC event's exact delegate signature risks a runtime binding failure with no compile-time signal. A timer that calls the existing `MoodlesIpc.ApplyStatus`/`MoodlesCommand`'s apply path every ~10 seconds while the collar lock is active is simple, uses only the IPC surface already proven to work, and satisfies the spec's "returns within a short, bounded interval" wording exactly. `Moodles.AddOrUpdateMoodleByPlayerV2` re-applying a status that's already active is expected to be a harmless no-op/refresh, matching how this plugin already re-applies gestures/outfits idempotently elsewhere.

**The collar's Moodle lifecycle is 1:1 with the Neck-slot lock's own lifecycle - no separate release command.** The proposal's "not allow removal unless panic or ending contract" reduces cleanly to "exactly when the collar's own lock would release": panic, the Owner's `collar unlock`, and the Owner's `collar lock` re-apply path (which resumes it). This avoids introducing a second lock/unlock surface for the Owner or Sub to reason about - the existing collar release/apply commands already fully own this.

**Markup stripping lives as one static helper, called at read time, not at scan time.** Stripping when the Moodles catalog is scanned (once) would be simpler, but this plugin's `Moodles.ExportNames()`/export-format needs the exact status name Moodles reports for the Owner to type back exactly what the Sub told them (name-based lookup, e.g. `moodle apply <name>` / `ForceApply(name)`) - stripping at scan time would silently change what "the exact name" means and could break that lookup if Moodles' own name still round-trips with markup internally. Stripping only at UI-display call sites keeps the underlying stored/matched name untouched.

**The Aliases export fix reuses the existing `ExportAliasNames` method - no new export section.** Moodles and Custom Trigger alias words are just two more `.Select(a => a.Alias).Concat(...)` calls added to the same LINQ pipeline; the file format, the "## ALIASES" header, and the import path (`ImportPlainNames` into `QuickCommands.Aliases`) are all already category-agnostic (they only ever handle bare words).

## Risks / Trade-offs

- **A brief window exists where a manually-removed collar Moodle is visibly absent before the periodic timer re-applies it.** → Accepted per the "short, bounded interval" spec wording rather than "instant" - a ~10s interval is a reasonable balance between responsiveness and avoiding a tight per-frame IPC call; not a security boundary, just a persistent-marker UX guarantee.
- **This adds a second periodic-timer mechanism to the codebase** (alongside Gesture's own ~30s idle-decay timer), rather than reusing one shared scheduler. → Both are small, independent, and already conceptually separate (one decays, one reasserts); introducing a shared scheduler abstraction for two call sites would be over-engineering for this change's scope.
- **Stripping happens at every display call site rather than once centrally in the catalog.** → Deliberate (see Decisions) - the trade-off is a handful of call sites each calling one shared static helper, which is a small, explicit cost compared to risking the export/lookup-by-name path silently diverging from what Moodles itself reports.
