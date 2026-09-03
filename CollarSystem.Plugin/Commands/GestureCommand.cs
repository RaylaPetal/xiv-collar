using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CollarSystem.Plugin.Config;
using CollarSystem.Plugin.Ipc;
using CollarSystem.Plugin.Relay;
using ECommons.Automation;
using Lumina.Excel.Sheets;

namespace CollarSystem.Plugin.Commands;

public enum GestureMessageKind
{
    /// Sub -> Owner: the Sub's current auto-resolved gesture catalog.
    CatalogPush,

    /// Owner -> Sub: a request to play one cataloged gesture. Only ever queued on receipt - see Handle.
    Prompt,
}

public sealed class GesturePayload
{
    public GestureMessageKind Kind { get; set; }
    public List<GestureCatalogEntry>? Catalog { get; set; }
    public string? ModDirectory { get; set; }
    public string? ModName { get; set; }
    public string? EmoteName { get; set; }
}

public sealed record QueuedGesture(string CommandId, string ModDirectory, string ModName, string EmoteName);

/// collar/gesture: Penumbra-backed gesture cataloging, relay, and sub-confirmed triggering. A Prompt is
/// never auto-fired - Handle() only ever enqueues it; ConfirmAndTrigger() is the sole path that plays an
/// emote, and it must be wired to a direct Sub UI action (design.md's queue-and-confirm decision).
public sealed class GestureCommand
{
    private readonly PluginConfig config;
    private readonly RelayClient relay;
    private readonly PenumbraIpc penumbra;

    public List<QueuedGesture> PendingPrompts { get; } = [];

    public event System.Action? CatalogUpdated;
    public event System.Action<QueuedGesture>? PromptQueued;

    public GestureCommand(PluginConfig config, RelayClient relay, PenumbraIpc penumbra)
    {
        this.config = config;
        this.relay = relay;
        this.penumbra = penumbra;
    }

    /// Sub-side: rescan installed mods (scoped to the configured folder allowlist) and, if paired with
    /// "gesture" permission enabled, push the refreshed catalog to the Owner.
    public Task RescanAndPushAsync()
    {
        var entries = penumbra.ScanGestureMods(config.GestureFolderAllowlist);
        config.GestureMapping.LocalCatalog = entries.ToDictionary(e => e.ModDirectory);
        config.Save();

        if (!config.Pairing.IsPaired || !config.Permissions.Gesture)
            return Task.CompletedTask;

        return SendAsync(new GesturePayload { Kind = GestureMessageKind.CatalogPush, Catalog = [.. entries] });
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

    public Task SendPromptAsync(string modDirectory, string modName, string emoteName) => SendAsync(new GesturePayload
    {
        Kind = GestureMessageKind.Prompt,
        ModDirectory = modDirectory,
        ModName = modName,
        EmoteName = emoteName,
    });

    public AckStatus Handle(CommandEnvelope envelope)
    {
        var payload = JsonSerializer.Deserialize<GesturePayload>(envelope.Payload);
        if (payload is null)
            return AckStatus.Failed;

        switch (payload.Kind)
        {
            case GestureMessageKind.CatalogPush:
                config.GestureMapping.CachedPeerCatalog = payload.Catalog ?? [];
                config.Save();
                CatalogUpdated?.Invoke();
                return AckStatus.Applied;

            case GestureMessageKind.Prompt:
                var queued = new QueuedGesture(envelope.CommandId, payload.ModDirectory ?? "", payload.ModName ?? "", payload.EmoteName ?? "");
                PendingPrompts.Add(queued);
                PromptQueued?.Invoke(queued);
                return AckStatus.Applied;

            default:
                return AckStatus.Rejected;
        }
    }

    /// Sub-side only, and only ever called from a direct confirmation click - never automatically.
    public bool ConfirmAndTrigger(string commandId)
    {
        var queued = PendingPrompts.FirstOrDefault(p => p.CommandId == commandId);
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

    public void DismissPrompt(string commandId) => PendingPrompts.RemoveAll(p => p.CommandId == commandId);

    private Task SendAsync(GesturePayload payload) => relay.SendCommandAsync(new CommandEnvelope
    {
        PairingId = config.Pairing.PairingId ?? "",
        Category = CommandCategory.Gesture,
        Payload = JsonSerializer.Serialize(payload),
    });

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
