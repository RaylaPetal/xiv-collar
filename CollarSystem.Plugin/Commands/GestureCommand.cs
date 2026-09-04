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

    /// Gap between the Penumbra redraw and playing the tied trigger, so the animation reliably starts
    /// after the redraw visually settles instead of racing a visible flicker.
    private const long PlayDelayMs = 500;

    /// How long an active temporary activation survives with no further gesture play before it's
    /// automatically reverted.
    private const long IdleTimeoutMs = 30_000;

    private readonly PluginConfig config;
    private readonly PenumbraIpc penumbra;
    private readonly GestureCatalogScanner scanner;

    private (GestureTrigger Trigger, long ReadyAtTicks)? pendingPlay;
    private (Guid Collection, string ModDirectory, long IdleUntilTicks)? activeTemporary;

    public int? LastScanTotalMods { get; private set; }
    public string? LastScanError { get; private set; }

    /// Whether there's an active temporary Penumbra activation to revert - lets the Gesture module's
    /// manual Reset control enable/disable itself.
    public bool HasActiveTemporary => activeTemporary is not null;

    public GestureCommand(PluginConfig config, PenumbraIpc penumbra)
    {
        this.config = config;
        this.penumbra = penumbra;
        scanner = new GestureCatalogScanner(penumbra, config);
    }

    /// Advanced from Plugin.OnFrameworkUpdate - the same per-frame hook already driving the panic
    /// hotkey, so the delayed play and idle-timeout revert both stay on the framework thread instead of
    /// racing a background Task.
    public void OnFrameworkUpdate()
    {
        var now = Environment.TickCount64;

        if (pendingPlay is { } pending && now >= pending.ReadyAtTicks)
        {
            pendingPlay = null;
            Play(pending.Trigger);
        }

        if (activeTemporary is { } active && now >= active.IdleUntilTicks)
            ResetActiveTemporary();
    }

    /// Reverts the active temporary gesture activation on demand - used by the manual Reset control and
    /// internally whenever a different mod's temporary activation needs to replace this one.
    public void ResetActiveTemporary()
    {
        if (activeTemporary is not { } active)
            return;

        penumbra.TryRemoveTemporarySettings(active.Collection, active.ModDirectory);
        activeTemporary = null;
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

    /// collar/catalog-sync: serializes only `GestureExportEntry`'s slim shape, not the full
    /// `GestureCatalogEntry` - see that type's own doc comment for why (the exported file's size scales
    /// with entry count, not with how many option groups each entry's source mod happens to have).
    public string ExportCatalog() => string.Join("\n", config.GestureMapping.LocalCatalog.Values
        .Where(e => e.Trigger != null).OrderBy(e => e.Label)
        .Select(e => ExportPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(GestureExportEntry.From(e))))));

    public static bool TryParseExport(string line, out GestureExportEntry? entry)
    {
        entry = null;
        if (!line.StartsWith(ExportPrefix, StringComparison.Ordinal)) return false;
        try { entry = JsonSerializer.Deserialize<GestureExportEntry>(Encoding.UTF8.GetString(Convert.FromBase64String(line[ExportPrefix.Length..]))); return entry?.Trigger != null; }
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

        // Switching to a different mod's temporary activation must revert whatever was previously
        // active first, so its settings never linger once an unrelated gesture takes over.
        if (activeTemporary is { } active && (active.Collection != collection.Value || active.ModDirectory != entry.ModDirectory))
            ResetActiveTemporary();

        var selections = entry.GroupSelections.ToDictionary(x => x.Key, x => (IReadOnlyList<string>)x.Value);
        if (!penumbra.TrySetTemporarySettings(collection.Value, entry.ModDirectory, selections)) return false;
        if (!penumbra.TryRedrawLocalPlayer()) return false;

        var now = Environment.TickCount64;
        activeTemporary = (collection.Value, entry.ModDirectory, now + IdleTimeoutMs);
        pendingPlay = (entry.Trigger, now + PlayDelayMs);
        return true;
    }

    /// Internal rather than private: collar/restraints' Arms Cuffed/Legs Cuffed/Full Body Cuffed rules
    /// reuse this exact one-shot trigger playback for their own chosen animation, distinct from Gesture's
    /// own temporary-activation/idle-timeout bookkeeping which those rules deliberately don't share.
    internal static unsafe void Play(GestureTrigger trigger)
    {
        if (trigger.Kind == GestureTriggerKind.SlashCommand)
        {
            Chat.SendMessage($"/{trigger.SlashCommand.TrimStart('/')} motion");
            return;
        }
        var playerState = PlayerState.Instance();
        if (playerState == null || trigger.EmoteModeId is < 1 or > 3) return;
        var poseType = trigger.EmoteModeId switch
        {
            1 => EmoteController.PoseType.GroundSit,
            2 => EmoteController.PoseType.Sit,
            3 => EmoteController.PoseType.Doze,
            _ => throw new ArgumentOutOfRangeException(),
        };
        playerState->SelectedPoses[(int)poseType] = trigger.CPoseState;
        Chat.SendMessage(trigger.EmoteModeId switch { 1 => "/groundsit", 2 => "/sit", 3 => "/doze", _ => "" });
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
