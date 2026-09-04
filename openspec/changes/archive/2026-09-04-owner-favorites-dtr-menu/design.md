## Context

Confirmed by inspecting Aetherphone's own installed DLL in this environment (`strings`/`monodis` against `Aetherphone.dll`) that it references `Dalamud.Plugin.Services.IDtrBar`/`Dalamud.Game.Gui.Dtr.IDtrBarEntry` directly - the officially-supported Dalamud API for adding an entry to FFXIV's own server info bar, not raw `AtkResNode` manipulation. Confirmed via Dalamud's own shipped XML doc comments (`Dalamud.xml`) that `IDtrBarEntry` exposes exactly what's needed: `Text` (SeString), `Tooltip` (SeString?), `Shown` (bool), and `OnClick` (an `Action`, settable) - a click handler is a first-class part of the public API, no need to hook or poll anything. `IDtrBar.Get(string title, SeString? text)` creates/returns the entry; `IDtrBar.Remove(string title)` (or the entry's own `Remove()`) tears it down.

Six of the seven Owner quick-command categories already share one row-drawing method, `CollarWindow.DrawSavedQuickRow(QuickCommand cmd, List<QuickCommand> list, bool canSend)` (used by Title, Outfit, Gesture, Follow, Moodles, and Aliases - confirmed by reading every category's section method). Only Restraints uses its own bespoke `DrawRestraintQuickRow` (it needs the per-entry rule-editor expansion `DrawSavedQuickRow` doesn't have). This means a star-toggle addition to `DrawSavedQuickRow` covers six categories in one place; Restraints needs the same addition made once, separately, in its own row method. The Owner's ad-hoc restraint device section (`DrawAdHocRestraintSection`) has no persisted list entry to favorite at all - it composes and sends immediately - so it's out of scope by construction, not by omission.

`CollarWindow`'s tab selection (`activeModule`, a private field) and `Plugin.ToggleMainUi()` (private, wired to Dalamud's own `OpenMainUi`/`/collar` command) are the existing patterns for "make the main window show a specific thing" - there's no public API today for "open the main window to a specific tab," since nothing outside `CollarWindow` itself has needed one before now.

See proposal.md - Why/What Changes for the full grounding and the recorded scope decision on why this is a toggleable window, not a true auto-dismissing dropdown.

## Goals / Non-Goals

**Goals:**
- The DTR entry uses Dalamud's own supported API end to end - no raw Atk/AtkResNode work anywhere.
- Favoriting reuses the existing `QuickCommand` model and existing row-drawing methods wherever possible - one new bool field, not a parallel data structure.
- The favorites window follows this codebase's existing `Window` pattern exactly (like `ItemPickerWindow`/`AnimationPickerWindow`) - no new UI framework or pattern introduced.

**Non-Goals:**
- True native dropdown behavior (auto-dismiss on outside click, anchored precisely under the DTR entry's live screen position). Dalamud's `Window` system doesn't provide this, and building a raw `ImGui.BeginPopup`-driven transient popup triggered from a DTR `OnClick` (which fires outside the normal per-frame `Window.Draw` callback) would be new, more fragile territory this codebase has no precedent for. The favorites window is a small toggleable window instead - explicitly called out in the proposal as a deliberate trade-off.
- A management UI for favorites beyond the per-row toggle (no reordering, no renaming, no folders) - it's a flat favorited/not-favorited flag.
- Any change to how quick commands are created, sent, or removed in their own category lists - favoriting is purely additive alongside those existing controls.

## Decisions

**`QuickCommand` gains one `IsFavorite` bool**, not a separate favorites list. A `List<QuickCommand>` living in a new config collection would need its own add/remove/sync logic to stay consistent with the source category list (what happens on Remove from the category list? Rename? Re-import?) - a flag on the existing object has none of that: removing the command removes it from everywhere it's referenced, favoriting/unfavoriting is a one-line toggle, and the favorites window is just `AllQuickCommandLists().Where(c => c.IsFavorite)`.

**The favorites window reads all seven lists fresh on every draw**, not a cached/maintained subset. `Plugin.Configuration.QuickCommands.Titles/Outfits/Gestures/Follow/Moodles/Restraints/Aliases` are all small in-memory lists already iterated fully elsewhere (e.g. every quick-command section redraws its whole list every frame) - filtering seven short lists by `IsFavorite` every frame is negligible cost and needs no cache-invalidation logic at all.

**The DTR entry's `OnClick` toggles a `FavoritesWindow.IsOpen` flag directly** - `IDtrBarEntry.OnClick` is a plain `Action`, so `() => favoritesWindow.Toggle()` (a method `Window` already provides) is sufficient; no new event plumbing needed.

**`Plugin` gains one new public method, `OpenOwnerCommands()`**, rather than making `CollarWindow` itself public. This keeps `CollarWindow`'s existing encapsulation (private on `Plugin`, same as today) while giving `FavoritesWindow` (which only holds a `Plugin` reference, like every other picker window here) a way to trigger it - `OpenOwnerCommands()` internally calls a new public `CollarWindow.OpenOwnerTab()` that sets `activeModule = "owner"` and `IsOpen = true`.

**The DTR entry's label is plain text ("Collar"), not a native game icon glyph.** Dalamud's `SeIconChar` enum (checked against Dalamud's own XML docs) has no lock/collar/chain icon that would read as meaningfully on-theme, and inventing an icon choice here isn't worth the risk of picking something that reads oddly in the actual game font - plain text matches how several other utility plugins' DTR entries already work and is trivially reversible later if a good icon glyph is identified.

## Risks / Trade-offs

- **A DTR entry is a shared, limited screen resource** - many plugins compete for space in the server info bar, and a wide Sub-facing name could crowd others out. → Mitigated by keeping the label short ("Collar") and relying on `IDtrBarEntry.Tooltip` for the fuller explanation instead of a verbose label.
- **The favorites window not auto-dismissing on outside click is a real UX gap relative to a true native dropdown** - accepted and disclosed in the proposal (see the recorded scope decision) rather than silently simplified; closing it is one click on the DTR entry or the window's own controls, consistent with every other window in this plugin.
- **Restraints needing its own separate star-toggle addition (not automatically covered by the `DrawSavedQuickRow` change) is an easy place to miss** if this pattern is extended to an eighth category later. → Called out explicitly in tasks.md as its own task, not folded silently into the `DrawSavedQuickRow` task.
