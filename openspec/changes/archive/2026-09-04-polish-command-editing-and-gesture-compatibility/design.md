## Context

See `proposal.md` for motivation. Gesture dispatch accepts a raw argument and has both stable-ID and readable-selector concepts, but the recent presentation migration did not establish one normalized compatibility boundary with stage-specific results. The UI is predominantly creation forms plus immutable rows; Custom Trigger summaries are generated as lowercase strings and joined with commas. The Settings test control should be a normal editable text buffer, but its current wrapper/flags need auditing against clipboard behavior. Bound restraint animation selection currently inherits Gesture's playable-trigger filter, even though an idle replacement may only need its mod options enabled; release removes temporary settings without guaranteeing a redraw afterward.

## Goals / Non-Goals

**Goals:**

- Centralize gesture selector decoding and return structured resolution/playback failures.
- Introduce reusable draft editors that clone persisted values and commit atomically on Save.
- Establish shared presentation helpers for category/action labels, summary badges, warnings, and row actions.
- Preserve stable IDs, import provenance, favorites, and alias references across edits.
- Support both triggered and enable-only idle animations throughout restraint selection, serialization, activation, and release.

**Non-Goals:**

- Redesigning the command wire protocol again or removing legacy ID support.
- Changing pairing, permissions, consent acknowledgements, or direct-send rules.
- Replacing the entire visual theme or introducing a new UI framework.
- Automatically rewriting all user labels; friendly formatting applies to system-owned category/action text.

## Decisions

### Normalize gesture input once and return a typed result

Route quoted readable selectors and unquoted legacy IDs through one decoder before lookup. Resolution returns success, missing, or ambiguous; execution then reports temporary-settings, redraw, and trigger failures separately. The listener and local test consume the same result, preventing one path from silently diverging. Retaining the opaque-ID branch is required for imported/saved compatibility.

Alternative considered: special-case the reported ID in the listener. That would fix one syntax while leaving future formats and diagnostics fragmented.

### Edit cloned drafts in a reusable focused editor

Opening Edit copies the entry into a category-specific draft keyed by stable entry identity. Changes remain isolated until validation passes and Save atomically copies them back; Cancel discards the draft. Simple categories may share a compact modal shell, while restraint and Custom Trigger editors reuse their existing picker/action controls within a larger focused popup.

For Sub restraint devices, preserve the device ID so aliases remain connected. For Owner quick commands, preserve `Source`, favorite state, and imported metadata unless the selected target itself changes. Custom Trigger action order is explicit and supports move-up/down or drag reordering.

Alternative considered: mutate live objects inline. That makes Cancel unreliable, saves partial invalid states, and makes nested action editing difficult to reason about.

### Separate presentation models from command serialization

Create shared display descriptors for categories, action kinds, restraint rules, targets, status, and optional details. Rows render those descriptors as compact badges/groups and tooltips or expansion detail; serializers continue using stable IDs and exact wire tokens. This prevents visual cleanup from changing command meaning, repeating the selector regression, or leaking protocol syntax.

Alternative considered: improve each existing interpolation independently. That would preserve inconsistent names and make future modules repeat the same formatting work.

### Preserve user-entered casing while owning system vocabulary

System labels use title-style friendly names (`Body Cuffed`, `Gagged`, `Moodle`), while user-defined aliases, labels, and catalog names display exactly as entered after existing markup sanitization. The UI may truncate visually but retains full text in detail/tooltips and stored data.

### Keep the command-test box a standard multiline-capable text input

Audit wrapper flags and focus handling so the control does not intercept clipboard shortcuts or overwrite its backing buffer during editing. Running remains a separate explicit action and uses the same buffer, validation, and no-send behavior as today.

### Give restraint animation selections an explicit playback mode

Do not equate “valid restraint animation” with “has a detected Gesture trigger.” The restraint picker receives a mode that includes catalog entries with a concrete mod directory and option selection even when `Trigger` is absent. Persist the same stable animation identity and readable metadata, plus enough derived or explicit mode information to distinguish `Triggered` from `EnableOnly` without inventing a fake ground-sit trigger. Legacy selections infer triggered behavior when a trigger resolves and otherwise may resolve as enable-only when their stable catalog entry is valid.

Applying either mode sets temporary Penumbra settings and redraws. Only `Triggered` schedules playback after redraw; `EnableOnly` stops there and lets the replacement idle take effect naturally. This keeps Gesture's own picker and commands restricted to genuinely playable entries while widening only restraint-mode selection.

Alternative considered: add a “do nothing” pose trigger. That misrepresents the animation, pollutes visible labels, and risks later code treating it as a real gesture.

### Treat post-removal redraw as part of bound-animation release

After removing one or more temporary bound-animation settings, issue one coalesced local-player redraw once the batch is complete. Per-device release, Owner force-unlock, panic, and rollback use the same release helper. Bookkeeping is cleared even if redraw fails, but the failure is logged and surfaced where a command result is available; a failed redraw must not leave the gag, movement, action, or slot teardown blocked.

Alternative considered: redraw after each removed rule. Batching avoids repeated character rebuilds when Arms, Legs, and Full Body settings release together.

## Risks / Trade-offs

- [Editing imported entries can sever stable identity from readable metadata] → Update target identity and presentation metadata together, and cover rename-only versus target-changing edits separately.
- [Modal drafts become stale if imports reset while open] → Key drafts by stable entry identity and reject Save if the source entry disappeared.
- [Dense badge layouts wrap poorly at minimum width] → Use measured wrapping plus a compact summary/detail expansion, and test minimum supported widths.
- [Gesture error detail leaks opaque IDs into normal UI] → Keep technical identity in logs/diagnostic expansion while user-facing errors name the readable target where available.
- [Legacy config lacks newer metadata] → Populate drafts defensively, allow compatible fields to remain absent, and only migrate after a successful validated save.
- [Triggerless catalog entries may not contain a meaningful option selection] → Offer only entries with a resolvable mod directory and concrete selection state, and reject stale/incomplete identities at apply time.
- [Several bound rules release together and cause redraw churn] → Remove all applicable temporary settings first and coalesce the release into one redraw.

## Migration Plan

1. Add non-destructive presentation/editor state around existing persisted models.
2. Load legacy quick commands and triggers unchanged; synthesize missing display descriptors at render time.
3. Persist new/edited entries through existing configuration serialization, retaining stable keys and provenance.
4. Keep old gesture IDs and existing command strings accepted throughout rollout and rollback.
5. Infer restraint playback mode for legacy selections at resolution time; no destructive saved-config migration is required.
