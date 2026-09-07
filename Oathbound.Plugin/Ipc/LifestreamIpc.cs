using System;
using Dalamud.Plugin.Ipc;

namespace Oathbound.Plugin.Ipc;

/// Thin wrapper around Lifestream's (NightmareXIV/Lifestream) EzIPC surface - ships no NuGet API package,
/// so this is a hand-rolled mirror of its documented `Lifestream.<Method>` IPC tags, same "no compile-time
/// reference" shape as HonorificIpc. Unlike Glamourer/Penumbra/Honorific/Moodles, Lifestream is optional -
/// collar/teleport is the only feature that depends on it, and every call here fails soft (returns
/// false/null, never throws) so a Sub without Lifestream installed gets a clean refusal, not a crash.
/// Only wraps what collar/teleport needs: same-world nudge to the aetheryte/shard nearest the Owner, never
/// housing, chara-select automation, or custom aliases.
public sealed class LifestreamIpc
{
    private readonly ICallGateSubscriber<bool> isBusy;
    private readonly ICallGateSubscriber<string, bool> changeWorld;
    private readonly ICallGateSubscriber<uint, byte, bool> teleport;
    private readonly ICallGateSubscriber<uint, bool> aethernetTeleportById;
    private readonly ICallGateSubscriber<uint> getActiveAetheryte;
    private readonly ICallGateSubscriber<uint> getActiveCustomAetheryte;
    private readonly ICallGateSubscriber<uint> getActiveResidentialAetheryte;

    public LifestreamIpc()
    {
        isBusy = Plugin.PluginInterface.GetIpcSubscriber<bool>("Lifestream.IsBusy");
        changeWorld = Plugin.PluginInterface.GetIpcSubscriber<string, bool>("Lifestream.ChangeWorld");
        teleport = Plugin.PluginInterface.GetIpcSubscriber<uint, byte, bool>("Lifestream.Teleport");
        aethernetTeleportById = Plugin.PluginInterface.GetIpcSubscriber<uint, bool>("Lifestream.AethernetTeleportById");
        getActiveAetheryte = Plugin.PluginInterface.GetIpcSubscriber<uint>("Lifestream.GetActiveAetheryte");
        getActiveCustomAetheryte = Plugin.PluginInterface.GetIpcSubscriber<uint>("Lifestream.GetActiveCustomAetheryte");
        getActiveResidentialAetheryte = Plugin.PluginInterface.GetIpcSubscriber<uint>("Lifestream.GetActiveResidentialAetheryte");
    }

    /// collar/teleport "Refuses when travel cannot safely happen" (Lifestream unavailable case): probes
    /// `IsBusy` - the cheapest read-only call on the surface - and treats any failure (not installed, not
    /// loaded yet, IPC contract mismatch) as unavailable rather than letting an IpcNotReadyError propagate.
    public bool IsAvailable { get { try { isBusy.InvokeFunc(); return true; } catch { return false; } } }

    public bool TryIsBusy() { try { return isBusy.InvokeFunc(); } catch { return true; } }

    public bool TryChangeWorld(string world) { try { return changeWorld.InvokeFunc(world); } catch (Exception ex) { Plugin.Log.Error(ex, $"Lifestream ChangeWorld(\"{world}\") failed."); return false; } }

    public bool TryTeleport(uint destination, byte subIndex) { try { return teleport.InvokeFunc(destination, subIndex); } catch (Exception ex) { Plugin.Log.Error(ex, $"Lifestream Teleport({destination}, {subIndex}) failed."); return false; } }

    public bool TryAethernetTeleportById(uint aethernetSheetRowId) { try { return aethernetTeleportById.InvokeFunc(aethernetSheetRowId); } catch (Exception ex) { Plugin.Log.Error(ex, $"Lifestream AethernetTeleportById({aethernetSheetRowId}) failed."); return false; } }

    /// Returns 0 (Lifestream's own "none active" sentinel) on failure, matching its documented return shape.
    public uint TryGetActiveAetheryte() { try { return getActiveAetheryte.InvokeFunc(); } catch { return 0; } }
    public uint TryGetActiveCustomAetheryte() { try { return getActiveCustomAetheryte.InvokeFunc(); } catch { return 0; } }
    public uint TryGetActiveResidentialAetheryte() { try { return getActiveResidentialAetheryte.InvokeFunc(); } catch { return 0; } }
}
