## MODIFIED Requirements

### Requirement: Restraint device captured from a single equipped gear piece
The system SHALL let the Sub capture a restraint device by picking one of the lockable equipment slots and then picking an item for that slot from a searchable item picker, rather than by equipping the item first and reading it back from live game state, and rather than referencing a whole Glamourer design. The picker SHALL let the Sub choose from every item valid for the chosen slot, not only items the Sub currently owns or has equipped. The Sub SHALL name the device at capture time. A captured device SHALL be immediately available for rule assignment and export - there is no separate "scan" step and no untagged/tagged distinction.

#### Scenario: Sub captures a device from an equipped item
- **WHEN** the Sub picks a slot and picks an item for it from the picker, and captures it as a new restraint device with a name
- **THEN** the device is saved with that slot and item (undyed), and is immediately available to assign rules to, alias, and export

#### Scenario: Capturing a device does not require scanning a design library
- **WHEN** the Sub opens the Restraints tab
- **THEN** capturing a new device only requires picking a slot and an item from the picker, with no prior scan of saved Glamourer designs and no requirement that the item be currently equipped or owned

#### Scenario: Applying a captured device sets only its own slot
- **WHEN** a restraint device captured from a single slot is applied
- **THEN** only that one equipment slot changes, and every other slot remains exactly as free to edit as if no device were active

## ADDED Requirements

### Requirement: Owner-authored ad-hoc restraint device
The system SHALL let the Owner define a restraint device's slot and item directly, using the same slot-and-item picker the Sub's own capture flow uses, without requiring the Sub to have captured, named, or shared the name of that device beforehand. The Owner SHALL give the ad-hoc device a local label for their own reference and SHALL assign it restriction rules from the same fixed rule set Sub-captured devices use. Sending an Owner-authored ad-hoc device to the paired Sub SHALL carry the full slot, item, and rule definition in the command itself, and SHALL apply and release using the same force-apply/force-release override precedence as a name-referenced quick command.

#### Scenario: Owner defines and sends an ad-hoc device
- **WHEN** the Owner picks a slot and an item from the picker, assigns one or more restriction rules, and sends it
- **THEN** the paired Sub's client equips that slot with that item and activates exactly the assigned rules, with no lookup of any Sub-side captured device by name

#### Scenario: Ad-hoc device follows the same force-release precedence
- **WHEN** the Owner has sent an ad-hoc device and the Sub attempts to remove it through their own controls
- **THEN** the device remains active and its rules stay in effect, the same as an Owner-forced name-referenced device

#### Scenario: Ad-hoc device conflicts are checked the same as any other device
- **WHEN** an Owner-authored ad-hoc device's assigned rules would conflict with an already-active rule from a different device
- **THEN** the ad-hoc device's apply is refused and the existing active rule remains unchanged, the same as a conflict between two name-referenced devices
