## MODIFIED Requirements

### Requirement: Movement input blocked while locked
The system SHALL, once a movement lock is engaged on a consenting Sub, suppress keyboard/controller movement, mouse-button movement, autorun requests, and any other player-controlled movement path for the entire lock duration. Suppression SHALL be continuously asserted and SHALL NOT suppress the follow movement initiated by the leash itself.

#### Scenario: Sub attempts keyboard or controller movement while locked
- **WHEN** a movement lock is active and the Sub supplies directional movement input
- **THEN** the character continues leash-driven follow and does not move as a result of that input

#### Scenario: Sub presses a movement key while locked
- **WHEN** a movement lock is active and the Sub presses a movement key
- **THEN** the character does not move independently and leash-driven follow is not cancelled

#### Scenario: Sub attempts mouse movement or autorun while locked
- **WHEN** a movement lock is active and the Sub attempts mouse-button movement or autorun
- **THEN** that input neither moves the character independently nor cancels leash-driven follow

### Requirement: Auto-unfollow suppressed while locked
The system SHALL start following the paired Owner when the leash engages, SHALL prevent local input and unfollow requests from cancelling that follow, and SHALL keep following until the leash releases. If the paired Owner cannot be resolved as a valid follow target, or required follow/movement enforcement is unavailable, engagement SHALL fail without recording an active leash and SHALL report the specific failure locally.

#### Scenario: Owner engages leash while targetable
- **WHEN** a paired Owner sends the configured leash command and is a valid follow target
- **THEN** the Sub begins following that Owner and manual input cannot cancel the follow while the leash remains active

#### Scenario: Sub nudges a key during an active leash
- **WHEN** the Sub supplies movement input during an active leash follow
- **THEN** follow continues uninterrupted

#### Scenario: Owner cannot be resolved
- **WHEN** a leash command is received but the paired Owner cannot be resolved as a valid follow target
- **THEN** no leash is recorded active and the local diagnostic reports that follow could not start

#### Scenario: Enforcement is unavailable
- **WHEN** any enforcement capability required for a non-bypassable leash is unavailable
- **THEN** the leash command is rejected and does not report success
