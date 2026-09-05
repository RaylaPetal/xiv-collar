## ADDED Requirements

### Requirement: Reserved command tells are human-readable and backward compatible
The system SHALL compose newly created reserved gesture, Moodle, and restraint-animation commands using quoted, markup-free human-readable selectors. The parser SHALL unambiguously map those selectors to stable receiver-side identities and SHALL continue accepting legacy opaque identifiers and raw-markup names from existing saved quick commands. Human-readable presentation SHALL NOT change the trigger phrase, sender verification, consent gates, or direct-send policy.

#### Scenario: New rich command is composed
- **WHEN** an Owner copies or directly sends a gesture, Moodle, or animation-bearing restraint quick command created from imported Sub metadata
- **THEN** the tell presents readable names rather than an unexplained numeric/hash-like sequence or Moodles formatting tokens

#### Scenario: Selector contains spaces or punctuation
- **WHEN** a readable command selector contains spaces, quotes, separators, or command-like text
- **THEN** escaping and parsing preserve it as inert selector data and resolve the intended entry only

#### Scenario: Existing opaque quick command is used
- **WHEN** an Owner uses a saved pre-change quick command
- **THEN** the receiving Sub accepts its legacy payload without requiring manual recreation

#### Scenario: Readable selector is ambiguous or stale
- **WHEN** a received readable selector cannot resolve to exactly one local entry
- **THEN** no action is applied and local diagnostics distinguish missing from ambiguous resolution

