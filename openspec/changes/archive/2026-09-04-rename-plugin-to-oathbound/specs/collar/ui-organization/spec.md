## MODIFIED Requirements

### Requirement: Safeword has one canonical configuration surface
The main character header SHALL be the sole visible safeword configuration surface. Settings SHALL continue to explain how `/oathboundpanic` (aliased as `/collarpanic`) works when relevant but SHALL NOT display a second safeword input.

#### Scenario: User opens Settings
- **WHEN** the safeword editor is available in the main character header
- **THEN** Settings does not render a duplicate safeword input or a conflicting editable value

#### Scenario: User needs to configure safety
- **WHEN** the user views the main character header in any pairing state
- **THEN** the existing safeword editor remains available there
