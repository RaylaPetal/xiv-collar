# collar/chat-transport Specification

## Purpose

Delivers Owner-issued commands to a paired Sub as in-game tell messages instead of over a hosted relay, eliminating hosting cost by riding infrastructure FFXIV's own server already provides, while keeping the automation-risk profile lower than the relay it replaces by requiring every outbound trigger message to be sent by the Owner's own direct action.

## Requirements

### Requirement: Trigger-phrase command delivery over tells
The system SHALL deliver a command as an in-game tell consisting of a configurable trigger phrase followed by an alias identifying the command, and SHALL only process such messages received via the private tell channel - never a public chat channel (party, say, shout, or otherwise).

#### Scenario: Trigger tell applies the matching alias
- **WHEN** a Sub receives a tell from their configured Owner containing the trigger phrase followed by a locally-known alias
- **THEN** the Sub's client applies the local action mapped to that alias

#### Scenario: Non-tell channels are never processed
- **WHEN** text matching the trigger phrase and an alias appears in party, say, or any channel other than a private tell
- **THEN** the Sub's client does not act on it

### Requirement: Alias resolution against a locally-defined dictionary
The system SHALL resolve the alias following the trigger phrase against a dictionary the receiving Sub defines locally, and SHALL NOT require, accept, or transmit a definition of what an alias means over chat - only the alias's short name crosses the chat channel.

#### Scenario: Known alias resolves
- **WHEN** the alias following the trigger phrase matches an entry the Sub has locally defined
- **THEN** the corresponding local action executes

#### Scenario: Unknown alias is ignored
- **WHEN** the alias following the trigger phrase does not match any locally-defined entry
- **THEN** the Sub's client takes no game-state-changing action

### Requirement: No automated sending
The system SHALL NOT itself invoke any function that transmits a trigger message on a user's behalf. A trigger message SHALL only be sent by the sending player's own direct action. The system MAY compose trigger text or place it on the clipboard for convenience, but SHALL NOT call any chat-send API itself.

#### Scenario: Composing a trigger message does not send it
- **WHEN** an Owner's client builds trigger text for a command
- **THEN** the text is made available to copy, and no chat-send function is invoked by the plugin
