using System;
using System.Collections.Generic;
using System.Numerics;

namespace CollarSystem.Plugin.Config;

/// Sub-side alias definitions (collar/chat-transport's "Alias resolution against a locally-defined
/// dictionary") - what each short alias actually does. Never transmitted; only the alias name crosses
/// chat. The Owner only ever needs to know the alias *name* (shared out of band), never these definitions.

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
    public uint Key { get; set; }
    public bool Locked { get; set; }
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

/// Follow has no Sub-defined content (only engage/release exist) - just the two trigger words themselves,
/// which the Sub can still rename for their own comfort.
[Serializable]
public class FollowAliasWords
{
    public string EngageAlias { get; set; } = "leash-on";
    public string ReleaseAlias { get; set; } = "leash-off";
}

[Serializable]
public class AliasBook
{
    public List<TitleAliasDefinition> Titles { get; set; } = new();
    public string ClearTitleAlias { get; set; } = "clear-title";

    public List<OutfitAliasDefinition> Outfits { get; set; } = new();

    /// Unlocks using whatever key was last used to lock (SubRuntimeState.OutfitLockKey) - the Sub never
    /// has to type or remember a key to release their own outfit.
    public string UnlockOutfitAlias { get; set; } = "unlock";

    public List<GestureAliasDefinition> Gestures { get; set; } = new();
    public FollowAliasWords Follow { get; set; } = new();
}
