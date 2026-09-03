using System;
using System.Collections.Generic;

namespace CollarSystem.Plugin.Config;

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
}
