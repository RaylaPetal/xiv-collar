using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oathbound.Plugin.Config;

/// One of the Sub's own individual Moodles statuses (buffs/debuffs) - collar/moodles reads these directly
/// rather than bundled presets, so the Owner can apply/clear a single status. Moodles statuses have no
/// folder/category organization the way Penumbra mods or Glamourer designs do, so unlike
/// GestureMapping/WardrobeMapping there is no allowlist to scope scanning - every registered status is
/// eligible, matching how Moodles itself presents them as one flat list.
[Serializable]
public class MoodlesStatusEntry
{
    public string StatusId { get; set; } = "";
    public string Name { get; set; } = "";
}

[Serializable]
public class MoodlesMapping
{
    /// Sub-side: the status catalog this Sub's own scan produced, keyed by status id. Local-only, same
    /// reasoning as GestureMapping.LocalCatalog - the Owner only ever learns status names via the Sub's
    /// own "Copy names" export, never a live push.
    ///
    /// collar/config-performance "Catalogs live outside the hot-saved config file": [JsonIgnore]d and
    /// persisted separately by CatalogStore - see GestureMapping's equivalent comment.
    [JsonIgnore]
    public Dictionary<string, MoodlesStatusEntry> LocalCatalog { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? LegacyExtensionData { get; set; }
}
