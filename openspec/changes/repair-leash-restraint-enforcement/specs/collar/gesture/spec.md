## MODIFIED Requirements

### Requirement: Animation identity is preserved across sharing and commands
The system SHALL preserve a stable animation identity across Sub export, Owner import, command storage, and Sub execution, while composing new gesture tells with a quoted human-readable selector derived from mod, group, animation, and trigger labels instead of displaying the opaque identity. The selector SHALL resolve deterministically; when readable fields are not unique, the command SHALL include the minimum readable disambiguation needed. Previously saved commands containing the legacy opaque identity SHALL remain accepted.

#### Scenario: Owner sends an imported gesture
- **WHEN** the Owner sends a newly imported gesture quick command
- **THEN** the visible tell names the gesture readably and the Sub resolves it to the same stable animation identity exported earlier

#### Scenario: Sub exports gesture names
- **WHEN** the Sub exports the gesture catalog
- **THEN** each entry carries its stable identity and readable mod, group, animation, and trigger metadata

#### Scenario: Owner chooses an imported animation
- **WHEN** the Owner chooses an animation imported from the Sub
- **THEN** the saved quick command preserves the Sub's stable identity and readable selector metadata

#### Scenario: Sub opens the animation picker
- **WHEN** the Sub opens their own gesture animation picker
- **THEN** it continues to show the Sub's locally scanned animation catalog

#### Scenario: Readable labels collide
- **WHEN** two exported animations share a display name
- **THEN** their generated selectors include enough mod, group, trigger, or stable suffix information to resolve each one unambiguously

#### Scenario: Legacy command is sent
- **WHEN** the Sub receives a previously saved gesture command containing an opaque identity
- **THEN** it resolves through the legacy identity path during the compatibility period
