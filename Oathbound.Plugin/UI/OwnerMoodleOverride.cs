using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Oathbound.Plugin.Commands;
using Oathbound.Plugin.Config;

namespace Oathbound.Plugin.UI;

/// collar/attached-moodles "Owner can override the attached moodle per command": the Owner-side picker for
/// the optional `moodle:"..."` option on outfit lock / restraint lock|catalog|wear / leash. The Owner only
/// knows the Sub's moodles as the `moodle apply "<selector>"` quick commands imported through catalog sync,
/// so choices come from those - the selector is exactly what the Sub's side resolves. "Sub's default" (null)
/// sends the command unchanged, byte-for-byte what older Sub clients already understand.
public static class OwnerMoodleOverride
{
    private const string MoodleApplyPrefix = "moodle apply ";

    /// The pick shared by the two favorites surfaces: set in the persistent FavoritesWindow, and reused
    /// read-only by the QuickAccessMenu popup (which deliberately stays combo-free - see its own doc comment
    /// on keeping popup calls minimal). Session-only, like every other override pick.
    public static string? FavoritesPick;

    public static IReadOnlyList<(string Label, string Selector)> Choices(PluginConfig config) =>
        config.QuickCommands.Moodles
            .Select(c => c.Command.StartsWith(MoodleApplyPrefix, StringComparison.OrdinalIgnoreCase)
                         && CommandSelector.TryRead(c.Command[MoodleApplyPrefix.Length..], out var selector, out _)
                         && !selector.Contains('"')
                ? (Label: MoodlesTextFormat.StripMarkup(c.Label), Selector: selector)
                : (Label: "", Selector: ""))
            .Where(c => c.Selector.Length > 0)
            .OrderBy(c => c.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// Whether `command` is one of the commands that carry the option.
    public static bool Accepts(string command)
    {
        var trimmed = command.Trim();
        return trimmed.Equals(ControlWords.Leash, StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("outfit lock ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("restraint lock ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("restraint catalog ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("restraint wear ", StringComparison.OrdinalIgnoreCase);
    }

    public static string Apply(string command, string? chosen) =>
        chosen is not null && Accepts(command) ? MoodleOption.Append(command, chosen) : command;

    /// Draws the picker; `chosen` stays null for "Sub's default".
    public static void Draw(string id, PluginConfig config, ref string? chosen)
    {
        var choices = Choices(config);
        var current = chosen;
        var preview = current is null
            ? "Sub's default"
            : choices.Where(c => c.Selector == current).Select(c => c.Label).FirstOrDefault() ?? current;
        ImGui.SetNextItemWidth(200);
        if (ImGui.BeginCombo($"Moodle##ownerMoodle_{id}", preview))
        {
            if (ImGui.Selectable($"Sub's default##ownerMoodle_{id}", chosen is null))
                chosen = null;
            if (choices.Count == 0)
                ImGui.TextDisabled("No Moodles imported from your Sub yet (Sync tab).");
            foreach (var (label, selector) in choices)
            {
                if (ImGui.Selectable($"{label}##ownerMoodle_{id}_{selector}", chosen == selector))
                    chosen = selector;
            }
            ImGui.EndCombo();
        }
        IconGlyph.HelpMarker("Which moodle goes on your Sub along with outfit lock / restraints / leash sent from here. \"Sub's default\" uses whatever your Sub attached themselves. A different pick only applies if your Sub has Moodles permission on, and needs your Sub on a plugin version that understands it - older versions ignore the whole command.");
    }
}
