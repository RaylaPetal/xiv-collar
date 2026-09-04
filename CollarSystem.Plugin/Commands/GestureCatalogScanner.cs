using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CollarSystem.Plugin.Config;
using CollarSystem.Plugin.Ipc;
using Lumina.Excel.Sheets;

namespace CollarSystem.Plugin.Commands;

public readonly record struct GestureScanResult(int TotalMods, IReadOnlyList<GestureCatalogEntry> Entries, string? Error = null);

/// PoseKit-equivalent reader for Penumbra's real option manifests. It preserves author-facing option
/// names instead of flattening an entire mod to GetChangedItems labels.
public sealed class GestureCatalogScanner(PenumbraIpc ipc, PluginConfig config)
{
    private sealed record GroupDto(string Type, string Name, List<OptionDto> Options);
    private sealed record OptionDto(string Name, Dictionary<string, string>? Files);
    private sealed record DefaultDto(Dictionary<string, string>? Files);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip };

    public GestureScanResult Scan()
    {
        var mods = ipc.TryGetModList();
        var root = ipc.TryGetModDirectory();
        var collection = ipc.TryGetLocalPlayerCollectionId();
        if (mods is null || root is null || collection is null)
            return new GestureScanResult(mods?.Count ?? 0, [], "Penumbra or the local-player collection is not ready.");

        MigrateFolderSelection(mods);
        var entries = new List<GestureCatalogEntry>();
        IEnumerable<string> directories = config.SelectedGestureMods.Count == 0 ? mods.Keys : config.SelectedGestureMods;
        foreach (var directory in directories)
        {
            if (!mods.TryGetValue(directory, out var modName)) continue;
            var (enabled, current) = ipc.TryGetCurrentSettings(collection.Value, directory);
            var modPath = Path.Combine(root, directory);
            if (!Directory.Exists(modPath)) continue;

            var groups = ReadGroups(modPath, modName, current);
            for (var groupOrder = 0; groupOrder < groups.Count; groupOrder++)
            {
                var group = groups[groupOrder];
                for (var optionOrder = 0; optionOrder < group.Options.Count; optionOrder++)
                {
                    var option = group.Options[optionOrder];
                    var selections = groups.Where(g => !g.Implicit).ToDictionary(g => g.Name,
                        g => g == group ? SelectionFor(g, option.Name) : g.Selected.ToList());
                    var triggers = GestureTriggerResolver.Detect(group.Name, option.Name, option.Paths);
                    if (triggers.Count == 0) triggers.Add(null);
                    for (var triggerOrder = 0; triggerOrder < triggers.Count; triggerOrder++)
                    {
                        var entry = new GestureCatalogEntry
                        {
                            ModDirectory = directory, ModName = modName, GroupName = group.Name,
                            AnimationName = option.Name, GroupSelections = selections, Trigger = triggers[triggerOrder], ModEnabled = enabled,
                            GroupOrder = groupOrder, OptionOrder = optionOrder, TriggerOrder = triggerOrder,
                        };
                        entry.Id = StableId(entry);
                        entries.Add(entry);
                    }
                }
            }
        }
        return new GestureScanResult(mods.Count, entries);
    }

    private void MigrateFolderSelection(Dictionary<string, string> mods)
    {
        if (config.SelectedGestureMods.Count != 0 || config.GestureFolderAllowlist.Count == 0) return;
        foreach (var (directory, name) in mods)
        {
            var path = ipc.TryGetModPath(directory, name);
            if (path != null && config.GestureFolderAllowlist.Any(f => IsUnder(path, f))) config.SelectedGestureMods.Add(directory);
        }
        config.Save();
    }

    private static bool IsUnder(string path, string folder) => path.Equals(folder.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) || path.StartsWith(folder.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);
    private static List<string> SelectionFor(Group group, string option) => group.Multi ? group.Selected.Append(option).Distinct().ToList() : [option];

    private static string StableId(GestureCatalogEntry e)
    {
        var raw = $"{e.ModDirectory}\n{e.GroupName}\n{e.AnimationName}\n{e.Trigger?.Kind}\n{e.Trigger?.SlashCommand}\n{e.Trigger?.EmoteModeId}\n{e.Trigger?.CPoseState}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..16].ToLowerInvariant();
    }

    private sealed record Option(string Name, IEnumerable<string> Paths);
    private sealed record Group(string Name, bool Multi, bool Implicit, List<Option> Options, HashSet<string> Selected);

    private static List<Group> ReadGroups(string modPath, string modName, Dictionary<string, List<string>>? current)
    {
        var groups = new List<Group>();
        var defaultPath = Path.Combine(modPath, "default_mod.json");
        if (File.Exists(defaultPath))
        {
            try
            {
                var dto = JsonSerializer.Deserialize<DefaultDto>(File.ReadAllText(defaultPath), JsonOptions);
                var paths = (dto?.Files?.Keys ?? Enumerable.Empty<string>()).Where(p => p.StartsWith("chara/", StringComparison.OrdinalIgnoreCase)).ToList();
                if (paths.Count > 0) groups.Add(new Group("Default", false, true, [new Option(modName, paths)], [modName]));
            }
            catch (Exception ex) { Plugin.Log.Warning(ex, $"Failed to parse {defaultPath}."); }
        }
        // Penumbra persists group order in the numeric group_### filename. Sorting by the displayed
        // group name produced 1, 10, 100... in large packs; PoseKit follows this manifest order.
        foreach (var file in Directory.GetFiles(modPath, "group_*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(GroupFileOrder).ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var dto = JsonSerializer.Deserialize<GroupDto>(File.ReadAllText(file), JsonOptions);
                if (dto?.Options is null) continue;
                groups.Add(new Group(dto.Name, dto.Type.Equals("Multi", StringComparison.OrdinalIgnoreCase), false,
                    dto.Options.Select(o => new Option(o.Name, o.Files?.Keys ?? Enumerable.Empty<string>())).ToList(),
                    current != null && current.TryGetValue(dto.Name, out var selected) ? [.. selected] : []));
            }
            catch (Exception ex) { Plugin.Log.Warning(ex, $"Failed to parse {file}."); }
        }
        return groups;
    }

    private static int GroupFileOrder(string path)
    {
        var match = Regex.Match(Path.GetFileName(path), @"^group_(\d+)", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var order) ? order : int.MaxValue;
    }
}

