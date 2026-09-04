using System;
using System.Collections.Generic;

namespace Oathbound.Plugin.Config;

/// One of a Sub's saved Glamourer designs, shared with a paired Owner - mirrors GestureCatalogEntry's
/// role for collar/gesture, applied to collar/outfit's design-selection flow.
[Serializable]
public class WardrobeDesignEntry
{
    public Guid DesignId { get; set; }
    public string Name { get; set; } = "";
}

[Serializable]
public class WardrobeMapping
{
    /// Sub-side: the Sub's own designs that fall under the configured folder allowlist. Local-only under
    /// the chat transport - the Sub picks one to name an outfit alias after, and tells the Owner the alias.
    public Dictionary<Guid, WardrobeDesignEntry> LocalDesigns { get; set; } = new();
}
