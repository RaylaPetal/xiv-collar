## MODIFIED Requirements

### Requirement: One-way pairing handshake completes both sides
The system SHALL let either side initiate pairing by sending a single invite tell carrying their own code and declared role and trigger phrase, in place of requiring both sides to separately send their own invite. The receiving side's Pending request and explicit Accept action work exactly as described in "Configured-identity pairing consent." Accepting a pending pairing request SHALL, as part of that one action, automatically send a single confirmation tell back to the inviting sender, carrying the accepting side's own declared role, trigger phrase, and the code that was matched. Upon receiving a confirmation tell whose carried code matches this side's own currently-configured code, the inviting side SHALL automatically complete pairing using the confirmed sender's identity and declared role/trigger phrase, with no further explicit local action required.

If the inviting side already has an invite outstanding (sent and not yet confirmed or expired) when it starts sending another one, the system SHALL require an explicit confirmation naming the pending invite being replaced before proceeding, rather than silently discarding it - a confirmation tell later arriving for the replaced invite SHALL still be ignored (it no longer matches the inviting side's current code), but the user SHALL have been told this would happen. After receiving a confirmation tell, the inviting side SHALL retry completing pairing (fetching and verifying the confirmed acceptance) a bounded number of times with backoff before giving up, rather than abandoning the attempt on the first failure; if every retry fails, the system SHALL surface an error to the inviting side rather than failing silently.

#### Scenario: One send and one accept pairs both sides
- **WHEN** one side sends an invite tell and the receiving side clicks Accept
- **THEN** the receiving side is paired immediately, and the inviting side becomes paired automatically upon receiving the resulting confirmation tell, with no further action from either side

#### Scenario: A confirmation tell with a non-matching code is ignored
- **WHEN** an inviting side receives a confirmation-shaped tell whose carried code does not match its own currently-configured code
- **THEN** the inviting side's pairing state is unchanged

#### Scenario: Both sides send an invite at the same time
- **WHEN** each side independently sends the other an invite before either accepts
- **THEN** each side's Pending request and Accept action work independently and normally, and if both sides accept, both end up paired with no error, duplicate state, or conflicting outcome

#### Scenario: Sending a new invite while one is already outstanding
- **WHEN** the inviting side already has an unconfirmed, unexpired invite outstanding and starts sending another invite
- **THEN** the system asks for explicit confirmation naming the outstanding invite's target before sending the new one, and does not send it until confirmed

#### Scenario: A confirmation arrives for a replaced invite
- **WHEN** the recipient of a replaced invite later accepts it and its confirmation tell reaches the inviting side
- **THEN** the inviting side ignores it, since it no longer matches the inviting side's current outstanding code, and its own pairing state is unchanged

#### Scenario: Completing pairing after a transient failure
- **WHEN** the inviting side receives a valid confirmation tell but the first attempt to fetch and verify the confirmed acceptance fails transiently
- **THEN** the system retries a bounded number of times with backoff before giving up

#### Scenario: Completing pairing fails after every retry
- **WHEN** every retry to fetch and verify a confirmed acceptance fails
- **THEN** the inviting side surfaces an error explaining that pairing could not be completed, instead of leaving the invite outstanding with no explanation

## ADDED Requirements

### Requirement: Invite target is validated before sending
The system SHALL validate that a typed invite target matches the expected "Name Surname@World" shape before composing and sending the invite tell, and SHALL reject an invalid target with a visible explanation instead of silently sending a malformed `/tell` that the game itself will reject with no plugin-visible feedback.

#### Scenario: Target is missing a world
- **WHEN** a user triggers Send Invite with a target that has no `@World` portion
- **THEN** the system rejects the attempt and explains that a world is required, without sending anything

#### Scenario: Well-formed target is sent normally
- **WHEN** a user triggers Send Invite with a target matching "Name Surname@World"
- **THEN** the system composes and sends the invite tell exactly as it does today
