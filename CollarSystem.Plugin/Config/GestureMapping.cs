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
    /// Sub-side: the catalog this Sub's own scan produced, keyed by mod directory.
    public Dictionary<string, GestureCatalogEntry> LocalCatalog { get; set; } = new();

    /// Owner-side: the last catalog a paired Sub relayed, cached for offline browsing.
    public List<GestureCatalogEntry> CachedPeerCatalog { get; set; } = new();
}
