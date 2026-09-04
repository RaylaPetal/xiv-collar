## MODIFIED Requirements

### Requirement: No automated sending
The system SHALL NOT itself invoke any function that transmits a trigger message on a user's behalf. A trigger message SHALL only be sent by the sending player's own direct action. The system MAY compose trigger text or place it on the clipboard for convenience, but SHALL NOT call any chat-send API itself. As the one narrow exception, accepting a pending pairing request (see `collar/pairing`'s "One-way pairing handshake completes both sides") MAY automatically send a single confirmation tell back to the inviting peer, since that send is a direct, singular consequence of the accepting user's own explicit Accept click, not an autonomous reaction to observed chat or game state, and it fires at most once per Accept click.

#### Scenario: Composing a trigger message does not send it
- **WHEN** an Owner's client builds trigger text for a command
- **THEN** the text is made available to copy, and no chat-send function is invoked by the plugin

#### Scenario: Accepting a pairing request sends exactly one confirmation tell
- **WHEN** a user clicks Accept on a pending pairing request
- **THEN** exactly one confirmation tell is sent automatically as a direct result of that click, and no other automatic send occurs anywhere else in the plugin
