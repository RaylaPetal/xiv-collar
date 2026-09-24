using System.Collections.Generic;
using System.Linq;
using Oathbound.Plugin.Config;

namespace Oathbound.Plugin.Commands;

/// collar/catalog-sync "shared presets are copies": what a Sub's own preset (an alias, a rules-only
/// restraint, a custom trigger) becomes when shared with their Owner - a self-contained Owner command that
/// carries everything needed to run it, so the Owner's copy keeps working after the Sub deletes or changes
/// the original, until the Owner removes it. Sharing again replaces the Owner's imported copies with the
/// Sub's current set (CatalogSyncService's reconcile), never touching the Owner's own commands.
///
/// Copies are Owner commands, with the Owner's force semantics: a title copy locks the title, a restraint
/// copy force-applies, an outfit copy locks unless the Sub's alias was unlocked (`outfit wear`). An
/// attached moodle is not baked into the command - it's exported separately and becomes the Owner's
/// per-command moodle pick (QuickCommand.MoodleOverride), so the Owner can still change it.
public static class SharedPresetCommands
{
    /// A copy must fit in one chat message once the Owner's /tell target and trigger phrase are added in
    /// front of it - this leaves room for those.
    private const int ComposeMargin = 64;

    public static bool FitsInOneMessage(string command) => command.Length + ComposeMargin <= CommandSelector.MaxCommandLength;

    /// The moodle name to carry with a copy (as the Owner's moodle pick), or null when there is none or it
    /// can't travel inside the `moodle:"..."` option (a name with a double quote).
    public static string? MoodleName(AttachedMoodleRef? moodle)
    {
        if (moodle is null)
            return null;
        var name = MoodlesTextFormat.StripMarkup(moodle.StatusName).Trim();
        return name.Length == 0 || name.Contains('"') ? null : name;
    }

    public static string Title(TitleAliasDefinition alias) =>
        TitleCommand.BuildStyleCommand(alias.Text, alias.IsPrefix, alias.Color, alias.Glow);

    /// `outfit lock` for a locking alias, `outfit wear` for an unlocked one - both Owner commands; the
    /// Sub's plugin finds the design by name in its own wardrobe.
    public static string? Outfit(OutfitAliasDefinition alias, PluginConfig config)
    {
        var name = config.WardrobeMapping.LocalDesigns.TryGetValue(alias.DesignId, out var design) ? design.Name : alias.DesignName;
        if (string.IsNullOrWhiteSpace(name) || name.Contains('"'))
            return null;
        return $"outfit {(alias.Locked ? "lock" : "wear")} \"{name}\"";
    }

    public static string? Gesture(GestureAliasDefinition alias, PluginConfig config)
    {
        if (!config.GestureMapping.LocalCatalog.TryGetValue(alias.GestureId, out var entry))
            return null;
        var catalog = config.GestureMapping.LocalCatalog.Values.Select(GestureExportEntry.From).ToList();
        return $"gesture {CommandSelector.Quote(CommandSelector.GestureSelector(GestureExportEntry.From(entry), catalog))}";
    }

    public static string? Moodle(MoodlesAliasDefinition alias, PluginConfig config)
    {
        var name = config.MoodlesMapping.LocalCatalog.TryGetValue(alias.StatusId, out var status) ? status.Name : alias.StatusName;
        if (string.IsNullOrWhiteSpace(name))
            return null;
        var allNames = config.MoodlesMapping.LocalCatalog.Values.Select(s => s.Name);
        return $"moodle apply {CommandSelector.Quote(CommandSelector.MoodleSelector(name, allNames))}";
    }

    public static string RulesOnlyRestraint(RestraintDeviceDefinition device) =>
        RestraintCommand.BuildWearCommand(device.Slot, device.ItemId, device.Name, device.Rules);

    /// A `customtrigger cast` bundle is sent as one message per action when the whole thing doesn't fit
    /// (ChatComposer.ComposeAll), so it only needs each action's own message to fit.
    private static bool BundleFits(string command) =>
        FitsInOneMessage(command) || CustomTriggerCommand.SplitCastCommand(command) is { Count: > 1 } parts && parts.All(FitsInOneMessage);

    /// The whole trigger as one `customtrigger cast` bundle, each restraint carrying its own rules. Null when
    /// it can't be made self-contained (a restraint action whose restraint no longer exists) or one of its
    /// actions alone won't fit in a chat message - such triggers aren't shared (see TooLongToShare).
    public static string? CustomTrigger(CustomTriggerDefinition trigger, PluginConfig config)
    {
        var actions = new List<CustomTriggerAction>();
        foreach (var action in trigger.Actions)
        {
            if (SelfContained(action, config) is not { } copy)
                return null;
            actions.Add(copy);
        }
        if (actions.Count == 0 || trigger.Alias.Contains('"'))
            return null;
        var command = CustomTriggerCommand.BuildCastCommand(trigger.Alias, actions);
        return BundleFits(command) ? command : null;
    }

    /// True when a trigger can't be shared because one of its actions is too long for a chat message.
    public static bool IsTooLongToShare(CustomTriggerDefinition trigger, PluginConfig config)
    {
        var actions = trigger.Actions.Select(a => SelfContained(a, config)).ToList();
        if (actions.Count == 0 || actions.Any(a => a is null))
            return false;
        return !BundleFits(CustomTriggerCommand.BuildCastCommand(trigger.Alias, actions!));
    }

    private static CustomTriggerAction? SelfContained(CustomTriggerAction action, PluginConfig config)
    {
        if (action.Kind != CustomTriggerActionKind.Restraint)
            return action;

        if (action.RestraintCatalogId.Length > 0)
        {
            var configured = config.RestraintMapping.ConfiguredMods.FirstOrDefault(m => m.CatalogId == action.RestraintCatalogId && m.ItemId == action.RestraintItemId)
                ?? config.RestraintMapping.ConfiguredMods.FirstOrDefault(m => m.CatalogId == action.RestraintCatalogId);
            if (configured is null || configured.Rules.Count == 0)
                return null;
            return new CustomTriggerAction
            {
                Kind = CustomTriggerActionKind.Restraint,
                RestraintCatalogId = action.RestraintCatalogId,
                RestraintItemId = action.RestraintItemId,
                RestraintDeviceName = action.RestraintDeviceName,
                RestraintRules = configured.Rules,
            };
        }

        if (!config.RestraintMapping.Devices.TryGetValue(action.RestraintDeviceId, out var device) || device.Rules.Count == 0)
            return null;
        return new CustomTriggerAction
        {
            Kind = CustomTriggerActionKind.Restraint,
            RestraintDeviceName = device.Name,
            RestraintRules = device.Rules,
            RestraintRulesOnly = true,
            RestraintSlot = device.Slot,
            RestraintItemId = device.ItemId ?? 0,
        };
    }
}
