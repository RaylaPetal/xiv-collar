using System;
using System.Collections.Generic;
using System.Linq;
using CollarSystem.Plugin.Config;
using Penumbra.Api.IpcSubscribers;

namespace CollarSystem.Plugin.Ipc;

/// Thin wrapper around the Penumbra.Api calls collar/gesture needs: mod/emote catalog scanning and
/// activating a mod for the local player so its animation takes effect before the emote fires.
public sealed class PenumbraIpc
{
    private const int LocalPlayerObjectIndex = 0;
    private const string EmoteChangedItemPrefix = "Emote: ";

    private readonly GetModList getModList;
    private readonly GetModPath getModPath;
    private readonly GetChangedItems getChangedItems;
    private readonly GetCollectionForObject getCollectionForObject;
    private readonly TrySetMod trySetMod;

    public PenumbraIpc()
    {
        getModList = new GetModList(Plugin.PluginInterface);
        getModPath = new GetModPath(Plugin.PluginInterface);
        getChangedItems = new GetChangedItems(Plugin.PluginInterface);
        getCollectionForObject = new GetCollectionForObject(Plugin.PluginInterface);
        trySetMod = new TrySetMod(Plugin.PluginInterface);
    }

    /// Scans every installed mod, resolves each to the emote(s) it affects using Penumbra's own
    /// identification (no manual tagging - collar/gesture's "Automatic gesture catalog" requirement),
    /// and keeps only mods whose Penumbra sort-folder path falls under one of `folderAllowlist`.
    /// A mod with changed items but none recognized as an emote is returned unresolved (empty EmoteNames)
    /// rather than omitted, so the caller can offer manual assignment.
    public IReadOnlyList<GestureCatalogEntry> ScanGestureMods(IReadOnlyList<string> folderAllowlist)
    {
        var results = new List<GestureCatalogEntry>();
        foreach (var (modDirectory, modName) in getModList.Invoke())
        {
            var (ec, sortPath, _, _) = getModPath.Invoke(modDirectory, modName);
            if (ec != Penumbra.Api.Enums.PenumbraApiEc.Success)
                continue;

            if (folderAllowlist.Count > 0 && !folderAllowlist.Any(folder => IsUnderFolder(sortPath, folder)))
                continue;

            var changedItems = getChangedItems.Invoke(modDirectory, modName);
            var emoteNames = changedItems.Keys
                .Where(key => key.StartsWith(EmoteChangedItemPrefix, StringComparison.Ordinal))
                .Select(key => key[EmoteChangedItemPrefix.Length..])
                .Distinct()
                .ToList();

            if (changedItems.Count == 0)
                continue;

            results.Add(new GestureCatalogEntry
            {
                ModDirectory = modDirectory,
                ModName = modName,
                EmoteNames = emoteNames,
            });
        }

        return results;
    }

    private static bool IsUnderFolder(string sortPath, string folder) =>
        sortPath.StartsWith(folder.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);

    /// Ensures the mapped mod is enabled in whatever collection currently governs the local player,
    /// so its animation redirect is live before the emote command fires (design.md's "Trigger" step).
    public bool ActivateModForLocalPlayer(string modDirectory, string modName)
    {
        var (objectValid, _, effectiveCollection) = getCollectionForObject.Invoke(LocalPlayerObjectIndex);
        if (!objectValid)
            return false;

        var ec = trySetMod.Invoke(effectiveCollection.Id, modDirectory, true, modName);
        return ec is Penumbra.Api.Enums.PenumbraApiEc.Success or Penumbra.Api.Enums.PenumbraApiEc.NothingChanged;
    }
}
