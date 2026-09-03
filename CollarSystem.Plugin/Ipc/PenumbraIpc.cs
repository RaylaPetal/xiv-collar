using System;
using System.Collections.Generic;
using Penumbra.Api.Enums;
using Penumbra.Api.IpcSubscribers;

namespace CollarSystem.Plugin.Ipc;

/// Guarded PoseKit-style Penumbra surface. All writes are temporary and source-scoped.
public sealed class PenumbraIpc
{
    private const string Source = "CollarSystem";
    private readonly GetModList getModList = new(Plugin.PluginInterface);
    private readonly GetModPath getModPath = new(Plugin.PluginInterface);
    private readonly GetModDirectory getModDirectory = new(Plugin.PluginInterface);
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
    public Guid? TryGetLocalPlayerCollectionId()
    {
        try { var (valid, _, collection) = getCollectionForObject.Invoke(0); return valid ? collection.Id : null; }
        catch { return null; }
    }
    public (bool Enabled, Dictionary<string, List<string>>? Selections) TryGetCurrentSettings(Guid collection, string directory)
    {
        try { var (_, settings) = getCurrentModSettings.Invoke(collection, directory); return settings is { } s ? (s.Item1, s.Item3) : (false, null); }
        catch { return (false, null); }
    }
    public bool TrySetTemporarySettings(Guid collection, string directory, IReadOnlyDictionary<string, IReadOnlyList<string>> selections)
    {
        try { return setTemporaryModSettings.Invoke(collection, directory, false, true, 0, selections, Source) == PenumbraApiEc.Success; }
        catch (Exception ex) { Plugin.Log.Error(ex, "Failed to apply temporary gesture mod settings."); return false; }
    }
    public bool TryRemoveTemporarySettings(Guid collection, string directory)
    {
        try { return removeTemporaryModSettings.Invoke(collection, directory) == PenumbraApiEc.Success; }
        catch { return false; }
    }
    public bool TryRedrawLocalPlayer()
    {
        try { redrawObject.Invoke(0); return true; }
        catch { return false; }
    }
}
