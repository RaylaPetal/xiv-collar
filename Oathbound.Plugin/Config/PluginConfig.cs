using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;
using Glamourer.Api.Enums;

namespace Oathbound.Plugin.Config;

public enum PluginRole
{
    Owner,
    Sub,
}

/// collar/ui-organization "A movable on-screen button opens the quick-access favorites menu".
public enum ScreenCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

/// Where the on-screen quick-access button sits - a corner preset plus a pixel margin from it, rather than
/// free drag placement (design.md's "Alternative considered": simpler to persist/validate, and matches how
/// the DTR bar itself is positioned by Dalamud, not dragged by this plugin).
[Serializable]
public class FavoritesButtonSettings
{
    public ScreenCorner Corner { get; set; } = ScreenCorner.BottomRight;
    public Vector2 Margin { get; set; } = new(16, 16);
}

/// collar/pairing's manual code handshake. `MyCode` is generated once per install and shared out of band
/// (voice, DM, etc); `PeerCode` is the other side's code, entered the same way. Neither code is ever
/// checked against ongoing trigger tells - they only gate the one-time pairing handshake message (see
/// ChatCommandListener), which is what actually populates `PeerName`/`PeerWorld` from FFXIV's own
/// server-verified sender field. `Paired` is the explicit accept action on that handshake - never
/// auto-enabled by receiving a correctly-coded message alone.
[Serializable]
public class PairingState
{
    public string MyCode { get; set; } = CodeGenerator.Generate();
    public string? PeerCode { get; set; }
    public string? PeerName { get; set; }
    public string? PeerWorld { get; set; }
    public bool Paired { get; set; }

    /// collar/chat-transport: the peer's own trigger phrase, captured from their handshake message (see
    /// ChatCommandListener.TryHandlePairingMessage) so composing a command to them never needs to manually
    /// match their independently-configured trigger phrase. Null for a pairing formed before this field
    /// existed, or with a peer whose handshake didn't declare one (an older client) - ChatComposer falls
    /// back to this side's own TriggerPhrase in that case.
    public string? PeerTriggerPhrase { get; set; }

    public bool IsPaired => Paired && !string.IsNullOrWhiteSpace(PeerName) && !string.IsNullOrWhiteSpace(PeerWorld);
}

/// collar/catalog-sync: where a `QuickCommand` came from - lets reset-imports (see CollarWindow's
/// "Reset imports" button) remove only entries an import added, once a category's list can mix
/// manually-added and imported entries together. Defaults to `Manual` so every pre-existing entry
/// (serialized before this field existed) deserializes as not-imported, matching design.md's Migration
/// Plan - nothing already saved is wrongly swept by the new per-category filtering; it starts being
/// tracked correctly from the next import onward. No separate `Scanned` state: only the Sub scans, never
/// the Owner - every populated OwnerQuickCommands entry is either typed by the Owner (`Manual`) or came
/// from a Sub-exported file via "Import commands" (`Imported`).
public enum ImportSource
{
    Manual,
    Imported,
}

/// Owner-side: a saved, one-click command - `Command` is the same raw text ChatComposer appends after the
/// trigger phrase (a plain alias, or a "title"/"outfit"/"gesture" override). Never authoritative -
/// ChatCommandListener still matches/validates everything against the Sub's own live state when a command
/// actually arrives, so a stale entry here just fails silently, it can't apply anything wrong.
[Serializable]
public class QuickCommand
{
    public string Label { get; set; } = "";
    public string Command { get; set; } = "";

    /// collar/catalog-sync "Owner can reset every import to a blank slate": see ImportSource.
    public ImportSource Source { get; set; } = ImportSource.Manual;

    /// collar/restraints only: the Owner-assigned restriction rules for this restraint quick command, kept
    /// in sync with the encoded suffix in `Command` (see CollarWindow's restraint rule editor). Null or
    /// empty means the Owner hasn't configured this entry yet - it can't be sent until they do.
    public List<RestraintRuleAssignment>? RestraintRules { get; set; }

