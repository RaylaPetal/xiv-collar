using System;
using System.Collections.Generic;
using System.Linq;
using Oathbound.Plugin.Ipc;

namespace Oathbound.Plugin.Commands;

/// Layers Oathbound's temporary Penumbra claims per collection/mod. Releasing the top claim restores the
/// next claim instead of blindly deleting another feature's active override.
public sealed class TemporaryModSettingsCoordinator
{
    private sealed record Claim(string Owner, Dictionary<string, IReadOnlyList<string>> Selections);
    private readonly Dictionary<(Guid Collection, string Mod), List<Claim>> claims = new();
    private readonly Func<Guid, string, IReadOnlyDictionary<string, IReadOnlyList<string>>, bool> set;
    private readonly Func<Guid, string, bool> remove;

    public TemporaryModSettingsCoordinator(PenumbraIpc penumbra)
        : this(penumbra.TrySetTemporarySettings, penumbra.TryRemoveTemporarySettings) { }

    public TemporaryModSettingsCoordinator(
        Func<Guid, string, IReadOnlyDictionary<string, IReadOnlyList<string>>, bool> set,
        Func<Guid, string, bool> remove)
    {
        this.set = set;
        this.remove = remove;
    }

    public bool Acquire(string owner, Guid collection, string mod, IReadOnlyDictionary<string, IReadOnlyList<string>> selections)
    {
        var key = (collection, mod);
        if (!set(collection, mod, selections)) return false;
        if (!claims.TryGetValue(key, out var layers)) claims[key] = layers = [];
        layers.RemoveAll(x => x.Owner == owner);
        layers.Add(new Claim(owner, selections.ToDictionary(x => x.Key, x => x.Value)));
        return true;
    }

    public bool Release(string owner, Guid collection, string mod)
    {
        var key = (collection, mod);
        if (!claims.TryGetValue(key, out var layers) || layers.RemoveAll(x => x.Owner == owner) == 0) return false;
        if (layers.Count == 0)
        {
            claims.Remove(key);
            return remove(collection, mod);
        }
        return set(collection, mod, layers[^1].Selections);
    }
}
