using System;
using System.Collections.Generic;
using System.Linq;
using Oathbound.Plugin.Ipc;

namespace Oathbound.Plugin.Commands;

/// Layers Oathbound's temporary Penumbra claims per collection/mod. Releasing the top claim restores the
/// next claim instead of blindly deleting another feature's active override.
///
/// While a claim is held, Oathbound keeps control of the mod: the settings are locked (see PenumbraIpc),
/// and if they're dropped or changed anyway - Penumbra removes even locked temporary settings when a mod's
/// structure changes - the top claim is put back on the next frame.
public sealed class TemporaryModSettingsCoordinator : IDisposable
{
    private sealed record Claim(string Owner, Dictionary<string, IReadOnlyList<string>> Selections);

    /// Minimum gap between two re-asserts of the same mod, so a fight with something else rewriting it
    /// can't turn into a per-frame loop.
    private const long ReassertCooldownMs = 1000;

    private readonly Dictionary<(Guid Collection, string Mod), List<Claim>> claims = new();
    private readonly Dictionary<(Guid Collection, string Mod), long> lastReassert = new();
    private readonly HashSet<(Guid Collection, string Mod)> pendingReassert = new();
    private readonly PenumbraIpc penumbra;

    /// True while Oathbound itself is writing - the change events that write fires are its own, not a loss.
    private bool writing;

    public TemporaryModSettingsCoordinator(PenumbraIpc penumbra)
    {
        this.penumbra = penumbra;
        penumbra.SettingChanged += OnSettingChanged;
    }

    public bool Acquire(string owner, Guid collection, string mod, IReadOnlyDictionary<string, IReadOnlyList<string>> selections)
    {
        var key = (collection, mod);
        if (!Set(collection, mod, selections)) return false;
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
            lastReassert.Remove(key);
            return Remove(collection, mod);
        }
        return Set(collection, mod, layers[^1].Selections);
    }

    /// Releases every claim still held - on unload, since a locked setting can't be removed by anything but
    /// Oathbound's own key and would otherwise stay stuck until the game restarts.
    public void Dispose()
    {
        penumbra.SettingChanged -= OnSettingChanged;
        foreach (var (collection, mod) in claims.Keys.ToList())
            Remove(collection, mod);
        claims.Clear();
    }

    private void OnSettingChanged(Guid collection, string mod)
    {
        var key = (collection, mod);
        if (writing || !claims.ContainsKey(key) || !pendingReassert.Add(key))
            return;
        // Not inline: this fires from inside Penumbra's own change handling.
        Plugin.Framework.RunOnTick(() => Reassert(key));
    }

    private void Reassert((Guid Collection, string Mod) key)
    {
        pendingReassert.Remove(key);
        if (!claims.TryGetValue(key, out var layers) || layers.Count == 0)
            return;

        var top = layers[^1];
        if (penumbra.IsHeld(key.Collection, key.Mod, top.Selections))
            return;

        var now = Environment.TickCount64;
        if (lastReassert.TryGetValue(key, out var last) && now - last < ReassertCooldownMs)
        {
            // Still wanted, just too soon - try again once the cooldown is over instead of dropping it.
            if (pendingReassert.Add(key))
                Plugin.Framework.RunOnTick(() => Reassert(key), TimeSpan.FromMilliseconds(ReassertCooldownMs));
            return;
        }
        lastReassert[key] = now;
        var ok = Set(key.Collection, key.Mod, top.Selections);
        Plugin.Log.Information($"Temporary settings for mod \"{key.Mod}\" changed outside Oathbound while held by \"{top.Owner}\" - {(ok ? "re-applied" : "could not re-apply")}.");
    }

    private bool Set(Guid collection, string mod, IReadOnlyDictionary<string, IReadOnlyList<string>> selections)
    {
        writing = true;
        try { return penumbra.TrySetTemporarySettings(collection, mod, selections); }
        finally { writing = false; }
    }

    private bool Remove(Guid collection, string mod)
    {
        writing = true;
        try { return penumbra.TryRemoveTemporarySettings(collection, mod); }
        finally { writing = false; }
    }
}
