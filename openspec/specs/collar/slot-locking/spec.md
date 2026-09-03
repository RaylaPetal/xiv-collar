# collar/slot-locking Specification

## Purpose

Defines the shared per-slot Glamourer lock model that Collar, Outfit, and future action categories (e.g. Restraints) all build on, so that each category can lock only the equipment slot(s) it actually owns without ever restricting any slot it doesn't, and multiple such locks can be active at once without conflicting with one another.

## Requirements

### Requirement: Independent locks on different slots coexist
The system SHALL allow more than one slot lock, from different action categories, to be simultaneously active as long as each lock targets a distinct set of equipment slots. Establishing one category's slot lock SHALL NOT disturb, weaken, or require releasing any other category's currently active slot lock.

#### Scenario: Two categories lock different slots at the same time
- **WHEN** one action category has an active lock on one set of equipment slots
- **AND** a different action category locks a different, non-overlapping set of equipment slots
- **THEN** both locks remain active and enforced independently

### Requirement: A locked slot resists external changes without using Glamourer's own state lock
The system SHALL enforce a slot lock by detecting when the locked slot's actual equipped value diverges from the locked value and reapplying the locked value, and SHALL NOT rely on Glamourer's own actor-wide lock to prevent the change.

#### Scenario: An external change to a locked slot is reverted
- **WHEN** a slot is locked by this system
- **AND** that slot's equipped value is changed through Glamourer or another tool, outside this plugin's own release path
- **THEN** the system reapplies the locked value to that slot

### Requirement: Unlocked slots remain completely free to edit
The system SHALL NOT restrict, block, or otherwise interfere with any equipment slot that has no active lock from this system, regardless of how many other slots are currently locked by any category.

#### Scenario: A non-locked slot stays freely editable while other slots are locked
- **WHEN** one or more slots are actively locked by this system
- **AND** the Sub or another tool changes a different slot that has no active lock
- **THEN** that change succeeds without interference from this system

### Requirement: Releasing a lock affects only the slots it owns
The system SHALL, when a slot lock is released, stop enforcing exactly the slots that lock owned and SHALL NOT alter the lock state, enforcement, or current value of any other slot, whether locked by another category or unlocked.

#### Scenario: Releasing one category's lock leaves other active locks untouched
- **WHEN** two different action categories each have an active slot lock
- **AND** one category's lock is released
- **THEN** the released category's slots stop being enforced and the other category's lock remains fully active and enforced

### Requirement: A new lock is refused if it would overlap an existing lock from a different source
The system SHALL refuse to establish a new slot lock for any slot already locked by a different action category's active lock, and SHALL leave that existing lock and its enforced value unchanged. The action requesting the new lock SHALL report failure rather than silently locking a subset of its intended slots or overriding the existing lock.

#### Scenario: A conflicting lock request is refused
- **WHEN** one action category has an active lock on a given slot
- **AND** a different action category attempts to lock that same slot
- **THEN** the new lock request fails, the original lock remains active and enforced, and no slot ends up locked by both categories

### Requirement: Active slot locks survive a plugin reload
The system SHALL retain enough information about every active slot lock, across a plugin reload or game restart, that each lock can still be released (by its own category's normal release action or by panic) afterward.

#### Scenario: A plugin reload does not strand an active lock
- **WHEN** a slot lock is active and the plugin reloads or the game restarts
- **THEN** the lock is still recognized as active afterward and can still be released through its normal release action or panic
