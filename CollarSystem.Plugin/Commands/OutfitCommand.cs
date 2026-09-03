using System.Text.Json;
using System.Threading.Tasks;
using CollarSystem.Plugin.Config;
using CollarSystem.Plugin.Ipc;
using CollarSystem.Plugin.Relay;
using CollarSystem.Plugin.Safety;
using Glamourer.Api.Enums;

namespace CollarSystem.Plugin.Commands;

public sealed class OutfitPayload
{
    /// Unlock-only command: releases a previously applied lock using Key, changes nothing else.
    public bool UnlockOnly { get; set; }

    /// True to apply a full saved-state blob (Base64State); false to apply a single slot (Slot/ItemId/Stains).
    public bool FullState { get; set; }

    public ApiEquipSlot Slot { get; set; }
    public ulong ItemId { get; set; }
    public byte[] Stains { get; set; } = [];

    public string? Base64State { get; set; }

    public uint Key { get; set; }
    public bool Locked { get; set; }
}

/// collar/outfit: Owner-issued outfit commands applied via Glamourer, including the lock/key model.
public sealed class OutfitCommand
{
    private readonly PluginConfig config;
    private readonly RelayClient relay;
    private readonly GlamourerIpc glamourer;
    private readonly SubRuntimeState runtimeState;

    public OutfitCommand(PluginConfig config, RelayClient relay, GlamourerIpc glamourer, SubRuntimeState runtimeState)
    {
        this.config = config;
        this.relay = relay;
        this.glamourer = glamourer;
        this.runtimeState = runtimeState;
    }

    public Task SendSetItemAsync(ApiEquipSlot slot, ulong itemId, byte[] stains, uint key, bool locked) => SendAsync(new OutfitPayload
    {
        FullState = false,
        Slot = slot,
        ItemId = itemId,
        Stains = stains,
        Key = key,
        Locked = locked,
    });

    public Task SendApplyStateAsync(string base64State, uint key, bool locked) => SendAsync(new OutfitPayload
    {
        FullState = true,
        Base64State = base64State,
        Key = key,
        Locked = locked,
    });

    public Task SendUnlockAsync(uint key) => SendAsync(new OutfitPayload { UnlockOnly = true, Key = key });

    public AckStatus Handle(CommandEnvelope envelope)
    {
        var payload = JsonSerializer.Deserialize<OutfitPayload>(envelope.Payload);
        if (payload is null)
            return AckStatus.Failed;

        if (payload.UnlockOnly)
        {
            var unlockEc = glamourer.Unlock(payload.Key);
            if (unlockEc != GlamourerApiEc.Success)
                return AckStatus.Rejected;

            if (runtimeState.OutfitLockKey == payload.Key)
                runtimeState.OutfitLockKey = null;
            return AckStatus.Applied;
        }

        var ec = payload.FullState
            ? glamourer.ApplyState(payload.Base64State ?? "", payload.Key, payload.Locked)
            : glamourer.SetItem(payload.Slot, payload.ItemId, payload.Stains, payload.Key, payload.Locked);

        if (ec != GlamourerApiEc.Success)
            return AckStatus.Rejected;

        runtimeState.OutfitLockKey = payload.Locked ? payload.Key : null;
        return AckStatus.Applied;
    }

    private Task SendAsync(OutfitPayload payload) => relay.SendCommandAsync(new CommandEnvelope
    {
        PairingId = config.Pairing.PairingId ?? "",
        Category = CommandCategory.Outfit,
        Payload = JsonSerializer.Serialize(payload),
    });
}
