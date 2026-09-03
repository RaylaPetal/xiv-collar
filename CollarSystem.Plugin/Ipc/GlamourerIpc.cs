using System.Collections.Generic;
using System.Linq;
using Glamourer.Api.Enums;
using Glamourer.Api.IpcSubscribers;

namespace CollarSystem.Plugin.Ipc;

public readonly record struct GlamourerDesign(System.Guid Id, string DisplayName, string FullPath);

/// Thin wrapper around the Glamourer.Api calls collar/outfit needs, always targeting the local player
/// (objectIndex 0) - see design.md's Context: only the local client's own state change reaches anyone else.
public sealed class GlamourerIpc
{
    private const int LocalPlayerObjectIndex = 0;

    private readonly SetItem setItem;
    private readonly ApplyState applyState;
    private readonly RevertState revertState;
    private readonly UnlockState unlockState;
    private readonly GetDesignListExtended getDesignListExtended;
    private readonly ApplyDesign applyDesign;

    public GlamourerIpc()
    {
        setItem = new SetItem(Plugin.PluginInterface);
        applyState = new ApplyState(Plugin.PluginInterface);
        revertState = new RevertState(Plugin.PluginInterface);
        unlockState = new UnlockState(Plugin.PluginInterface);
        getDesignListExtended = new GetDesignListExtended(Plugin.PluginInterface);
        applyDesign = new ApplyDesign(Plugin.PluginInterface);
    }

    /// The Sub's own saved Glamourer designs, with the folder path shown in Glamourer's design browser -
    /// mirrors PenumbraIpc's sort-path use for the gesture folder allowlist.
    public IReadOnlyList<GlamourerDesign> GetDesigns() =>
        getDesignListExtended.Invoke()
            .Select(kv => new GlamourerDesign(kv.Key, kv.Value.DisplayName, kv.Value.FullPath))
            .ToList();

    /// Applies one of the Sub's own saved designs by id, optionally locked - the primary Wardrobe flow
    /// (collar/outfit), simpler than hand-picking a slot/item for the common "put them in outfit X" case.
    public GlamourerApiEc ApplyDesign(System.Guid designId, uint key, bool locked)
    {
        var flags = ApplyFlagEx.DesignDefault;
        if (locked)
            flags |= ApplyFlag.Lock;
        return applyDesign.Invoke(designId, LocalPlayerObjectIndex, key, flags);
    }

    public GlamourerApiEc SetItem(ApiEquipSlot slot, ulong itemId, IReadOnlyList<byte> stains, uint key, bool locked) =>
        setItem.Invoke(LocalPlayerObjectIndex, slot, itemId, stains, key, locked ? ApplyFlag.Once | ApplyFlag.Lock : ApplyFlag.Once);

    /// Applies a full saved-state blob (base64, as produced by Glamourer's own export), optionally locked.
    public GlamourerApiEc ApplyState(string base64State, uint key, bool locked)
    {
        var flags = ApplyFlagEx.StateDefault;
        if (!locked)
            flags &= ~ApplyFlag.Lock;
        return applyState.Invoke(base64State, LocalPlayerObjectIndex, key, flags);
    }

    /// Glamourer requires the same key that locked a state to revert or unlock it - it does not trust
    /// objectIndex alone. The panic path still satisfies "release without the Owner's key crossing the
    /// network" by passing back whatever key the Sub's own client locally retained when it applied the
    /// lock (see PanicHandler) - key=0 for an unlocked state, per Glamourer's own default.
    public GlamourerApiEc Revert(uint key = 0) => revertState.Invoke(LocalPlayerObjectIndex, key);

    public GlamourerApiEc Unlock(uint key) => unlockState.Invoke(LocalPlayerObjectIndex, key);
}
