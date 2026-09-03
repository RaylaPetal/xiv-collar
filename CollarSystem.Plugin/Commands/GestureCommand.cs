using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using CollarSystem.Plugin.Config;
using CollarSystem.Plugin.Ipc;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace CollarSystem.Plugin.Commands;

public sealed class GestureCommand
{
    private const string ExportPrefix = "COLLAR-GESTURE-V1|";
    private readonly PluginConfig config;
    private readonly PenumbraIpc penumbra;
    private readonly GestureCatalogScanner scanner;

    public int? LastScanTotalMods { get; private set; }
    public string? LastScanError { get; private set; }

    public GestureCommand(PluginConfig config, PenumbraIpc penumbra)
    {
        this.config = config;
        this.penumbra = penumbra;
        scanner = new GestureCatalogScanner(penumbra, config);
    }

    public void Rescan()
    {
        var result = scanner.Scan();
        LastScanTotalMods = result.TotalMods;
        LastScanError = result.Error;
        if (result.Error != null) return;
        config.GestureMapping.LocalCatalog = result.Entries.ToDictionary(e => e.Id);
        MigrateAliases();
        config.Save();
    }

    public IReadOnlyList<(string Directory, string Name, string? SortPath)> GetInstalledMods()
    {
        var mods = penumbra.TryGetModList();
        return mods is null ? [] : mods.Select(x => (x.Key, x.Value, penumbra.TryGetModPath(x.Key, x.Value))).OrderBy(x => x.Value).ToList();
    }

    public string ExportCatalog() => string.Join("\n", config.GestureMapping.LocalCatalog.Values
        .Where(e => e.Trigger != null).OrderBy(e => e.Label)
        .Select(e => ExportPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(e)))));

    public static bool TryParseExport(string line, out GestureCatalogEntry? entry)
    {
        entry = null;
        if (!line.StartsWith(ExportPrefix, StringComparison.Ordinal)) return false;
        try { entry = JsonSerializer.Deserialize<GestureCatalogEntry>(Encoding.UTF8.GetString(Convert.FromBase64String(line[ExportPrefix.Length..]))); return entry?.Trigger != null; }
        catch { return false; }
    }

    public bool Apply(GestureAliasDefinition alias)
    {
        if (!string.IsNullOrEmpty(alias.GestureId) && config.GestureMapping.LocalCatalog.TryGetValue(alias.GestureId, out var exact)) return Execute(exact);
        var matches = config.GestureMapping.LocalCatalog.Values.Where(e => e.Trigger != null && e.ModDirectory == alias.ModDirectory &&
            string.Equals(e.Trigger.DisplayName.TrimStart('/'), alias.EmoteName.TrimStart('/'), StringComparison.OrdinalIgnoreCase)).ToList();
        return matches.Count == 1 && Execute(matches[0]);
    }

    public bool ForceApply(string idOrName)
    {
        if (config.GestureMapping.LocalCatalog.TryGetValue(idOrName, out var byId)) return Execute(byId);
        var matches = config.GestureMapping.LocalCatalog.Values.Where(e => e.Trigger != null &&
            (e.AnimationName.Equals(idOrName, StringComparison.OrdinalIgnoreCase) || e.Label.Equals(idOrName, StringComparison.OrdinalIgnoreCase))).ToList();
        return matches.Count == 1 && Execute(matches[0]);
    }

    private bool Execute(GestureCatalogEntry entry)
    {
        if (entry.Trigger is null) return false;
        var collection = penumbra.TryGetLocalPlayerCollectionId();
        if (collection is null) return false;
        var selections = entry.GroupSelections.ToDictionary(x => x.Key, x => (IReadOnlyList<string>)x.Value);
        if (!penumbra.TrySetTemporarySettings(collection.Value, entry.ModDirectory, selections)) return false;
        if (!penumbra.TryRedrawLocalPlayer()) return false;
        return Play(entry.Trigger);
    }

    private static unsafe bool Play(GestureTrigger trigger)
    {
        if (trigger.Kind == GestureTriggerKind.SlashCommand)
        {
            Chat.SendMessage($"/{trigger.SlashCommand.TrimStart('/')} motion");
            return true;
        }
        var playerState = PlayerState.Instance();
        if (playerState == null || trigger.EmoteModeId is < 1 or > 3) return false;
        var poseType = trigger.EmoteModeId switch
        {
            1 => EmoteController.PoseType.GroundSit,
            2 => EmoteController.PoseType.Sit,
            3 => EmoteController.PoseType.Doze,
            _ => throw new ArgumentOutOfRangeException(),
        };
        playerState->SelectedPoses[(int)poseType] = trigger.CPoseState;
        Chat.SendMessage(trigger.EmoteModeId switch { 1 => "/groundsit", 2 => "/sit", 3 => "/doze", _ => "" });
        return true;
    }

    private void MigrateAliases()
    {
        foreach (var alias in config.Aliases.Gestures.Where(a => string.IsNullOrEmpty(a.GestureId)))
        {
            var hits = config.GestureMapping.LocalCatalog.Values.Where(e => e.Trigger != null && e.ModDirectory == alias.ModDirectory &&
                e.Trigger.DisplayName.TrimStart('/').Equals(alias.EmoteName.TrimStart('/'), StringComparison.OrdinalIgnoreCase)).ToList();
            if (hits.Count != 1) continue;
            alias.GestureId = hits[0].Id;
            alias.AnimationName = hits[0].AnimationName;
        }
    }
}
