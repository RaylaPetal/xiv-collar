using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;

namespace CollarSystem.Plugin.Config;

public enum PluginRole
{
    Owner,
    Sub,
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

    public bool IsPaired => Paired && !string.IsNullOrWhiteSpace(PeerName) && !string.IsNullOrWhiteSpace(PeerWorld);
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
}

/// The Sub's configured collar item (collar/collaring) - a single Neck-slot item, captured from whatever
/// the Sub currently has equipped (see GlamourerIpc.GetCurrentNeckItem), never typed in as a raw item id.
/// The lock itself (key, force-locked flag) lives in SubRuntimeState, not here - same in-memory-only
/// precedent as the existing outfit lock (OutfitLockKey/OutfitForceLocked), applied fresh each time rather
/// than persisted.
[Serializable]
public class CollarState
{
    public ulong? ItemId { get; set; }
    public byte Stain { get; set; }
    public byte Stain2 { get; set; }

    public bool IsConfigured => ItemId is not null;
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
    public MoodlesMapping MoodlesMapping { get; set; } = new();

    /// Sub-side: the Sub's configured collar item (collar/collaring). See CollarState.
    public CollarState Collar { get; set; } = new();

    /// Owner-side only in practice (a Sub has no use for their own names here) - see OwnerQuickCommands.
    public OwnerQuickCommands QuickCommands { get; set; } = new();

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

    /// `/collarpanic <word>` only triggers if `<word>` matches this (case-insensitive) - the actual
    /// safeword mechanic: no visible button to hit by accident or under someone else's eye, just a typed
    /// word like any other safeword convention. Empty/unset means no safeword configured, in which case
    /// `/collarpanic` (with or without any argument) still triggers unconditionally - an unconfigured
    /// safeword must never become the reason panic stops working.
    public string? PanicSafeword { get; set; }

    /// Legacy folder allowlist, retained only to seed the explicit mod picker during migration.
    public List<string> GestureFolderAllowlist { get; set; } = new();

    /// PoseKit-style explicit Penumbra mod directories to inspect for animation options.
    public HashSet<string> SelectedGestureMods { get; set; } = new();

    /// Non-mutating convenience filter for the explicit mod picker.
    public string GestureModFolderFilter { get; set; } = "";

    /// Sub-side: Glamourer design-browser folders that wardrobe scanning is scoped to - same allowlist
    /// pattern as GestureFolderAllowlist, applied to `Glamourer.GetDesignListExtended`'s FullPath.
    public List<string> WardrobeFolderAllowlist { get; set; } = new();

    /// Gate per collar/gesture and collar/follow's ToS-disclosure requirement: the Sub must acknowledge
    /// the automation-risk caveat before either permission can be enabled.
    public bool TosAcknowledged { get; set; }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