    /// collar/gesture only: the source mod/group names and their manifest order, carried through from the
    /// Sub's export, so the Owner's Gesture quick-command list can group and order entries the same way the
    /// animation picker does (numerically, e.g. option 1..400, rather than alphabetically as text - "10"
    /// sorts before "2" as a string). Null/0 for entries imported before these fields existed - those fall
    /// back to an "Ungrouped" bucket, alphabetically ordered, until re-imported.
    public string? GestureModName { get; set; }
    public string? GestureGroupName { get; set; }
    public int GestureGroupOrder { get; set; }
    public int GestureOptionOrder { get; set; }

    /// collar/title only: the Owner-chosen prefix/color for this title quick command, kept in sync with the
    /// encoded `title style ...` command (see CollarWindow's title quick-command editor) - `Command` remains
    /// the actual source of truth sent over the wire; these exist purely so the UI can display/reconstruct
    /// the chosen style without re-parsing it. Null color means a plain `title create <text>` command with
    /// no style at all.
    public bool TitleIsPrefix { get; set; }
    public Vector3? TitleColor { get; set; }

    /// collar/ui-organization "Owner can favorite quick commands for quick access": a plain flag, not a
    /// separate list - removing/renaming this entry in its own category list already removes/renames it
    /// everywhere it's referenced, so the favorites window just filters all seven lists by this field.
    public bool IsFavorite { get; set; }
}

/// Owner-side only in practice. Outfits/Gestures are normally auto-populated by "Add from clipboard" (one
/// QuickCommand per imported name) - see CollarWindow's Owner tab - so there's a ready, one-click button
/// per name without an extra manual "save" step. Titles/Follow/Aliases are built one at a time since
/// there's nothing to bulk-import for freeform title text or arbitrary alias words - Follow in particular
/// has no direct-override syntax the way Title/Outfit/Gesture do (see ChatCommandListener's reserved
/// words), so a Follow entry is always a plain alias like any other.
[Serializable]
public class OwnerQuickCommands
{
    public List<QuickCommand> Titles { get; set; } = new();
    public List<QuickCommand> Outfits { get; set; } = new();
    public List<QuickCommand> Gestures { get; set; } = new();
    public List<QuickCommand> Follow { get; set; } = new();
    public List<QuickCommand> Moodles { get; set; } = new();
    public List<QuickCommand> Aliases { get; set; } = new();

    /// collar/restraints: Owner-side saved `restraint lock <name>` quick commands, one per scanned design
    /// name (tagged by the Sub or not) - same auto-populated-via-import pattern as Outfits/Moodles. Each
    /// entry needs its own QuickCommand.RestraintRules assigned by the Owner before it can be sent.
    public List<QuickCommand> Restraints { get; set; } = new();
}

/// The Sub's configured collar item (collar/collaring) - a single Neck-slot item, picked from a
/// Neck-locked ItemPickerWindow (see CollarCommand.ConfigureFromItem), never typed in as a raw item id.
/// Whether it's currently locked lives in SlotLockManager (collar/slot-locking), not here.
[Serializable]
public class CollarState
{
    public ulong? ItemId { get; set; }
    public byte Stain { get; set; }
    public byte Stain2 { get; set; }

    /// collar/collaring "Sub can optionally assign a Moodle to the collar" - independent of the Neck-slot
    /// item above, optional (both null when unassigned), applied/re-asserted/cleared alongside the collar's
    /// own lock lifecycle (CollarCommand), never through the ordinary Moodles alias/override path.
    public string? MoodleStatusId { get; set; }
    public string? MoodleStatusName { get; set; }

    public bool IsConfigured => ItemId is not null;
    public bool HasMoodleAssigned => MoodleStatusId is not null;
}

