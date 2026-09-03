using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Ipc;
using ECommons.GameHelpers;

namespace CollarSystem.Plugin.Ipc;

public readonly record struct MoodlesPreset(Guid Id, string Name);
public enum MoodlesScanStatus { Success, Unavailable, Failed }
public readonly record struct MoodlesScanResult(MoodlesScanStatus Status, IReadOnlyList<MoodlesPreset> Presets, string? Error = null);

/// Exact consumer-side mirror of kawaii/Moodles' current IPCProcessor surface. Preset enumeration is
/// local-library-wide and takes no character argument. Player operations take GUID first, then the
/// actual IPlayerCharacter (not a character-name string).
public sealed class MoodlesIpc
{
    private readonly ICallGateSubscriber<List<(Guid GUID, List<Guid> Statuses, int ApplicationType, string Title)>> getPresetsInfoList;
    private readonly ICallGateSubscriber<Guid, IPlayerCharacter, object> applyPresetByPlayer;
    private readonly ICallGateSubscriber<IPlayerCharacter, object> clearStatusManagerByPlayer;

    public MoodlesIpc()
    {
        getPresetsInfoList = Plugin.PluginInterface.GetIpcSubscriber<List<(Guid, List<Guid>, int, string)>>("Moodles.GetPresetsInfoListV2");
        applyPresetByPlayer = Plugin.PluginInterface.GetIpcSubscriber<Guid, IPlayerCharacter, object>("Moodles.ApplyPresetByPlayerV2");
        clearStatusManagerByPlayer = Plugin.PluginInterface.GetIpcSubscriber<IPlayerCharacter, object>("Moodles.ClearStatusManagerByPlayerV2");
    }

    public MoodlesScanResult GetOwnPresets()
    {
        try
        {
            var presets = getPresetsInfoList.InvokeFunc().Select(p => new MoodlesPreset(p.GUID, p.Title)).ToList();
            return new MoodlesScanResult(MoodlesScanStatus.Success, presets);
        }
        catch (Dalamud.Plugin.Ipc.Exceptions.IpcNotReadyError ex)
        {
            Plugin.Log.Warning(ex, "Moodles is not available while reading presets.");
            return new MoodlesScanResult(MoodlesScanStatus.Unavailable, [], "Moodles is not installed or ready.");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to read the local Moodles preset library.");
            return new MoodlesScanResult(MoodlesScanStatus.Failed, [], ex.Message);
        }
    }

    public bool ApplyPreset(Guid presetId)
    {
        var player = Player.Object;
        if (player is null) return false;
        try { applyPresetByPlayer.InvokeAction(presetId, player); return true; }
        catch (Exception ex) { Plugin.Log.Error(ex, "Failed to apply a Moodles preset."); return false; }
    }

    public bool ClearStatus()
    {
        var player = Player.Object;
        if (player is null) return false;
        try { clearStatusManagerByPlayer.InvokeAction(player); return true; }
        catch (Exception ex) { Plugin.Log.Error(ex, "Failed to clear the Sub's Moodles status."); return false; }
    }
}
