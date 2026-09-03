## Context

See `proposal.md` for motivation. The main window currently uses one evenly spaced navigation row and renders every Owner section fully expanded. Settings still owns duplicate safety and leash configuration. Wardrobe uses an empty allowlist as “none,” while gesture scanning iterates only explicit selections; both semantics must invert without breaking non-empty scoping or customized aliases.

## Goals / Non-Goals

**Goals:**

- Make navigation and Owner controls reflect the Owner/Sub information architecture.
- Make empty scan scope a convenient, clearly explained “all” state.
- Establish one canonical UI location for safeword and leash-trigger settings.
- Migrate only untouched legacy leash defaults.
- Let users validate every local action path safely before establishing an Owner pairing.

**Non-Goals:**

- Changing command wire formats, permission checks, pairing behavior, or the panic action.
- Providing a way to bypass category permissions or the automation-risk acknowledgement.
- Automatically persisting every installed animation mod as a selected mod.
- Removing the ability to narrow wardrobe or animation scans.

## Decisions

### Model empty scope as all at scan time

Wardrobe scanning will bypass folder predicates when the allowlist is empty. Gesture scanning will derive an effective directory sequence: all installed mod directories when `SelectedGestureMods` is empty, otherwise the saved explicit set. The saved collections remain empty in the “all” state so newly installed items are included automatically and configuration does not balloon.

Alternative considered: populate the saved collections with every current item. Rejected because it turns an open-ended “all” rule into a stale snapshot and makes future additions unexpectedly absent.

### Keep visual filters independent from scan scope

Folder and text fields only narrow what is displayed in selectors. Clearing them shows all available rows and never edits selected state. UI copy will state that zero selections means all mods, while any explicit selection switches to restricted mode.

### Use collapsible Owner sections with session-local expansion

Each category will be wrapped in a labeled collapsing header. Expansion is presentation state only and will not be serialized. Sensible defaults may open the first or most-used section initially, but each category remains independent and its existing control implementation stays inside the section.

Alternative considered: tabs within the Owner page. Rejected because nested tabs obscure cross-category browsing and add another navigation layer.

### Give NavBar an explicit trailing-item layout

The navigation component will support a trailing item or spacer calculation rather than depending on array order alone. Sub modules draw as the left cluster; Owner is positioned against the right content edge with enough room for its existing icon hit target and tooltip.

### Move, do not duplicate, configuration editors

The Settings safety card/input and leash alias card will be removed. Safeword configuration stays in the character header. Leash trigger fields move into the Collar module near collar and lock-related controls, using the same validation and immediate-save behavior.

### Migrate legacy defaults as an atomic pair

The default initializer changes to `leash`/`unleash`. At configuration load, only the exact legacy pair `leash-on` plus `leash-off` is rewritten. If either side differs, both values are treated as user-owned and preserved. A configuration version bump or idempotent migration guard will ensure the rule runs safely once.

### Dispatch local tests through shared action methods

Sub-facing configuration rows will expose Test buttons appropriate to their action. A small local-test coordinator will check the same category permission and acknowledgement prerequisites, then call the existing command services directly rather than fabricating an Owner identity, routing through the tell listener, or weakening its pairing checks. Apply/play/engage tests use the selected configured entry; clear/unlock/release tests call the corresponding local release path. Collar and other commands whose existing Owner override path is the accurate behavior should expose a narrowly scoped local method rather than duplicating integration calls in UI code.

Each invocation records transient UI feedback containing the action label, success/failure, and a useful failure reason where the service can provide one. Results are session-only and do not alter pairing. Test controls must be visibly labeled so they cannot be mistaken for Owner-send controls.

Alternative considered: inject a fake Owner command into `ChatCommandListener`. Rejected because it would couple testing to pairing identity and transport parsing, encourage a pairing bypass in security-sensitive code, and make failures harder to attribute.

## Risks / Trade-offs

- [Empty animation selection can scan a very large mod library] → Keep scans user-triggered, retain progress/error feedback, and avoid persisting an expanded selection list.
- [Users may mistake clearing selections for disabling scans] → State “No selections = all installed mods” beside the control and in scan feedback.
- [Owner sections can hide information users expect immediately] → Use clear category labels and counts/status summaries in collapsed headers where practical.
- [Right-aligned Owner icon can overlap at narrow widths] → Calculate the trailing position from available width and preserve a minimum gap from the Sub cluster.
- [Alias migration could overwrite customization] → Migrate only when both old defaults match exactly; preserve all partial or complete customizations.
- [A local Test can still change the player's actual game state] → Require the normal permission gates, retain acknowledgement for automated gesture/leash actions, label controls clearly, and provide matching clear/unlock/release tests.
- [UI code could drift from real command behavior] → Route tests through shared command-service entry points rather than reproducing IPC or movement logic in draw methods.

## Migration Plan

On first configuration load after the update, convert the exact untouched legacy leash pair to the new defaults and save. Empty scan collections require no stored-data migration because their meaning changes at evaluation time. UI relocation does not alter safeword, quick-command, or alias data. Rollback retains all values, although an older build will interpret empty scan scopes using its former “none” behavior.