/// One category's (Collar/Outfit/future Restraints) claim on a single equipment slot (collar/slot-locking)
/// - persisted so SlotLockManager can resume enforcing it after a plugin reload without needing any
/// Glamourer-side key, unlike the whole-actor `Combination` lock this replaces.
[Serializable]
public class SlotLockEntry
{
    public ApiEquipSlot Slot { get; set; }
    public string Owner { get; set; } = "";
    public ulong ItemId { get; set; }
    public byte Stain { get; set; }
    public byte Stain2 { get; set; }
}

[Serializable]
public class PermissionSet
{
    public bool Title { get; set; }
    public bool Outfit { get; set; }
    public bool Gesture { get; set; }

    // Separate, higher-risk opt-in per collar/follow's spec - never implied by the other three.
    public bool Follow { get; set; }

    // collar/collaring and collar/moodles: same independent opt-in-per-category pattern as the four above.
    public bool Collar { get; set; }
    public bool Moodles { get; set; }

    // collar/restraints: same independent opt-in pattern - gates both Sub self-apply and the Owner's
    // force-apply override, same as every other category's permission flag.
    public bool Restraints { get; set; }

    /// collar/custom-triggers "Sending a chat message requires its own dedicated permission and
    /// acknowledgement": deliberately independent of every category above (including Gesture, whose own
    /// chat use is a closed set of self-targeting commands) - this permission alone lets a Custom Trigger's
    /// chat action send arbitrary text to any channel, so it needs its own opt-in, never implied by any
    /// other permission being on. See PluginConfig.CustomChatAcknowledged for the matching acknowledgement.
    public bool CustomChatMessages { get; set; }
}

/// collar/restraints: the fixed set of restriction rule kinds a restraint device may carry.
public enum RestraintRuleKind
{
    ForcedPose,
    WalkOnly,
    ActionBlock,
    GagChat,
    ArmsCuffed,
    LegsCuffed,
    FullBodyCuffed,
}

/// One restriction rule assigned to a device. `PoseModeId` only matters for ForcedPose (1=GroundSit,
/// 2=Sit, 3=Doze - the same EmoteModeId values GestureTrigger already uses). `AnimationId` only matters
/// for ArmsCuffed/LegsCuffed/FullBodyCuffed - a `GestureCatalogEntry.Id` (collar/gesture) identifying the
/// chosen animation to temporarily activate and hold for as long as the rule stays active. Both are
/// ignored by every rule kind that doesn't use them.
[Serializable]
public class RestraintRuleAssignment
{
    public RestraintRuleKind Kind { get; set; }
    public int PoseModeId { get; set; }
    public string? AnimationId { get; set; }
}

/// A single gear piece (collar/restraints) picked from a slot-and-item picker, generalized to any of the
/// 10 lockable slots - carrying one or more restriction rules. There is no separate scan/tag step: a
/// device is captured and named in one action (see RestraintCommand.CaptureDeviceFromItem).
[Serializable]
public class RestraintDeviceDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ApiEquipSlot Slot { get; set; }
    public ulong ItemId { get; set; }
    public byte Stain { get; set; }
    public byte Stain2 { get; set; }
    public string Name { get; set; } = "";
    public List<RestraintRuleAssignment> Rules { get; set; } = new();
}

/// Sub-side: the restraint device catalog, keyed by RestraintDeviceDefinition.Id. No scan step or
/// allowlist - each device is captured individually from whatever gear piece the Sub currently has
/// equipped (collar/restraints), the same way CollarState captures the collar item.
[Serializable]
public class RestraintMapping
{
    public Dictionary<string, RestraintDeviceDefinition> Devices { get; set; } = new();
}

