## Why

The main window opens with a utilitarian pairing card that feels empty when no peer is connected and hides the safeword configuration in Settings. A consistent character-focused banner would make the plugin feel personal and polished while keeping identity, pairing ownership, and the local safety control immediately understandable in every state.

## What Changes

- Replace the current pairing card presentation with a polished, responsive header banner centered on the logged-in character.
- Show reliably available local character context such as character name and home world, with optional Free Company information only when the game exposes it safely.
- Keep the pairing state visible in the banner at all times: not paired, paired Owner/owned Sub relationship, and pending pairing request.
- Always expose safeword configuration from the main header, regardless of role or pairing state, while preserving `/collarpanic` as the deliberate typed panic action.
- Handle character data that is loading, unavailable, or incomplete without stale identity information or broken layout.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `collar/pairing`: Extend the always-visible pairing and local-safety experience with a character identity banner, explicit relationship status, and pairing-independent safeword configuration.

## Impact

- Main window header and pairing-request UI in `CollarWindow`.
- Safeword editing currently housed in `SettingsWindow`, including shared editing/save behavior if it remains available there.
- Dalamud client/player data access for local name, home world, and best-effort Free Company display.
- Existing pairing and panic behavior remain compatible; this change alters presentation and safeword accessibility, not the chat protocol or permission model.
