## 1. Live character identity

- [x] 1.1 Add a frame-local character display model sourced from the current local player, containing name, home world, and optional supported Free Company information; verify logout, login, zoning, and character switching cannot display cached identity from another character.
- [x] 1.2 Add graceful unavailable/loading behavior for partial local-player data; verify pairing status and safeword editing remain usable when no character object exists.

## 2. Responsive header banner

- [x] 2.1 Replace the fixed-height pairing card with a theme-consistent character banner that visually prioritizes name and home world and conditionally shows Free Company context; verify populated and missing optional fields produce balanced layouts.
- [x] 2.2 Render explicit pending, not-paired, “Owns”, and “Owned by” relationship states in the banner while preserving accept/reject and Owner release behavior; verify each role/state combination displays the correct peer name and world.
- [x] 2.3 Make the banner content-driven and responsive using wrapped text and reorganized controls; verify long names/worlds and every relationship state fit at the 460-pixel minimum window width without clipping or overlap.

## 3. Always-accessible safeword

- [x] 3.1 Add a safeword editor to the main header that is present for Owner and Sub roles, paired and unpaired states, pending requests, and unavailable character data; verify setting, changing, and clearing it saves immediately in every state.
- [x] 3.2 Mask the safeword value by default and add an intentional reveal/hide affordance plus clear configured/unconfigured feedback; verify interacting with the editor never invokes panic or changes pairing.
- [x] 3.3 Consolidate or synchronize the existing Settings safeword editor so every retained surface reflects the same `PanicSafeword` value; verify edits made in either open window appear correctly in the other without reopening it.
- [x] 3.4 Preserve `/collarpanic` and panic-hotkey behavior for configured and blank safewords; verify correct, incorrect, cleared, and case-insensitive command arguments retain existing behavior.

## 4. Integration and polish

- [x] 4.1 Update inline help and README documentation to describe the persistent identity/relationship banner and always-accessible safeword configuration while retaining the no-clickable-panic safety rationale.
- [x] 4.2 Build the solution and run relevant pairing, safeword, and UI-state tests; verify there are no compiler warnings, spec regressions, or stale references to the removed fixed pairing-card layout.
- [x] 4.3 Perform an in-game smoke test across login/loading, unpaired, pending, paired Owner, and paired Sub states at minimum and normal widths; verify live identity accuracy, optional FC behavior, relationship actions, safeword synchronization, and `/collarpanic` end to end.
