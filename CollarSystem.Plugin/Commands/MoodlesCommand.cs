using System;
using System.Collections.Generic;
using System.Linq;
using CollarSystem.Plugin.Config;
using CollarSystem.Plugin.Ipc;

namespace CollarSystem.Plugin.Commands;

/// collar/moodles: the Owner's direct override for applying/clearing a Moodle. No alias dictionary
/// involved at all (unlike title/outfit/gesture, which have both an alias path and a force-override path)
/// - Moodles only ever exist as this one reserved-keyword command, matched by name against the Sub's own
/// scanned preset catalog. Applies/clears immediately, no confirmation queue - see design.md's "immediate,
/// no confirmation gate" decision.
public sealed class MoodlesCommand
{
    private readonly PluginConfig config;
    private readonly MoodlesIpc moodles;

    /// How many presets the last scan found - so the UI can say "found N" even before anything is picked.
    public int? LastScanTotalPresets { get; private set; }
    public MoodlesScanStatus? LastScanStatus { get; private set; }
    public string? LastScanError { get; private set; }

    public MoodlesCommand(PluginConfig config, MoodlesIpc moodles)
    {
        this.config = config;
        this.moodles = moodles;
    }

    /// Sub-side: rescan the Sub's own saved Moodles presets. No folder allowlist - Moodles presets have no
    /// folder-organization concept, every saved preset is eligible (design.md's "mirrors GestureMapping's
    /// shape" decision, minus the allowlist).
    public void Rescan()
    {
        var result = moodles.GetOwnPresets();
        LastScanStatus = result.Status;
        LastScanError = result.Error;
        if (result.Status != MoodlesScanStatus.Success)
            return;

        LastScanTotalPresets = result.Presets.Count;
        config.MoodlesMapping.LocalCatalog = result.Presets
            .Select(p => new MoodlesPresetEntry { PresetId = p.Id.ToString(), Name = p.Name })
            .ToDictionary(e => e.PresetId);
        config.Save();
    }

    /// The Owner's direct override: matches `presetName` against the Sub's own scanned catalog
    /// (case-insensitive) - the Owner never sees preset GUIDs, only whatever name the Sub told them out of
    /// band, same pattern as OutfitCommand.ForceApply/GestureCommand.ForceApply.
    public bool ForceApply(string presetName)
    {
        var entry = config.MoodlesMapping.LocalCatalog.Values
            .FirstOrDefault(p => string.Equals(p.Name, presetName, StringComparison.OrdinalIgnoreCase));
        if (entry is null || !Guid.TryParse(entry.PresetId, out var presetId))
            return false;

        return moodles.ApplyPreset(presetId);
    }

    public bool ForceClear() => moodles.ClearStatus();

    /// collar/catalog-sync: every scanned preset's display name, deduplicated - the same plain-name shape
    /// Settings' former "Copy names" button produced.
    public IReadOnlyList<string> ExportNames() =>
        config.MoodlesMapping.LocalCatalog.Values.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
}
