## ADDED Requirements

### Requirement: Imported Sub animations back Owner restraint selection
The system SHALL retain enough metadata from every imported Sub gesture entry to power Owner-side restraint animation browsing and to construct a readable, uniquely resolvable command selector. This imported catalog SHALL be distinct from the Owner's local scan and SHALL remain the sole animation source for Owner-authored restraint rules.

#### Scenario: Imported catalog populates restraint picker
- **WHEN** the Owner imports a Sub export containing gesture entries
- **THEN** those entries appear in the Owner's restraint animation picker with the Sub's mod/group/animation/trigger organization

#### Scenario: Owner local scan differs from imported catalog
- **WHEN** the Owner's locally installed animations differ from the Sub's imported animations
- **THEN** the restraint picker contents remain based only on the imported Sub catalog

#### Scenario: Re-import refreshes metadata
- **WHEN** the Owner re-imports an updated export from the same Sub
- **THEN** restraint selection uses the refreshed imported metadata and visibly marks saved choices that no longer exist

