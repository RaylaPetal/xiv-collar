using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Ipc;

namespace CollarSystem.Plugin.Ipc;

public readonly record struct MoodlesPreset(Guid Id, string Name);

/// Thin wrapper around Moodles' own IPC surface - always targeting the local player, same "own client
/// only" constraint as GlamourerIpc/HonorificIpc/PenumbraIpc. Moodles ships no `.Api` NuGet package (unlike
/// Glamourer/Penumbra/Honorific), so these are hand-rolled `GetIpcSubscriber` calls against label strings
/// confirmed to exist in the installed Moodles plugin's own assembly - the labels are real, but the exact
/// parameter/return shapes below are this plugin's best-effort reading of Moodles' public IPC convention
/// (its own name-suffixed "ByPlayerV2" overloads, targeting the local player by character name, mirroring
/// how GlamourerIpc/HonorificIpc already avoid needing a raw pointer/object index where a name-based
/// overload exists) and have NOT been confirmed against a running Moodles instance - see design.md's Open
/// Question. If a call throws or a shape mismatches at runtime, that is expected until verified live, not
/// a sign the label itself is wrong.
public sealed class MoodlesIpc
{
    private readonly ICallGateSubscriber<string, List<MoodlesPresetInfo>> getPresetsInfoList;
    private readonly ICallGateSubscriber<string, Guid, object> applyPresetByPlayer;
    private readonly ICallGateSubscriber<string, object> clearStatusManagerByPlayer;

    public MoodlesIpc()
    {
        getPresetsInfoList = Plugin.PluginInterface.GetIpcSubscriber<string, List<MoodlesPresetInfo>>("Moodles.GetPresetsInfoListV2");
        applyPresetByPlayer = Plugin.PluginInterface.GetIpcSubscriber<string, Guid, object>("Moodles.ApplyPresetByPlayerV2");
        clearStatusManagerByPlayer = Plugin.PluginInterface.GetIpcSubscriber<string, object>("Moodles.ClearStatusManagerByPlayerV2");
    }

    /// The Sub's own saved presets (collar/moodles' local scan). Returns empty if Moodles isn't installed/
    /// running or the local player isn't available, rather than throwing - scanning is a routine Settings
    /// action, not something that should crash the plugin if Moodles is momentarily unavailable.
    public IReadOnlyList<MoodlesPreset> GetOwnPresets()
    {
        var localName = Plugin.PlayerState.IsLoaded ? Plugin.PlayerState.CharacterName : null;
        if (localName is null)
            return [];

        try
        {
            return getPresetsInfoList.InvokeFunc(localName)
                .Select(p => new MoodlesPreset(p.GUID, p.Title))
                .ToList();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to read Moodles presets - is Moodles installed and running?");
            return [];
        }
    }

    public bool ApplyPreset(Guid presetId)
    {
        var localName = Plugin.PlayerState.IsLoaded ? Plugin.PlayerState.CharacterName : null;
        if (localName is null)
            return false;

        try
        {
            applyPresetByPlayer.InvokeAction(localName, presetId);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to apply a Moodles preset.");
            return false;
        }
    }

    public bool ClearStatus()
    {
        var localName = Plugin.PlayerState.IsLoaded ? Plugin.PlayerState.CharacterName : null;
        if (localName is null)
            return false;

        try
        {
            clearStatusManagerByPlayer.InvokeAction(localName);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to clear the Sub's Moodles status.");
            return false;
        }
    }
}

/// Best-effort mirror of Moodles' own preset-info record - `GUID`/`Title` field names are confirmed present
/// in the installed Moodles assembly (via its shipped strings), the rest of Moodles' actual record shape is
/// not - Newtonsoft.Json (used to deserialize IPC payloads elsewhere in this plugin, e.g. HonorificIpc)
/// ignores fields it doesn't recognize, so a wider or differently-ordered real payload should still
/// populate at least these two.
public sealed class MoodlesPresetInfo
{
    public Guid GUID { get; set; }
    public string Title { get; set; } = "";
}
