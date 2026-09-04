## ADDED Requirements

### Requirement: Moodles markup is stripped before display
Moodles status names may carry Moodles' own inline markup tags (`[color=N]...[/color]`, `[glow=N]...[/glow]`, `[i]...[/i]`). The system SHALL strip these tags before displaying a status name anywhere in this plugin's UI, showing only the plain underlying text - it SHALL NOT attempt to reproduce the tags' color, glow, or italic styling, and SHALL NOT display the literal bracketed markup.

#### Scenario: A status name carrying markup displays as plain text
- **WHEN** the Sub's scanned Moodles catalog contains a status whose name includes markup such as `[color=2]Good Girl[/color]`
- **THEN** every place this plugin displays that status name shows only `Good Girl`, with no bracketed markup visible

#### Scenario: A status name with no markup is unaffected
- **WHEN** a scanned status's name contains no markup tags
- **THEN** the displayed name is identical to the name Moodles itself reports
