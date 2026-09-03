## ADDED Requirements

### Requirement: Empty wardrobe scope includes all designs
The wardrobe scanner SHALL treat an empty design-folder scope as an explicit request to include every locally saved Glamourer design. When one or more folder scopes are configured, the scanner SHALL include only designs within those scopes.

#### Scenario: Wardrobe scope is empty
- **WHEN** the Sub rescans wardrobe designs with no folder scope configured
- **THEN** every locally saved Glamourer design is available in the wardrobe catalog

#### Scenario: Wardrobe scope is configured
- **WHEN** the Sub rescans with one or more folder scopes configured
- **THEN** only designs matching those folder scopes are available in the wardrobe catalog
