using System;
using System.Collections.Generic;
using System.Numerics;

namespace Oathbound.Plugin.Config;

/// Sub-side alias definitions (collar/chat-transport's "Alias resolution against a locally-defined
/// dictionary") - what each short alias actually does. Never transmitted over chat - only the alias name
/// crosses the wire during live commanding, resolved locally against this dictionary on the Sub's own
/// client. The Sub's Scan & Export file separately carries a human-readable summary of what each alias
/// does (see CatalogSyncService's Aliases export/import), so an Owner who imports it can see what an entry
/// actually does before sending it - a deliberate choice, not an oversight; see collar/catalog-sync.

[Serializable]
public class TitleAliasDefinition
{
    public string Alias { get; set; } = "";
    public string Text { get; set; } = "";
    public bool IsPrefix { get; set; }
    public Vector3 Color { get; set; } = new(1, 1, 1);
}

[Serializable]
public class OutfitAliasDefinition
{
    public string Alias { get; set; } = "";
    public Guid DesignId { get; set; }

    /// Display only, so the Sub can recognize the entry in Settings - not used for matching.
    public string DesignName { get; set; } = "";
    public bool Locked { get; set; }
}

[Serializable]
public class RestraintAliasDefinition
{
    public string Alias { get; set; } = "";
    public string DeviceId { get; set; } = "";

    /// Display only, so the Sub can recognize the entry in Settings - not used for matching.
    public string DeviceName { get; set; } = "";
}

[Serializable]
public class GestureAliasDefinition
{
    public string Alias { get; set; } = "";
    public string GestureId { get; set; } = "";
    public string AnimationName { get; set; } = "";
    public string ModDirectory { get; set; } = "";
    public string ModName { get; set; } = "";
    public string EmoteName { get; set; } = "";
}

[Serializable]
public class MoodlesAliasDefinition
{
    public string Alias { get; set; } = "";
    public string StatusId { get; set; } = "";

    /// Display only, so the Sub can recognize the entry in Settings - not used for matching.
    public string StatusName { get; set; } = "";
}

/// Follow has no Sub-defined content (only engage/release exist) - just the two trigger words themselves,
/// which the Sub can still rename for their own comfort.
[Serializable]
public class FollowAliasWords
{
    public string EngageAlias { get; set; } = "leash";
    public string ReleaseAlias { get; set; } = "unleash";
}

/// collar/custom-triggers: the fixed set of action kinds a Custom Trigger's bundle may carry.
public enum CustomTriggerActionKind
{
    Title,
    Outfit,
    Gesture,
    Moodle,
    Restraint,
    Chat,
}

/// One action within a Custom Trigger's bundle. Mirrors RestraintRuleAssignment's own "Kind + only the
/// fields that kind uses" shape rather than a class hierarchy - every field below is ignored by every kind
/// that doesn't use it.
[Serializable]
public class CustomTriggerAction
{
    public CustomTriggerActionKind Kind { get; set; }

    // Title
    public string TitleText { get; set; } = "";
    public bool TitleIsPrefix { get; set; }
    public Vector3 TitleColor { get; set; } = new(1, 1, 1);

    // Outfit
    public Guid OutfitDesignId { get; set; }
    public string OutfitDesignName { get; set; } = "";

    // Gesture
    public string GestureId { get; set; } = "";
    public string GestureAnimationName { get; set; } = "";

    // Moodle
    public string MoodleStatusId { get; set; } = "";
    public string MoodleStatusName { get; set; } = "";

    // Restraint
    public string RestraintDeviceId { get; set; } = "";
    public string RestraintDeviceName { get; set; } = "";

    // Chat - collar/custom-triggers "Sending a chat message requires its own dedicated permission and
    // acknowledgement": sent verbatim, any channel, any text - gated at apply time, never here.
    public string ChatText { get; set; } = "";
}

/// collar/custom-triggers: a Sub-defined bundle of actions fired together as one alias, resolved through
/// the same alias dictionary every other category already uses. Only the alias name crosses the wire
/// during live commanding - the Owner's Scan & Export import additionally shows a summary of the bundle's
/// actions for their own reference, the same as every other category (see AliasBook's own doc comment).
[Serializable]
public class CustomTriggerDefinition
{
    public string Alias { get; set; } = "";
    public List<CustomTriggerAction> Actions { get; set; } = new();
}

[Serializable]
public class AliasBook
{
    public List<TitleAliasDefinition> Titles { get; set; } = new();
    public string ClearTitleAlias { get; set; } = "clear-title";

    public List<OutfitAliasDefinition> Outfits { get; set; } = new();

    /// Releases whichever slots the currently-locked outfit design claimed (SlotLockManager) - the Sub
    /// never has to type or remember a key to release their own outfit.
    public string UnlockOutfitAlias { get; set; } = "unlock";

    public List<GestureAliasDefinition> Gestures { get; set; } = new();
    public FollowAliasWords Follow { get; set; } = new();

    public List<MoodlesAliasDefinition> Moodles { get; set; } = new();

    /// Removes the Sub's currently active Moodle - the same "one dedicated clear word" shape
    /// ClearTitleAlias/UnlockOutfitAlias already use.
    public string ClearMoodleAlias { get; set; } = "clear-moodle";

    /// Unlike Outfit's single "one design locked at a time" + shared Unlock alias, multiple restraint
    /// devices can be active at once (collar/restraints), so each device's alias toggles that one device:
    /// applies it if not currently active, releases it (and only its own rules) if it is.
    public List<RestraintAliasDefinition> Restraints { get; set; } = new();

    public List<CustomTriggerDefinition> CustomTriggers { get; set; } = new();
}
