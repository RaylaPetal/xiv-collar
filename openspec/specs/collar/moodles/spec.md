## Purpose

Lets the Sub define their own short alias words that self-apply or self-clear a Moodles status from their own scanned catalog, the same trigger mechanism every other command category already provides.

## Requirements

### Requirement: Sub can self-apply or self-clear a Moodle via alias
The system SHALL let the Sub define a short alias word mapped to one status from their own scanned Moodles catalog, and a separate dedicated alias that clears the Sub's currently active Moodle, the same trigger mechanism `collar/title`/`collar/outfit`/`collar/gesture`/`collar/restraints` already provide for their own categories. Triggering the apply alias SHALL apply that status to the Sub's own character; triggering the clear alias SHALL remove the Sub's currently active status. Both SHALL require the Sub's own "Moodles" permission to be enabled, the same as the Owner's override.

#### Scenario: Sub applies their own Moodle via alias
- **WHEN** a Sub has defined an alias mapped to one of their own scanned Moodles statuses, has the "Moodles" permission enabled, and triggers that alias
- **THEN** the Sub's client applies that status to their own character

#### Scenario: Sub clears their own Moodle via alias
- **WHEN** a Sub has defined a clear-Moodle alias, has the "Moodles" permission enabled, and triggers it
- **THEN** the Sub's client removes their currently active Moodles status

#### Scenario: Self-apply alias requires the Moodles permission
- **WHEN** a Sub triggers a Moodles apply or clear alias while the "Moodles" permission is disabled
- **THEN** the Sub's client takes no action

### Requirement: Moodles markup is stripped before display
Moodles status names may carry Moodles' own inline markup tags (`[color=N]...[/color]`, `[glow=N]...[/glow]`, `[i]...[/i]`). The system SHALL strip these tags before displaying a status name anywhere in this plugin's UI, showing only the plain underlying text - it SHALL NOT attempt to reproduce the tags' color, glow, or italic styling, and SHALL NOT display the literal bracketed markup.

#### Scenario: A status name carrying markup displays as plain text
- **WHEN** the Sub's scanned Moodles catalog contains a status whose name includes markup such as `[color=2]Good Girl[/color]`
- **THEN** every place this plugin displays that status name shows only `Good Girl`, with no bracketed markup visible

#### Scenario: A status name with no markup is unaffected
- **WHEN** a scanned status's name contains no markup tags
- **THEN** the displayed name is identical to the name Moodles itself reports