[Serializable]
public class PluginConfig : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public PluginRole Role { get; set; } = PluginRole.Sub;

    public PairingState Pairing { get; set; } = new();
    public PermissionSet Permissions { get; set; } = new();
    public GestureMapping GestureMapping { get; set; } = new();
    public WardrobeMapping WardrobeMapping { get; set; } = new();
    public RestraintMapping RestraintMapping { get; set; } = new();
    public MoodlesMapping MoodlesMapping { get; set; } = new();

    /// Sub-side: the Sub's configured collar item (collar/collaring). See CollarState.
    public CollarState Collar { get; set; } = new();

    /// Every active per-slot lock (collar/slot-locking) - see SlotLockEntry and SlotLockManager.
    public List<SlotLockEntry> SlotLocks { get; set; } = new();

    /// Set by CollarCommand.ForceApply/OutfitCommand.ForceApply (the Owner's "joker" override). While
    /// true, the Sub's own alias-triggered Apply/Clear/Unlock for that category is refused - only the
    /// matching Force* release (or panic) can undo it. Plain bookkeeping, independent of the Glamourer
    /// lock model - unaffected by the move to per-slot locking.
    public bool OutfitForceLocked { get; set; }
    public bool CollarForceLocked { get; set; }
    public bool RestraintsForceLocked { get; set; }

    /// Owner-side only in practice (a Sub has no use for their own names here) - see OwnerQuickCommands.
    public OwnerQuickCommands QuickCommands { get; set; } = new();

    /// collar/ui-organization: persisted position of the on-screen quick-access favorites button.
    public FavoritesButtonSettings FavoritesButton { get; set; } = new();

    /// The phrase that precedes an alias in a trigger tell (collar/chat-transport). Both sides must agree
    /// on this - the Owner's composer and the Sub's parser both read it from their own local config, so
    /// changing it only takes effect for messages sent/parsed after the change.
    public string TriggerPhrase { get; set; } = "command";

    /// Sub-side: what each alias actually does. Never transmitted - only the alias name crosses chat.
    public AliasBook Aliases { get; set; } = new();

    /// The always-available local panic hotkey (collar/pairing). NO_KEY means "not bound" - the hotkey
    /// always triggers panic unconditionally (it's already a deliberate physical action, nothing to type),
    /// regardless of whether a PanicSafeword is set below.
    public VirtualKey PanicHotkey { get; set; } = VirtualKey.NO_KEY;

    /// `/oathboundpanic <word>` only triggers if `<word>` matches this (case-insensitive) - the actual
    /// safeword mechanic: no visible button to hit by accident or under someone else's eye, just a typed
    /// word like any other safeword convention. Empty/unset means no safeword configured, in which case
    /// `/oathboundpanic` (with or without any argument) still triggers unconditionally - an unconfigured
    /// safeword must never become the reason panic stops working.
    public string? PanicSafeword { get; set; }

    /// Legacy folder allowlist, retained only to seed the explicit mod picker during migration.
    public List<string> GestureFolderAllowlist { get; set; } = new();

    /// PoseKit-style Penumbra mod scope. Empty means every installed mod; entries restrict the scan.
    public HashSet<string> SelectedGestureMods { get; set; } = new();

    /// Non-mutating convenience filter for the explicit mod picker.
    public string GestureModFolderFilter { get; set; } = "";

    /// Sub-side: optional Glamourer design-browser folders applied to `Glamourer.GetDesignListExtended`'s
    /// FullPath. Empty means every saved design; entries restrict the scan.
    public List<string> WardrobeFolderAllowlist { get; set; } = new();

    /// Gate per collar/gesture and collar/follow's ToS-disclosure requirement: the Sub must acknowledge
    /// the automation-risk caveat before either permission can be enabled.
    public bool TosAcknowledged { get; set; }

    /// collar/custom-triggers "Sending a chat message requires its own dedicated permission and
    /// acknowledgement": deliberately separate from `TosAcknowledged` above - a Custom Trigger's chat
    /// action is a materially broader automation surface (arbitrary text, any channel) than anything the
    /// general acknowledgement was written to cover, so it gets its own explicit, dedicated checkbox rather
    /// than silently riding on the existing one.
    public bool CustomChatAcknowledged { get; set; }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
