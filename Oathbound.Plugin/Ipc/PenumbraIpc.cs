using System;
using System.Collections.Generic;
using Penumbra.Api.Enums;
using Penumbra.Api.IpcSubscribers;

namespace Oathbound.Plugin.Ipc;

/// Guarded PoseKit-style Penumbra surface. All writes are temporary and source-scoped.
public sealed class PenumbraIpc
{
    private const string Source = "Oathbound";
    private readonly GetModList getModList = new(Plugin.PluginInterface);
    private readonly GetModPath getModPath = new(Plugin.PluginInterface);
    private readonly GetModDirectory getModDirectory = new(Plugin.PluginInterface);
    private readonly GetChangedItems getChangedItems = new(Plugin.PluginInterface);
    private readonly GetCollectionForObject getCollectionForObject = new(Plugin.PluginInterface);
    private readonly GetCurrentModSettings getCurrentModSettings = new(Plugin.PluginInterface);
    private readonly SetTemporaryModSettings setTemporaryModSettings = new(Plugin.PluginInterface);
    private readonly RemoveTemporaryModSettings removeTemporaryModSettings = new(Plugin.PluginInterface);
    private readonly RedrawObject redrawObject = new(Plugin.PluginInterface);

    public Dictionary<string, string>? TryGetModList() { try { return getModList.Invoke(); } catch { return null; } }
    public string? TryGetModDirectory() { try { return getModDirectory.Invoke(); } catch { return null; } }
    public string? TryGetModPath(string directory, string name)
    {
        try { var (ec, path, _, _) = getModPath.Invoke(directory, name); return ec == PenumbraApiEc.Success ? path : null; }
        catch { return null; }
    }
    public IReadOnlySet<uint> TryGetChangedItemIds(string directory, string name)
    {
        try
        {
            var raw = getChangedItems.Invoke(directory, name);
            var json = System.Text.Json.JsonSerializer.Serialize(raw);
            using var document = System.Text.Json.JsonDocument.Parse(json);
            var result = new HashSet<uint>();
            CollectItemIds(document.RootElement, result);
            return result;
        }
        catch { return new HashSet<uint>(); }
    }

    private static void CollectItemIds(System.Text.Json.JsonElement element, HashSet<uint> result)
    {
        if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("ItemId") || property.NameEquals("itemId"))
                    if (property.Value.TryGetUInt32(out var id) && id > 0) result.Add(id);
                CollectItemIds(property.Value, result);
            }
        }
        else if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) CollectItemIds(child, result);
    }
    public Guid? TryGetLocalPlayerCollectionId()
    {
        try
        {
            var (valid, _, collection) = getCollectionForObject.Invoke(0);
            if (!valid)
            {
                Plugin.Log.Warning("Penumbra: could not resolve the local player's effective collection (GetCollectionForObject reported invalid).");
                return null;
            }
            return collection.Id;
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "Failed to resolve the local player's Penumbra collection."); return null; }
    }
    public (bool Enabled, Dictionary<string, List<string>>? Selections) TryGetCurrentSettings(Guid collection, string directory)
    {
        try { var (_, settings) = getCurrentModSettings.Invoke(collection, directory); return settings is { } s ? (s.Item1, s.Item3) : (false, null); }
        catch { return (false, null); }
    }
    public bool TrySetTemporarySettings(Guid collection, string directory, IReadOnlyDictionary<string, IReadOnlyList<string>> selections)
    {
        try
        {
            var ec = setTemporaryModSettings.Invoke(collection, directory, false, true, 0, selections, Source);
            if (ec != PenumbraApiEc.Success)
            {
                Plugin.Log.Warning($"Penumbra: failed to apply temporary settings for mod \"{directory}\": {ec}.");
                return false;
            }
            return true;
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "Failed to apply temporary gesture mod settings."); return false; }
    }
    public bool TryRemoveTemporarySettings(Guid collection, string directory)
    {
        try
        {
            var ec = removeTemporaryModSettings.Invoke(collection, directory);
            if (ec != PenumbraApiEc.Success)
            {
                Plugin.Log.Warning($"Penumbra: failed to remove temporary settings for mod \"{directory}\": {ec}.");
                return false;
            }
            return true;
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "Failed to remove temporary gesture mod settings."); return false; }
    }
    public bool TryRedrawLocalPlayer()
    {
        try { redrawObject.Invoke(0); return true; }
        catch (Exception ex) { Plugin.Log.Error(ex, "Failed to redraw the local player after a temporary Penumbra activation."); return false; }
    }
}
