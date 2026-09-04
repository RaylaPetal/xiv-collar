# collar/chat-transport Specification

## Purpose

Delivers Owner-issued commands to a paired Sub as in-game tell messages instead of over a hosted relay, eliminating hosting cost by riding infrastructure FFXIV's own server already provides, while keeping the automation-risk profile lower than the relay it replaces by requiring every outbound trigger message to be sent by the Owner's own direct action.

## Requirements

### Requirement: Trigger-phrase command delivery over tells
The system SHALL deliver a command as an in-game tell consisting of a configurable trigger phrase followed by an alias identifying the command, and SHALL only process such messages received via the private tell channel - never a public chat channel (party, say, shout, or otherwise). When composing a trigger message to a paired peer, the system SHALL use that peer's trigger phrase captured during the pairing handshake (see `collar/pairing`) rather than the composing side's own independently-configured trigger phrase, so the composed message always matches what the receiving side's listener expects. The composing side's own configured trigger phrase SHALL be used only when no peer trigger phrase has been captured (no pairing yet, or a peer whose handshake didn't declare one).

#### Scenario: Trigger tell applies the matching alias
- **WHEN** a Sub receives a tell from their configured Owner containing the trigger phrase followed by a locally-known alias
- **THEN** the Sub's client applies the local action mapped to that alias

#### Scenario: Non-tell channels are never processed
- **WHEN** text matching the trigger phrase and an alias appears in party, say, or any channel other than a private tell
- **THEN** the Sub's client does not act on it

#### Scenario: Composing to a paired peer uses the peer's trigger phrase
- **WHEN** an Owner composes a command tell to a paired Sub whose trigger phrase was captured during pairing
- **THEN** the composed message is prefixed with the Sub's captured trigger phrase, not the Owner's own independently-configured trigger phrase

#### Scenario: Composing before any peer trigger phrase is known
- **WHEN** a user composes a trigger message with no captured peer trigger phrase (unpaired, or a peer whose handshake didn't declare one)
- **THEN** the composed message is prefixed with the composing side's own configured trigger phrase, the same as before this change

### Requirement: Alias resolution against a locally-defined dictionary
The system SHALL resolve the alias following the trigger phrase against a dictionary the receiving Sub defines locally, and SHALL NOT require, accept, or transmit a definition of what an alias means over chat - only the alias's short name crosses the chat channel.

#### Scenario: Known alias resolves
- **WHEN** the alias following the trigger phrase matches an entry the Sub has locally defined
- **THEN** the corresponding local action executes

#### Scenario: Unknown alias is ignored
- **WHEN** the alias following the trigger phrase does not match any locally-defined entry
- **THEN** the Sub's client takes no game-state-changing action

### Requirement: No automated sending
The system SHALL NOT itself invoke any function that transmits a trigger message on a user's behalf. A trigger message SHALL only be sent by the sending player's own direct action. The system MAY compose trigger text or place it on the clipboard for convenience, but SHALL NOT call any chat-send API itself. Two narrow, explicit exceptions exist, each a direct, singular, synchronous consequence of one specific local action - never a reaction to incoming chat or other background state: accepting a pending pairing request (see `collar/pairing`'s "One-way pairing handshake completes both sides") MAY automatically send a single confirmation tell back to the inviting peer; and triggering the panic action (see `collar/pairing`'s "Panic notifies the peer, best-effort") MAY automatically send a single notification tell to the cached peer identity. No other automatic send exists anywhere in the system.

#### Scenario: Composing a trigger message does not send it
- **WHEN** an Owner's client builds trigger text for a command
- **THEN** the text is made available to copy, and no chat-send function is invoked by the plugin

#### Scenario: Accepting a pairing request sends exactly one confirmation tell
- **WHEN** a user clicks Accept on a pending pairing request
- **THEN** exactly one confirmation tell is sent automatically as a direct result of that click, and no other automatic send occurs anywhere else in the plugin

#### Scenario: Panic sends exactly one notification tell
- **WHEN** a user triggers the panic action while a peer identity is cached
- **THEN** exactly one notification tell is sent automatically as a direct result of that action, and no other automatic send occurs as a result of it

### Requirement: Composing and sending require active pairing, not just a remembered peer
The system SHALL only compose an addressed `/tell` and only allow sending one when pairing is currently active (`Pairing.IsPaired`), not merely when a peer name and world were captured at some point in the past. A side whose own pairing has ended (via panic or an Owner's release) SHALL NOT be able to compose or send an addressed command to that former peer until pairing is established again.

#### Scenario: A side that panicked cannot still send
- **WHEN** a side has triggered panic, ending its own pairing, and a peer name/world remain cached from before
- **THEN** that side's Send controls are disabled and any composed text carries no `/tell` target, the same as before any pairing ever existed

#### Scenario: An actively paired side can still compose and send
- **WHEN** a side's pairing is currently active
- **THEN** composing and sending work exactly as they did before this requirement was added

### Requirement: The trigger phrase in effect is visible once paired
The Settings UI SHALL show which trigger phrase is actually being used to compose outgoing commands to the current peer (the peer's captured phrase if known, otherwise the user's own), so a mismatch between an un-updated peer and this client is visible rather than silently producing commands the peer's listener will never recognize.

#### Scenario: Paired with a known peer trigger phrase
- **WHEN** a user is paired with a peer whose trigger phrase was captured during handshake
- **THEN** Settings shows that captured trigger phrase as the one in effect for outgoing commands

#### Scenario: Paired with no known peer trigger phrase
- **WHEN** a user is paired with a peer whose trigger phrase was not captured (an un-updated peer)
- **THEN** Settings shows the user's own configured trigger phrase as the one in effect, distinguishably from the known-peer case

### Requirement: Receiving and dispatching a trigger tell is locally diagnosable
The system SHALL log, locally only, the outcome of every decision point in receiving and dispatching an incoming chat message as a potential trigger tell: whether it was a tell at all, whether the sender matched the configured peer, whether it started with the expected trigger phrase, whether the resulting category's permission (and ToS acknowledgement, where required) was enabled, and whether dispatch resulted in an applied action, a rejected/unmatched alias, or a failed apply. This logging SHALL NOT transmit anything to the Owner, the Sub's peer, or any network destination - it is a local diagnostic trail only.

#### Scenario: A message is discarded before matching the trigger phrase
- **WHEN** an incoming tell's sender matches the configured peer but the message does not start with the expected trigger phrase
- **THEN** a local log entry records that the message was discarded at the trigger-phrase check, distinguishable from a sender mismatch or a permission rejection

#### Scenario: A message is rejected by a permission or ToS gate
- **WHEN** an incoming trigger tell's category permission (or required ToS acknowledgement) is not enabled
- **THEN** a local log entry records that the message was rejected at that gate, naming the category

#### Scenario: A dispatched command fails to find a match
- **WHEN** a trigger tell's reserved-word command or alias does not match anything the Sub has locally defined or scanned
- **THEN** a local log entry records the unmatched name, distinguishable from a permission rejection or a successful apply

#### Scenario: A dispatched command applies successfully
- **WHEN** a trigger tell is fully recognized, permitted, and its underlying command apply succeeds
- **THEN** a local log entry records the successful apply, naming the category and target

#### Scenario: Diagnostic logging never leaves the local client
- **WHEN** any of the above log entries are recorded
- **THEN** nothing is sent to the Owner, the paired peer, or any network destination as a result

### Requirement: An Owner-style command can be tested entirely locally
The system SHALL let a Sub type raw command text - the trigger phrase and the reserved-word command or alias it would expect from a real incoming tell - into a dedicated local control, and SHALL report a specific result: which trigger-phrase, permission/ToS, or dispatch check it passed or failed, or that it applied successfully. This test SHALL exercise the same trigger-phrase check, permission/ToS gates, and reserved-word/alias dispatch logic a real incoming tell goes through, with the one deliberate exception that no sender identity is checked (there is no real sender in a local test) and no pairing is required. The system SHALL NOT send or receive any chat message as part of this test.

#### Scenario: Local test with the wrong trigger phrase
- **WHEN** a Sub tests command text that does not start with their own configured trigger phrase
- **THEN** the result reports that the trigger phrase did not match, naming the phrase the system expected

#### Scenario: Local test blocked by a permission or ToS gate
- **WHEN** a Sub tests command text whose category permission (or required ToS acknowledgement) is not enabled
- **THEN** the result reports that the category is not permitted, without applying anything

#### Scenario: Local test with an unmatched name
- **WHEN** a Sub tests command text whose reserved-word command or alias does not match anything locally defined or scanned
- **THEN** the result reports no match found, naming what was looked up

#### Scenario: Local test succeeds
- **WHEN** a Sub tests command text that passes every check
- **THEN** the underlying action is actually applied locally (the same as a real incoming tell would), and the result reports success

#### Scenario: Local test requires no pairing and sends nothing
- **WHEN** a Sub uses this control while unpaired, or while paired
- **THEN** no chat message is sent or received, and no pairing state is required or changed by the test itself
