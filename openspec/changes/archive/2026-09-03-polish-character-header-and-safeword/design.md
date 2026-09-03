## Context

See `proposal.md` for motivation. `CollarWindow` currently draws a fixed-height pairing card before navigation, while `SettingsWindow` owns a separate safeword input buffer and safety card. Local character services already exist, and pairing state is local configuration plus an optional pending request. Character objects are transient across login, logout, zone transitions, and character changes, so presentation must read live data without retaining unsafe references or stale identity strings.

## Goals / Non-Goals

**Goals:**

- Give the main window one coherent, attractive identity, relationship, and safety header in all states.
- Derive core identity only from reliable live local-player data and degrade gracefully while it is unavailable.
- Keep a single saved safeword value synchronized anywhere it can be edited.
- Preserve existing pairing acceptance, Owner release, and typed panic semantics.

**Non-Goals:**

- Adding a clickable panic button, portrait capture, remote profile lookup, or new network traffic.
- Persisting local character identity or Free Company information in plugin configuration.
- Changing pairing messages, role rules, permissions, or panic cleanup behavior.

## Decisions

### Compose the banner from live data and local state

The header will obtain the local player fresh while drawing and copy only display values needed for that frame: character name, home-world name, and the Free Company tag/name if the currently supported game API exposes it reliably. Pairing and pending-request text will continue to come from existing state. No character identity cache will be introduced, preventing one character's details from appearing after logout or character switch.

Alternative considered: cache the last known identity for a smoother logged-out presentation. Rejected because stale character identity is more confusing and privacy-sensitive than a short loading/unavailable state.

### Treat name and home world as primary; Free Company as optional

The visual hierarchy will use character name as the banner title and home world as stable secondary identity. Free Company information will appear only when non-empty and available from the local character object. The implementation must not add memory reads or external lookups solely to fill decorative metadata.

Alternative considered: always reserve fields for server and Free Company. Rejected because empty placeholders make the header noisy and imply guarantees the client cannot always provide.

### Use one reusable safeword editor bound to configuration

Extract the editing behavior into a small reusable UI routine or shared buffer strategy used by the main header and, if retained, Settings. Both surfaces write the same `PanicSafeword` value immediately. The field should use password masking by default with an explicit reveal affordance so “always show the safeword” means always show its control and configured state, not permanently expose the secret on screen.

Alternative considered: move the field exclusively to the main window. Keeping the Settings copy is acceptable if synchronization is robust; removing it is also acceptable if the main header becomes the clear canonical location.

### Keep relationship language role-aware and explicit

The banner state model is pending, unpaired, paired-as-Owner, or paired-as-Sub. Paired text will use explicit “Owns” and “Owned by” wording with peer name and world. Pending requests take visual priority but do not replace local identity or safeword access. Existing accept/reject and Owner release controls remain in the header.

### Prefer content-driven layout over fixed card heights

The banner will use wrapping text, grouped rows/columns, and measured spacing instead of state-specific fixed heights. Decorative color, icons, separators, and subtle background treatment should reuse the existing theme so the result remains coherent and readable at minimum window width.

## Risks / Trade-offs

- [Free Company information may be absent or differ by API version] → Make it optional and validate against the Dalamud API already referenced by the project before implementation.
- [Two safeword inputs can drift because each window has its own buffer] → Centralize synchronization or refresh each buffer from configuration on open and after edits.
- [Password masking could conflict with the user's desire to see the value] → Always expose configured/unconfigured state and provide an intentional reveal control.
- [Rich header content can crowd narrow windows] → Use responsive wrapping and verify every pairing state at the configured minimum width.
- [Local-player services can be unavailable during login or zoning] → Render a neutral identity placeholder while retaining relationship and safety controls from local configuration.

## Migration Plan

No data migration is required. Existing `PanicSafeword`, role, pairing, and pending-request behavior remain the source of truth. Deployment replaces the main pairing card and optionally consolidates the duplicate Settings safety editor; rollback restores the old UI without configuration loss.
