## Purpose

Lets a Sub bundle several actions across different categories - Title, Outfit, Gesture, Moodle, Restraint, and a raw chat message - behind one alias, so a single trigger can do everything a scene moment needs instead of requiring several separate commands; also lets the Owner author the same kind of bundle directly, without a Sub-defined name, the same way Restraints already supports both a Sub-named device and an Owner-authored ad-hoc one.

## ADDED Requirements

### Requirement: Sub defines a named Custom Trigger bundling multiple actions
The system SHALL let the Sub define a Custom Trigger: a short alias word mapped to an ordered list of one or more actions, each action being one of Title (text, prefix, color), Outfit (a design the Sub has scanned), Gesture (an animation from the Sub's catalog), Moodle (a status from the Sub's catalog), Restraint (a device the Sub has captured), or a raw chat message. Triggering the alias SHALL apply every action in the bundle. This SHALL use the same alias-resolution mechanism (`collar/chat-transport`'s locally-defined dictionary) every other alias category already uses - no new wire syntax is needed for the Owner to trigger a Sub-defined Custom Trigger.

#### Scenario: Sub defines a multi-action trigger
- **WHEN** a Sub creates a Custom Trigger with an alias word and two or more actions across different categories
- **THEN** triggering that alias applies every action in the bundle

#### Scenario: Owner triggers a Sub-defined Custom Trigger the same way as any other alias
- **WHEN** a paired Owner sends a tell containing the trigger phrase followed by the Sub's Custom Trigger alias
- **THEN** the Sub's client resolves and applies it exactly as it would any other locally-defined alias, with no reserved keyword involved

### Requirement: Each bundled action still requires its own category's permission
The system SHALL check each action within a Custom Trigger against that action's own category permission (Title, Outfit, Gesture, Moodles, Restraints, or the dedicated chat-message permission - see "Sending a chat message requires its own dedicated permission and acknowledgement") independently before applying it. An action whose category permission is disabled SHALL be skipped without preventing the rest of the bundle's permitted actions from applying.

#### Scenario: One disabled category is skipped, others still apply
- **WHEN** a Custom Trigger bundles a Title action and a Restraint action, and the Sub has disabled the Restraints permission but left Title enabled
- **THEN** triggering it applies the Title action and skips the Restraint action, with no error blocking the Title action

#### Scenario: Every bundled category disabled applies nothing
- **WHEN** every action in a Custom Trigger belongs to a category whose permission is currently disabled
- **THEN** triggering it applies none of the bundle's actions

### Requirement: Owner can author an ad-hoc Custom Trigger directly
The system SHALL let the Owner compose a Custom Trigger's actions directly in the Owner tab, the same way Restraints already lets the Owner define ad-hoc gear without a Sub-side name, and send it as one self-contained command carrying the full bundle. This ad-hoc bundle SHALL be subject to the same per-action category-permission gating as a Sub-defined Custom Trigger.

#### Scenario: Owner sends an ad-hoc bundle with no Sub-defined name
- **WHEN** the Owner composes a bundle of actions in the Owner tab and sends it
- **THEN** the paired Sub's client applies every permitted action in that bundle, with no lookup of any Sub-side named trigger

#### Scenario: Ad-hoc bundle actions are gated the same as named-trigger actions
- **WHEN** an Owner-authored ad-hoc bundle includes an action whose category permission is disabled on the Sub's side
- **THEN** that action is skipped while the bundle's other permitted actions still apply

### Requirement: Sending a chat message requires its own dedicated permission and acknowledgement
The system SHALL let a Custom Trigger action send an arbitrary chat message - any text, to any channel, including public channels - as the Sub's own client, sent exactly as the Sub's client would send any other locally-composed chat message. This SHALL require a dedicated "Custom chat messages" permission, independent of every other category permission, and a dedicated explicit acknowledgement separate from the general automation-risk acknowledgement, before any chat-message action can apply. This is a materially broader automation surface than Gesture's existing chat use (which is limited to a closed set of self-targeting pose/emote commands) and SHALL be disclosed as such.

#### Scenario: Chat-message action requires its own permission
- **WHEN** a Custom Trigger's chat-message action is triggered while the "Custom chat messages" permission is disabled
- **THEN** the message is not sent, regardless of whether the trigger's other actions apply

#### Scenario: Chat-message action requires its own acknowledgement
- **WHEN** a Sub has enabled the "Custom chat messages" permission but has not completed its dedicated acknowledgement
- **THEN** the message is not sent

#### Scenario: An enabled and acknowledged chat action sends exactly what was configured
- **WHEN** a Sub has enabled the permission, completed the acknowledgement, and a chat-message action's configured text and channel are triggered
- **THEN** that exact text is sent to that exact channel, unmodified
