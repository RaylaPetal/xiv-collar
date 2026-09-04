## Context

Today `AliasExportEntry` (`CatalogSyncService.cs`) carries only `{Alias, Description}` - a bare alias word plus a free-text human-readable summary (e.g. `"Outfit: Casual Blue"`). Import-time dedup (`ImportPlainNames`/`ImportAliasLines`/`ImportGestureLines`) only ever checks for an exact match on `QuickCommand.Command` *within the one category list being populated* - it has no notion of "this alias and that plain scanned name secretly point at the same thing," and no cross-category check at all. See proposal.md - Why.

The Owner never sees Sub-local ids for Outfit or Moodles - both `OutfitCommand.ForceApply`/`MoodlesCommand.ForceApply` match a Sub-authored override by **name**, case-insensitively, against the Sub's own catalog (`WardrobeMapping.LocalDesigns` / `MoodlesMapping.LocalCatalog`), which is exactly how the plain Wardrobe/Moodles scan sections are already exported (name-only, one per line). Gesture is the one category where the Owner-facing grammar already carries a real catalog id (`gesture <id>`, from `GestureCommand.TryParseExport`'s `entry.Id`), and `GestureAliasDefinition.GestureId` already references that same id space on the Sub's side.

## Goals / Non-Goals

**Goals:**
- Let import-time dedup recognize "this alias and that plain scanned name are the same target" for Outfit, Gesture, and Moodles, without inventing new Sub-side identity plumbing beyond what already exists.
- Extend the existing "don't duplicate a command already present" check from per-category to global, since a shared alias word always sends identical wire text regardless of which category list it lives in.
- Keep Title, Restraints, and Custom Trigger Bundles exactly as they are today - none of them have a meaningful shared "target" to match on, and forcing one would either do nothing useful (Title) or actively block legitimate configurations (a Custom Trigger Bundle sharing an action with something else is normal, not a duplicate).

**Non-Goals:**
- Not introducing design ids into the plain Wardrobe/Moodles export sections - those stay name-only, matching every other name-based Owner-facing override grammar (`outfit lock <name>`, `moodle apply <name>`) unchanged.
- Not deduplicating *within* a Custom Trigger Bundle's own bundled actions, or across bundles - a bundle overlapping another entry's action is normal and explicitly out of scope (proposal.md's "What Changes").
- Not attempting to resolve a same-target duplicate by merging metadata (e.g. combining two labels) - one entry is kept as-is, the other is simply not added.

## Decisions

### Target identity is design/status name for Outfit and Moodles, gesture id for Gesture
For Outfit and Moodles, the shared identity available to the Owner is the case-insensitive design/status **name** - the same value both the plain scan export and an alias's own `DesignName`/`StatusName` already carry, and the same value `ForceApply` already matches by. `AliasExportEntry` gains a `Target` field, populated with that name for a single-action Outfit/Moodle alias (Moodles' `Target` uses `MoodlesTextFormat.StripMarkup`, matching how its description is already stripped for display). For Gesture, `Target` is populated with the alias's `GestureId` - the same id `GestureCommand.TryParseExport` already emits for the plain scan section's `gesture <id>` command, so the two are directly comparable without any text-matching heuristics.

**Alternative considered**: export `OutfitAliasDefinition.DesignId`/parse a Guid identity for Outfit too, and extend the plain Wardrobe export section to also carry design ids. Rejected - it changes an existing, stable export section's line format for every consumer (including anyone who's automated around today's plain-name-per-line Wardrobe section), to solve a problem name-matching already solves just as reliably, since `ForceApply` itself only ever matches by name.

### Same-target dedup runs per category, keyed by category-appropriate identity
Within each of Outfit/Gesture/Moodle's import routine, before adding a new entry, the importer checks the target already-imported/existing entries in *that category's* `QuickCommand` list carry - for Outfit/Moodles that means re-deriving each existing entry's name from its stored `Label`/`Command` (see below), for Gesture it means comparing against `QuickCommand.Command`'s embedded id directly (already structured, no re-derivation needed).

Outfit and Moodles need a way to read an *existing* `QuickCommand`'s target name back out for comparison, since `QuickCommand` doesn't currently store one separately from `Label`/`Command`. Rather than parse `Command` (`outfit lock "<name>"` vs. a bare alias word - two different shapes for the same target), `QuickCommand` gains an optional `Target` field (nullable string), populated by every import path that has a name available (`ImportPlainNames` for the plain scan, `ImportAliasLines` for a single-action alias) - never by manual entry, which naturally never collides with a scanned name until re-imported. Same-target dedup for Outfit/Moodles then compares incoming `Target` (case-insensitive) against every existing entry's `Target` in that category, not against `Command`.

**Alternative considered**: derive the target name from `Command` at comparison time via string parsing (`outfit lock "..."` vs. a bare word needing a live alias-to-name lookup). Rejected - fragile (breaks if the override grammar's quoting/format ever changes) and can't compare an alias-word command against a plain-name command without re-resolving the alias, which the Owner's client has no way to do (it never received the alias's target, only Sub-authored text) prior to this change adding `Target` to the export format.

### Cross-category same-command dedup is a single pass over every category's commands
Before any category-specific import runs, or as a shared post-check per entry, the importer maintains a set of every command string already present across `Titles/Outfits/Gestures/Moodles/Restraints/Aliases` (case-insensitive) and skips adding any new entry whose command is already in that set - regardless of which category it would land in. This subsumes each category's own existing same-category check (a command already in its own category's list is trivially also in the global set).

**Alternative considered**: only check cross-category when a `Custom Trigger Bundle`'s "one-off" nature is involved. Rejected - the ambiguity is symmetric (Title `"test"` vs. Outfit `"test"` is exactly as broken as two Outfit `"test"`s), so the check should be unconditional and category-agnostic, matching proposal.md's "more importantly" framing.

### Import summary gains a duplicate-skipped count
`CatalogImportResult` gains a `Duplicates` count, incremented whenever either dedup path (same-target or cross-category same-command) skips an entry that would otherwise have been added. `TotalAdded` stays additions-only; the Owner tab's result text gets one appended clause (e.g. `", 2 duplicate(s) skipped"`) rather than per-entry detail, per the user's chosen "skip silently, report count" resolution - keeping the existing result-text shape rather than growing a itemized list in limited UI space.

## Risks / Trade-offs

- [Two designs/statuses that are genuinely different but happen to share a name] → Accepted: name collisions are already how `ForceApply`/the plain-name grammar identify a target at all - two same-named-but-different designs are already ambiguous today at send time, not a new risk this change introduces.
- [`QuickCommand.Target` adds a field only Outfit/Gesture/Moodle populate] → Low risk: nullable, defaults to absent for every existing/manually-added entry, and only ever compared within its own category's dedup pass.
- [Cross-category dedup could silently swallow an intentional same-word setup] → Accepted per the user's explicit ask: two entries sharing one command are indistinguishable at send time regardless of intent, so silently keeping only one is strictly less confusing than keeping both.

## Migration Plan

- Existing saved `QuickCommand` entries deserialize with `Target` absent/null, so they're simply never matched by same-target dedup until the next import re-populates a `Target` for them (or a fresh import compares a new entry against them and finds no `Target` to match, meaning today's exact-`Command` check is still the only thing protecting those older entries - consistent with "no forced migration" already established for `ImportSource`).
- No wire-format or persisted-file version bump needed - `AliasExportEntry`'s new `Target` field is additive to the existing base64-encoded JSON payload; an older exported file simply produces entries with no `Target`, which fall back to cross-category same-command matching only (today's behavior), not an error.
