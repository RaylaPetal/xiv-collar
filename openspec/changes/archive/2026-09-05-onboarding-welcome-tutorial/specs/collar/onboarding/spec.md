## Purpose

Gives a first-time user an explicit, one-time introduction to their chosen Role and trigger phrase, followed by a guided walkthrough of the plugin's own tabs, and lets that walkthrough be replayed whenever a user's Role changes for the first time or on demand from Settings.

## ADDED Requirements

### Requirement: Welcome window appears once on first plugin load
The system SHALL show a Welcome window automatically the first time the plugin loads with no prior recorded completion of it, before the main `CollarWindow` is shown for the first time. The Welcome window SHALL let the user set Role and trigger phrase, writing directly to the same persisted values Settings' Identity & Pairing tab reads and writes. Once the user completes or dismisses the Welcome window, it SHALL NOT appear again on any subsequent plugin load.

#### Scenario: First-ever plugin load shows the Welcome window
- **WHEN** the plugin loads for the first time, with no prior Welcome completion recorded
- **THEN** the Welcome window is shown, offering Role and trigger phrase controls

#### Scenario: Welcome window does not reappear after completion
- **WHEN** the user has already completed or dismissed the Welcome window in a prior session
- **THEN** subsequent plugin loads do not show the Welcome window again

#### Scenario: Role and trigger phrase set in Welcome persist to Settings
- **WHEN** the user sets Role and trigger phrase in the Welcome window
- **THEN** Settings' Identity & Pairing tab reflects the same values afterward, since both read and write the same persisted configuration

### Requirement: Guided tutorial follows Welcome and switches tabs automatically
Immediately after the Welcome window is completed or dismissed, the system SHALL open a guided tutorial that drives the main `CollarWindow`: it SHALL switch the active tab through a defined sequence of tabs relevant to the Role chosen in the Welcome window, and SHALL show an explanation of that tab's purpose alongside each one, without requiring the user to manually navigate tabs to see the walkthrough.

#### Scenario: Tutorial starts automatically after Welcome
- **WHEN** the user completes the Welcome window
- **THEN** the guided tutorial begins immediately, opening the main window if it is not already open

#### Scenario: Tutorial advances through tabs on its own
- **WHEN** the guided tutorial is active and the user advances to the next step
- **THEN** the main window's active tab switches to the next tab in the tutorial's sequence and shows that tab's explanation

#### Scenario: User can exit the tutorial early
- **WHEN** the guided tutorial is active and the user dismisses it
- **THEN** the tutorial closes without switching any further tabs, and the current Role's tutorial is marked as seen the same as if it had been completed

### Requirement: Tutorial completion is tracked independently per Role
The system SHALL track tutorial completion separately for Owner and Sub. The Owner tutorial SHALL run automatically the first time the local Role is ever set to Owner (including as part of the initial Welcome flow, if Owner was the Role chosen there), and the Sub tutorial SHALL run automatically the first time the local Role is ever set to Sub. Once a Role's tutorial has been shown, switching away from and back to that Role SHALL NOT automatically re-trigger its tutorial.

#### Scenario: First-ever switch to Owner triggers the Owner tutorial
- **WHEN** the local Role is changed to Owner for the first time ever on this install
- **THEN** the Owner-specific guided tutorial runs automatically

#### Scenario: First-ever switch to Sub triggers the Sub tutorial
- **WHEN** the local Role is changed to Sub for the first time ever on this install
- **THEN** the Sub-specific guided tutorial runs automatically

#### Scenario: Returning to a previously-seen Role does not replay its tutorial
- **WHEN** a user switches from Owner to Sub and back to Owner, and the Owner tutorial has already been shown once
- **THEN** switching back to Owner does not automatically reopen the Owner tutorial

#### Scenario: Each Role's tutorial content matches that Role's tabs
- **WHEN** the Owner tutorial runs
- **THEN** it walks through the Owner-role view of each shared category tab; and when the Sub tutorial runs, it walks through the Sub-role alias-authoring view of each shared category tab instead

### Requirement: Settings offers a control to rerun the current Role's tutorial
SettingsWindow's Identity & Pairing tab SHALL offer a "Rerun Tutorial" control that replays the guided tutorial for the currently active Role on demand, regardless of whether that Role's tutorial has been seen before. Using this control SHALL NOT change the other Role's own first-run tutorial tracking.

#### Scenario: User reruns the tutorial from Settings
- **WHEN** the user selects "Rerun Tutorial" in Settings' Identity & Pairing tab
- **THEN** the guided tutorial for the currently active Role runs again from its first step

#### Scenario: Rerunning one Role's tutorial does not affect the other Role's first-run state
- **WHEN** the user reruns the Owner tutorial from Settings while the Sub tutorial has never yet run
- **THEN** the Sub tutorial still runs automatically the first time Role is later switched to Sub