internal static partial class GestureTriggerResolver
{
    private static readonly Regex CommandHint = new(@"\(/([a-zA-Z]+)\)", RegexOptions.Compiled);
    private static readonly (uint Mode, Regex Pattern)[] PosePatterns = [(1, new Regex(@"j_pose(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)), (2, new Regex(@"s_pose(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)), (3, new Regex(@"l_pose(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled))];
    private static Dictionary<string, List<(string Key, string Command)>>? emotes;

    public static List<GestureTrigger?> Detect(string group, string option, IEnumerable<string> paths)
    {
        var result = new List<GestureTrigger?>();
        var seen = new HashSet<string>();
        void Add(GestureTrigger t) { if (seen.Add($"{t.Kind}:{t.SlashCommand}:{t.EmoteModeId}:{t.CPoseState}")) result.Add(t); }
        var hint = CommandHint.Match(option); if (!hint.Success) hint = CommandHint.Match(group);
        if (hint.Success) Add(new GestureTrigger { Kind = GestureTriggerKind.SlashCommand, SlashCommand = hint.Groups[1].Value });
        foreach (var raw in paths)
        {
            var path = raw.Replace('\\', '/');
            foreach (var (mode, regex) in PosePatterns)
            {
                var match = regex.Match(path);
                if (match.Success && byte.TryParse(match.Groups[1].Value, out var state) && state <= 6)
                    Add(new GestureTrigger { Kind = GestureTriggerKind.Pose, EmoteModeId = mode, CPoseState = state });
            }
            var baseGroundSit = path.EndsWith("/jmn.pap", StringComparison.OrdinalIgnoreCase);
            if (baseGroundSit) Add(new GestureTrigger { Kind = GestureTriggerKind.Pose, EmoteModeId = 1, CPoseState = 0 });
            // collar/gesture: `Length: > 0` (not just `is { }`) - some emotes resolve via Lookup with a
            // valid row reference but blank TextCommand text (see BuildIndex), which would otherwise
            // catalog an unplayable "/ motion"-style trigger that always fails when played.
            else if (path.EndsWith(".pap", StringComparison.OrdinalIgnoreCase) && Lookup(path[..^4]) is { Length: > 0 } cmd)
                Add(new GestureTrigger { Kind = GestureTriggerKind.SlashCommand, SlashCommand = cmd });
        }
        return result;
    }

    private static string? Lookup(string path)
    {
        emotes ??= BuildIndex();
        var basename = path[(path.LastIndexOf('/') + 1)..];
        return emotes.TryGetValue(basename, out var hits) ? hits.FirstOrDefault(x => path.EndsWith(x.Key, StringComparison.OrdinalIgnoreCase)).Command : null;
    }

    private static Dictionary<string, List<(string, string)>> BuildIndex()
    {
        var result = new Dictionary<string, List<(string, string)>>();
        foreach (var emote in Plugin.DataManager.GetExcelSheet<Emote>())
        {
            if (!emote.TextCommand.IsValid) continue;
            var command = emote.TextCommand.Value.Command.ExtractText().TrimStart('/');
            foreach (var timeline in emote.ActionTimeline)
            {
                if (!timeline.IsValid) continue;
                var key = timeline.Value.Key.ExtractText();
                var basename = key[(key.LastIndexOf('/') + 1)..];
                if (!result.TryGetValue(basename, out var list)) result[basename] = list = [];
                list.Add((key, command));
            }
        }
        return result;
    }
}
