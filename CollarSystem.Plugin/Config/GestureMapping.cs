using System;
using System.Collections.Generic;

namespace CollarSystem.Plugin.Config;

/// One installed Penumbra mod, and the emote name(s) it resolves to (auto-resolved, or manually assigned
/// as a fallback for mods GetChangedItems could not tag - see collar/gesture's "unresolved" scenario).
[Serializable]
public class GestureCatalogEntry
{
    public string ModDirectory { get; set; } = "";
    public string ModName { get; set; } = "";
    public List<string> EmoteNames { get; set; } = new();
    public bool IsManualAssignment { get; set; }
}

[Serializable]
public class GestureMapping
{
    /// Sub-side: the catalog this Sub's own scan produced, keyed by mod directory. Local-only under the
    /// chat transport - there is no live channel to push this to an Owner, so the Sub assigns alias names
    /// against it locally and tells the Owner what they are (collar/gesture's "Catalog shared with paired
    /// Owner" requirement was removed for exactly this reason).
    public Dictionary<string, GestureCatalogEntry> LocalCatalog { get; set; } = new();
}
