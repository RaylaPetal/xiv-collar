## ADDED Requirements

### Requirement: Local command test input supports ordinary text editing
The Settings local Owner-command test field SHALL accept typing and the platform's normal select-all, copy, cut, paste, deletion, cursor movement, and replacement interactions. Using clipboard operations in this field SHALL NOT send chat, require pairing, or bypass the existing local-test validation path.

#### Scenario: User pastes a complete command
- **WHEN** the user focuses the local command-test field and pastes trigger text from the clipboard
- **THEN** the pasted text appears in the editable field and can be run through the normal local test

#### Scenario: User copies or replaces test text
- **WHEN** the user selects part or all of the command-test text and copies, cuts, deletes, or types over it
- **THEN** the field behaves like an ordinary editable text input without applying or sending the command until Run is explicitly chosen

#### Scenario: Sub tests any configured trigger
- **WHEN** the Sub selects a configured Title, Wardrobe, Gesture, Moodle, Restraint, Custom Trigger, Follow, Collar, or fixed release action in the local-test dropdown and chooses Run
- **THEN** the system composes the configured trigger phrase and selected command, shows the exact Owner text, and executes it through the same local command-test dispatch path without sending chat
