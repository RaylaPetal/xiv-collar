## REMOVED Requirements

### Requirement: Scanning every catalog together
**Reason**: Restraints no longer has a scan step - a restraint device is captured individually from a single equipped gear piece (see `collar/restraints`), not scanned from a saved-designs library. Replaced by a narrower version of the same requirement covering only the categories that still scan.
**Migration**: None needed for Wardrobe/Gesture/Moodles - their scan behavior, including via the unified action, is unchanged. Restraints devices are captured one at a time in the Restraints tab instead of via "Scan all".

## ADDED Requirements

### Requirement: Scanning the scannable catalogs together
The system SHALL let the Sub trigger a scan of Wardrobe, Gesture, and Moodles catalogs from a single action. Each category's own scan scope (Wardrobe's folder allowlist, Gesture's selected-mod set) SHALL remain independently configurable and SHALL apply exactly as it does for that category's individual scan. Restraints has no scan step and SHALL NOT be part of this unified scan action - a restraint device is captured individually (see `collar/restraints`), independent of scanning.

#### Scenario: One action scans every scannable category
- **WHEN** the Sub triggers the unified scan action
- **THEN** Wardrobe, Gesture, and Moodles are each rescanned using their own currently-configured scope, and the results are exactly what triggering each category's own scan individually would have produced

#### Scenario: A per-category scope still restricts that category's results
- **WHEN** the Sub has configured a Wardrobe folder allowlist or a Gesture mod selection before running the unified scan
- **THEN** the scanned results for that category are restricted to the configured scope, the same as scanning that category alone

#### Scenario: Restraints is not part of the unified scan
- **WHEN** the Sub triggers the unified scan action
- **THEN** no Restraints scan runs as part of it, and any already-captured restraint devices are left exactly as they were
