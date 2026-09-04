using System;
using System.Linq;
using Oathbound.Plugin.Config;
using Oathbound.Plugin.Safety;
using ECommons.Automation;

namespace Oathbound.Plugin.Commands;

/// collar/follow: movement-lock (leash) enforcement, gated behind its own "Follow" permission which
/// ChatCommandListener checks before Engage/Release ever runs - the same dedicated opt-in the spec
/// requires, kept separate from the other three categories by construction.
public sealed class FollowCommand
{
    private const string Owner = "Follow";

    private readonly MovementLockService movementLock;
    private readonly SubRuntimeState runtimeState;
    private readonly PluginConfig config;
    private ulong followedObjectId;

    public FollowCommand(PluginConfig config, MovementLockService movementLock, SubRuntimeState runtimeState)
    {
        this.config = config;
        this.movementLock = movementLock;
        this.runtimeState = runtimeState;
    }

    public bool Engage()
    {
        if (!movementLock.IsAvailable)
            return false;

        var owner = Plugin.ObjectTable.FirstOrDefault(o => string.Equals(o.Name.TextValue, config.Pairing.PeerName, StringComparison.OrdinalIgnoreCase));
        if (owner is null)
        {
            Plugin.Log.Warning($"Leash refused: paired Owner '{config.Pairing.PeerName}' is not a targetable player in the current area.");
            return false;
        }

        Plugin.TargetManager.Target = owner;
        Chat.SendMessage("/follow <t>");

        movementLock.EngagePreserveFollow(Owner);
        followedObjectId = owner.GameObjectId;
        runtimeState.MovementLockActive = true;
        return true;
    }

    public void Release()
    {
        movementLock.ReleasePreserveFollow(Owner);
        if (runtimeState.MovementLockActive)
            Chat.SendMessage("/follow");
        followedObjectId = 0;
        runtimeState.MovementLockActive = false;
    }

    public void OnFrameworkUpdate()
    {
        if (!runtimeState.MovementLockActive || followedObjectId == 0) return;
        if (Plugin.ObjectTable.Any(o => o.GameObjectId == followedObjectId)) return;
        Plugin.Log.Warning("Leash released: the paired Owner is no longer present in the current area.");
        Release();
    }
}
