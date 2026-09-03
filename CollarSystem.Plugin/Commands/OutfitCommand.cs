using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CollarSystem.Plugin.Config;
using CollarSystem.Plugin.Ipc;
using CollarSystem.Plugin.Relay;
using CollarSystem.Plugin.Safety;
using Glamourer.Api.Enums;

namespace CollarSystem.Plugin.Commands;

public enum OutfitMessageKind
{
    SetItem,
    ApplyState,
    ApplyDesign,
    Unlock,

    /// Sub -> Owner: the Sub's current wardrobe (Glamourer design) catalog, mirroring collar/gesture's
    /// CatalogPush.
    CatalogPush,
}

public sealed class OutfitPayload
{
    public OutfitMessageKind Kind { get; set; }

    public ApiEquipSlot Slot { get; set; }
    public ulong ItemId { get; set; }
    public byte[] Stains { get; set; } = [];

    public string? Base64State { get; set; }
    public Guid DesignId { get; set; }

    public uint Key { get; set; }
    public bool Locked { get; set; }

    public List<WardrobeDesignEntry>? Catalog { get; set; }
}

/// collar/outfit: Owner-issued outfit commands applied via Glamourer, including the lock/key model and
/// the Wardrobe design-selection flow (Sub shares their saved designs, Owner applies one by id).
public sealed class OutfitCommand
{
    private readonly PluginConfig config;
    private readonly RelayClient relay;
    private readonly GlamourerIpc glamourer;
    private readonly SubRuntimeState runtimeState;

    /// Sub-side: how many designs the last wardrobe scan found in total, before the allowlist filter.
    public int? LastScanTotalDesigns { get; private set; }

    public event Action? CatalogUpdated;

    public OutfitCommand(PluginConfig config, RelayClient relay, GlamourerIpc glamourer, SubRuntimeState runtimeState)
    {
        this.config = config;
        this.relay = relay;
        this.glamourer = glamourer;
        this.runtimeState = runtimeState;
    }

    public Task SendSetItemAsync(ApiEquipSlot slot, ulong itemId, byte[] stains, uint key, bool locked) => SendAsync(new OutfitPayload
    {
        Kind = OutfitMessageKind.SetItem,
        Slot = slot,
        ItemId = itemId,
        Stains = stains,
        Key = key,
        Locked = locked,
    });

    public Task SendApplyStateAsync(string base64State, uint key, bool locked) => SendAsync(new OutfitPayload
    {
        Kind = OutfitMessageKind.ApplyState,
        Base64State = base64State,
        Key = key,
        Locked = locked,
    });

    /// Owner-side: apply one of the Sub's shared wardrobe designs by id - the primary Wardrobe flow.
    public Task SendApplyDesignAsync(Guid designId, uint key, bool locked) => SendAsync(new OutfitPayload
    {
        Kind = OutfitMessageKind.ApplyDesign,
        DesignId = designId,
        Key = key,
        Locked = locked,
    });

    public Task SendUnlockAsync(uint key) => SendAsync(new OutfitPayload { Kind = OutfitMessageKind.Unlock, Key = key });

    /// Sub-side: rescan the Sub's own Glamourer designs (scoped to the wardrobe folder allowlist) and,
    /// if paired with "outfit" permission enabled, push the refreshed catalog to the Owner. Mirrors
    /// GestureCommand.RescanAndPushAsync exactly.
    public Task RescanAndPushDesignsAsync()
    {
        var allDesigns = glamourer.GetDesigns();
        LastScanTotalDesigns = allDesigns.Count;

        var allowlist = config.WardrobeFolderAllowlist;
        var matched = allowlist.Count == 0
            ? []
            : allDesigns.Where(d => allowlist.Any(folder => IsUnderFolder(d.FullPath, folder))).ToList();

        var entries = matched.Select(d => new WardrobeDesignEntry { DesignId = d.Id, Name = d.DisplayName }).ToList();
        config.WardrobeMapping.LocalDesigns = entries.ToDictionary(e => e.DesignId);
        config.Save();

        if (!config.Pairing.IsPaired || !config.Permissions.Outfit)
            return Task.CompletedTask;

        return SendAsync(new OutfitPayload { Kind = OutfitMessageKind.CatalogPush, Catalog = entries });
    }

    private static bool IsUnderFolder(string fullPath, string folder) =>
        fullPath.StartsWith(folder.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);

    public AckStatus Handle(CommandEnvelope envelope)
    {
        var payload = JsonSerializer.Deserialize<OutfitPayload>(envelope.Payload);
        if (payload is null)
            return AckStatus.Failed;

        switch (payload.Kind)
        {
            case OutfitMessageKind.Unlock:
                var unlockEc = glamourer.Unlock(payload.Key);
                if (unlockEc != GlamourerApiEc.Success)
                    return AckStatus.Rejected;

                if (runtimeState.OutfitLockKey == payload.Key)
                    runtimeState.OutfitLockKey = null;
                return AckStatus.Applied;

            case OutfitMessageKind.CatalogPush:
                config.WardrobeMapping.CachedPeerDesigns = payload.Catalog ?? [];
                config.Save();
                CatalogUpdated?.Invoke();
                return AckStatus.Applied;

            default:
                var ec = payload.Kind switch
                {
                    OutfitMessageKind.ApplyState => glamourer.ApplyState(payload.Base64State ?? "", payload.Key, payload.Locked),
                    OutfitMessageKind.ApplyDesign => glamourer.ApplyDesign(payload.DesignId, payload.Key, payload.Locked),
                    _ => glamourer.SetItem(payload.Slot, payload.ItemId, payload.Stains, payload.Key, payload.Locked),
                };

                if (ec != GlamourerApiEc.Success)
                    return AckStatus.Rejected;

                runtimeState.OutfitLockKey = payload.Locked ? payload.Key : null;
                return AckStatus.Applied;
        }
    }

    private Task SendAsync(OutfitPayload payload) => relay.SendCommandAsync(new CommandEnvelope
    {
        PairingId = config.Pairing.PairingId ?? "",
        Category = CommandCategory.Outfit,
        Payload = JsonSerializer.Serialize(payload),
    });
}
