## MODIFIED Requirements

### Requirement: Moodles markup is stripped before display
The system SHALL strip supported Moodles formatting tokens, including color, glow, and emphasis tags, from every Oathbound UI label and every newly composed outbound command tell. The original status identity/name SHALL remain available internally for exact Moodles lookup, and previously saved commands containing raw markup SHALL remain accepted.

#### Scenario: Owner composes a Moodle command
- **WHEN** an imported Moodle name contains formatting tokens and the Owner copies or sends its command
- **THEN** the visible tell contains the readable status name without formatting tokens

#### Scenario: A status name carrying markup displays as plain text
- **WHEN** a Moodles status name contains supported markup tokens
- **THEN** Oathbound displays the name without those tokens

#### Scenario: A status name with no markup is unaffected
- **WHEN** a Moodles status name contains no supported markup tokens
- **THEN** its displayed text is unchanged

#### Scenario: Sanitized names collide
- **WHEN** stripping markup makes two statuses share the same readable name
- **THEN** the command carries deterministic disambiguation without exposing raw markup

#### Scenario: Legacy raw-markup command is received
- **WHEN** a previously saved command includes the exact raw Moodles name
- **THEN** it remains resolvable during the compatibility period
