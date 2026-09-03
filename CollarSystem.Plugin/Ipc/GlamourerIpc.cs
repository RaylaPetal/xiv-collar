using System.Collections.Generic;
using Glamourer.Api.Enums;
using Glamourer.Api.IpcSubscribers;

namespace CollarSystem.Plugin.Ipc;

/// Thin wrapper around the Glamourer.Api calls collar/outfit needs, always targeting the local player
/// (objectIndex 0) - see design.md's Context: only the local client's own state change reaches anyone else.
public sealed class GlamourerIpc
{
    private const int LocalPlayerObjectIndex = 0;

    private readonly SetItem setItem;
    private readonly ApplyState applyState;
    private readonly RevertState revertState;
    private readonly UnlockState unlockState;

    public GlamourerIpc()
    {
        setItem = new SetItem(Plugin.PluginInterface);
        applyState = new ApplyState(Plugin.PluginInterface);
        revertState = new RevertState(Plugin.PluginInterface);
        unlockState = new UnlockState(Plugin.PluginInterface);
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
