using System;
using System.Collections.Generic;

namespace CollarSystem.Plugin.Config;

/// One of the Sub's own saved Moodles presets (collar/moodles). Moodles presets have no folder/category
/// organization the way Penumbra mods or Glamourer designs do, so unlike GestureMapping/WardrobeMapping
/// there is no allowlist to scope scanning - every saved preset is eligible, matching how Moodles itself
/// presents them as one flat list.
[Serializable]
public class MoodlesPresetEntry
{
    public string PresetId { get; set; } = "";
    public string Name { get; set; } = "";
}

[Serializable]
public class MoodlesMapping
{
    /// Sub-side: the preset catalog this Sub's own scan produced, keyed by preset id. Local-only, same
    /// reasoning as GestureMapping.LocalCatalog - the Owner only ever learns preset names via the Sub's
    /// own "Copy names" export, never a live push.
    public Dictionary<string, MoodlesPresetEntry> LocalCatalog { get; set; } = new();
}
