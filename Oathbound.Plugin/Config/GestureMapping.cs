using System;
using System.Collections.Generic;

namespace Oathbound.Plugin.Config;

public enum GestureTriggerKind { SlashCommand, Pose }

[Serializable]
public class GestureTrigger
{
    public GestureTriggerKind Kind { get; set; }
    public string SlashCommand { get; set; } = "";
    public uint EmoteModeId { get; set; }
    public byte CPoseState { get; set; }
    public string DisplayName => Kind == GestureTriggerKind.SlashCommand
        ? $"/{SlashCommand.TrimStart('/')}"
        : EmoteModeId switch { 1 => $"Ground Sit Pose {CPoseState + 1}", 2 => $"Sit Pose {CPoseState + 1}", 3 => $"Doze Pose {CPoseState + 1}", _ => $"Pose {CPoseState + 1}" };
}

[Serializable]
public class GestureCatalogEntry
{
    public string Id { get; set; } = "";
    public string ModDirectory { get; set; } = "";
    public string ModName { get; set; } = "";
    public string GroupName { get; set; } = "";
    public string AnimationName { get; set; } = "";
    public int GroupOrder { get; set; }
    public int OptionOrder { get; set; }
    public int TriggerOrder { get; set; }
    public Dictionary<string, List<string>> GroupSelections { get; set; } = new();
    public GestureTrigger? Trigger { get; set; }
    public bool ModEnabled { get; set; }
    public string Label => $"{ModName} — {AnimationName}" + (Trigger is null ? " — no playable trigger" : $" — {Trigger.DisplayName}");
}

[Serializable]
public class GestureMapping
{
    public Dictionary<string, GestureCatalogEntry> LocalCatalog { get; set; } = new();
    public Dictionary<string, GestureExportEntry> ImportedPeerCatalog { get; set; } = new();
}

/// collar/catalog-sync "Exporting every catalog to one file": the slim shape actually serialized into a
/// Gesture export line - only the fields `CatalogSyncService.ImportGestureLines` (the sole reader) ever
/// consumes. Deliberately excludes `GroupSelections`, `ModDirectory`, `TriggerOrder`, and `ModEnabled` -
/// those are meaningful only to the Sub's own local playback (`GestureCommand.Execute`, which reads them
/// straight off `GestureMapping.LocalCatalog` by `Id`, never from anything re-imported) and, in
/// `GroupSelections`' case, redundantly repeat every other option group's selection state on every single
/// entry - the one field that made a large mod collection's export scale combinatorially instead of
/// linearly with catalog size.
[Serializable]
public class GestureExportEntry
{
    public string Id { get; set; } = "";
    public string ModName { get; set; } = "";
    public string GroupName { get; set; } = "";
    public string AnimationName { get; set; } = "";
    public int GroupOrder { get; set; }
    public int OptionOrder { get; set; }
    public GestureTrigger? Trigger { get; set; }
    public string Label => $"{ModName} — {AnimationName}" + (Trigger is null ? " — no playable trigger" : $" — {Trigger.DisplayName}");

    public static GestureExportEntry From(GestureCatalogEntry entry) => new()
    {
        Id = entry.Id,
        ModName = entry.ModName,
        GroupName = entry.GroupName,
        AnimationName = entry.AnimationName,
        GroupOrder = entry.GroupOrder,
        OptionOrder = entry.OptionOrder,
        Trigger = entry.Trigger,
    };
}
