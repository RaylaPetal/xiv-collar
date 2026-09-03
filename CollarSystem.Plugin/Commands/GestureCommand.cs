using System;
using System.Collections.Generic;
using System.Linq;
using CollarSystem.Plugin.Config;
using CollarSystem.Plugin.Ipc;
using ECommons.Automation;
using Lumina.Excel.Sheets;

namespace CollarSystem.Plugin.Commands;

public sealed record QueuedGesture(string Id, string ModDirectory, string ModName, string EmoteName);

/// collar/gesture: Penumbra-backed gesture cataloging and sub-confirmed triggering. A resolved alias is
/// never auto-fired - Queue() only ever enqueues it; ConfirmAndTrigger() is the sole path that plays an
/// emote, and it must be wired to a direct Sub UI action (design.md's queue-and-confirm decision,
/// unchanged by the chat-transport switch - only how a trigger arrives changed, not this safety property).
public sealed class GestureCommand
{
    private readonly PluginConfig config;
    private readonly PenumbraIpc penumbra;

    public List<QueuedGesture> PendingPrompts { get; } = [];

    /// Sub-side: how many mods the last scan found in total, before the allowlist filter - so the UI can
    /// say "found N, M matched" instead of an unexplained empty list. Null until the first scan runs.
    public int? LastScanTotalMods { get; private set; }

    public event Action<QueuedGesture>? PromptQueued;

    public GestureCommand(PluginConfig config, PenumbraIpc penumbra)
    {
        this.config = config;
        this.penumbra = penumbra;
    }

    /// Sub-side: rescan installed mods, scoped to the configured folder allowlist. Purely local - the Sub
    /// picks a resolved mod/emote here to name a gesture alias after in Settings.
    public void Rescan()
    {
        var scan = penumbra.ScanGestureMods(config.GestureFolderAllowlist);
        LastScanTotalMods = scan.TotalModsScanned;
        config.GestureMapping.LocalCatalog = scan.Entries.ToDictionary(e => e.ModDirectory);
        config.Save();
    }

    /// Owner-side: manually assign an emote to a mod GetChangedItems could not resolve, before prompting it.
    public void SetManualAssignment(string modDirectory, string emoteName)
    {
        if (config.GestureMapping.LocalCatalog.TryGetValue(modDirectory, out var entry))
        {
            entry.EmoteNames = [emoteName];
            entry.IsManualAssignment = true;
            config.Save();
        }
    }

    public void Queue(GestureAliasDefinition alias) => QueueInternal(alias.ModDirectory, alias.ModName, alias.EmoteName);

    /// The Owner's direct override: matches `name` against the Sub's own scanned+allowlisted catalog
    /// (mod name or resolved emote name, case-insensitive) instead of requiring a pre-defined alias. Still
    /// only ever queues - the confirm-required safety property is identical to the alias path, since a
    /// gesture is a one-shot action with nothing to "lock" the way title/outfit have.
    public bool ForceQueue(string name)
    {
        var match = config.GestureMapping.LocalCatalog.Values
            .SelectMany(e => e.EmoteNames.Select(emote => (Entry: e, Emote: emote)))
            .FirstOrDefault(x => string.Equals(x.Emote, name, StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(x.Entry.ModName, name, StringComparison.OrdinalIgnoreCase));
        if (match.Entry is null)
            return false;

        QueueInternal(match.Entry.ModDirectory, match.Entry.ModName, match.Emote);
        return true;
    }

    private void QueueInternal(string modDirectory, string modName, string emoteName)
    {
        var queued = new QueuedGesture(Guid.NewGuid().ToString("N"), modDirectory, modName, emoteName);
        PendingPrompts.Add(queued);
        PromptQueued?.Invoke(queued);
    }

    /// Sub-side only, and only ever called from a direct confirmation click - never automatically.
    public bool ConfirmAndTrigger(string id)
    {
        var queued = PendingPrompts.FirstOrDefault(p => p.Id == id);
        if (queued is null)
            return false;

        PendingPrompts.Remove(queued);

        if (!penumbra.ActivateModForLocalPlayer(queued.ModDirectory, queued.ModName))
            return false;

        var command = ResolveEmoteTextCommand(queued.EmoteName);
        if (command is null)
            return false;

        Chat.SendMessage(command);
        return true;
    }

    public void DismissPrompt(string id) => PendingPrompts.RemoveAll(p => p.Id == id);

    private static string? ResolveEmoteTextCommand(string emoteName)
    {
        var sheet = Plugin.DataManager.GetExcelSheet<Emote>();
        foreach (var row in sheet)
        {
            if (!string.Equals(row.Name.ExtractText(), emoteName, StringComparison.OrdinalIgnoreCase))
                continue;

            var command = row.TextCommand.ValueNullable?.Command.ExtractText();
            if (!string.IsNullOrEmpty(command))
                return command;
        }

        Plugin.Log.Warning($"Could not resolve a text command for emote \"{emoteName}\".");
        return null;
    }
}
