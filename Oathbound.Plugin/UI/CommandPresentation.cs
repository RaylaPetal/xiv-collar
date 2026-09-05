using System;
using Oathbound.Plugin.Commands;
using Oathbound.Plugin.Config;

namespace Oathbound.Plugin.UI;

/// User-facing vocabulary only. Wire serializers and stable identities never consume these strings.
public static class CommandPresentation
{
    public static string Action(CustomTriggerAction action) => action.Kind switch
    {
        CustomTriggerActionKind.Title => $"Title · \"{action.TitleText}\"",
        CustomTriggerActionKind.Outfit => $"Outfit · {action.OutfitDesignName}",
        CustomTriggerActionKind.Gesture => $"Gesture · {action.GestureAnimationName}",
        CustomTriggerActionKind.Moodle => $"Moodle · {MoodlesTextFormat.StripMarkup(action.MoodleStatusName)}",
        CustomTriggerActionKind.Restraint => $"Restraint · {action.RestraintDeviceName}",
        CustomTriggerActionKind.Chat => $"Chat · \"{action.ChatText}\"",
        _ => "Unknown action",
    };

    public static string Rule(RestraintRuleAssignment rule) => rule.Kind switch
    {
        RestraintRuleKind.ForcedPose => $"Forced Pose · {Pose(rule.PoseModeId)}",
        RestraintRuleKind.WalkOnly => "Walking Only",
        RestraintRuleKind.ActionBlock => "Actions Blocked",
        RestraintRuleKind.GagChat => "Gagged",
        RestraintRuleKind.ArmsCuffed => "Arms Cuffed",
        RestraintRuleKind.LegsCuffed => "Legs Cuffed",
        RestraintRuleKind.FullBodyCuffed => "Full Body Cuffed",
        _ => "Unknown rule",
    };

    public static string Pose(int id) => id switch { 1 => "Ground Sit", 2 => "Sit", 3 => "Doze", _ => "Unknown Pose" };

    public static string CompactAnimation(string name)
    {
        if (name.StartsWith('(')) return name;
        var triggerHint = name.IndexOf(" (", StringComparison.Ordinal);
        var compact = triggerHint > 0 ? name[..triggerHint] : name;
        while (true)
        {
            var open = compact.IndexOf('[');
            var close = open >= 0 ? compact.IndexOf(']', open + 1) : -1;
            if (open < 0 || close < 0) break;
            compact = $"{compact[..open]}{compact[(close + 1)..]}";
        }
        compact = string.Join(' ', compact.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= 28 ? compact : $"{compact[..25]}...";
    }
}
