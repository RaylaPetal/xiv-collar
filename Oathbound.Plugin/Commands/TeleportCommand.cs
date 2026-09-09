using System;
using Dalamud.Game.ClientState.Conditions;
using Oathbound.Plugin.Config;
using Oathbound.Plugin.Ipc;

namespace Oathbound.Plugin.Commands;

/// collar/teleport: same-world "come to the Owner" command. Applying it moves the Sub to the Owner's world
/// (if different) and then to the aetheryte/aethernet shard nearest the Owner's position at send time -
/// never the Owner's exact coordinates (that needs vnavmesh too; deliberately out of scope, see design.md).
/// Every guard runs on the Sub's own client before Lifestream is ever called - the Sub's client protects
/// itself, it never trusts the Owner's tell to have already checked duty/combat/permission.
public sealed class TeleportCommand
{
    private const string MovementLockOwner = "Teleport";

    private readonly PluginConfig config;
    private readonly LifestreamIpc lifestream;
    private readonly MovementLockService movementLock;
    private bool teleportInProgress;
    private bool hasObservedTravelBusy;

    public TeleportCommand(PluginConfig config, LifestreamIpc lifestream, MovementLockService movementLock)
    {
        this.config = config;
        this.lifestream = lifestream;
        this.movementLock = movementLock;
    }

    /// `Lifestream.IsBusy` alone isn't enough here: Lifestream considers a direct aetheryte teleport "done"
    /// as soon as it has fired the in-game Teleport action, before that action's own cast bar (ConditionFlag
    /// Casting87) or the following loading screen (BetweenAreas/BetweenAreas51) actually runs - polling only
    /// IsBusy released the lock before real travel began, letting the Sub walk right through it. So this
    /// waits for travel to actually be observed busy at least once (Lifestream OR the game's own casting/
    /// loading state) before it will treat "not busy" as "finished", the same duplicate-flag pattern already
    /// used for the duty guard above. No fail-safe cap either way - PanicHandler.movementLock.ReleaseAll()
    /// remains the backstop if travel is never observed busy at all.
    public void OnFrameworkUpdate()
    {
        if (!teleportInProgress) return;

        if (IsTravelBusy())
        {
            hasObservedTravelBusy = true;
            return;
        }
        if (!hasObservedTravelBusy) return;

        ReleaseMovementLock();
    }

    private bool IsTravelBusy() =>
        lifestream.TryIsBusy()
        || Plugin.Condition[ConditionFlag.Casting87]
        || Plugin.Condition[ConditionFlag.BetweenAreas]
        || Plugin.Condition[ConditionFlag.BetweenAreas51];

    /// collar/teleport "Separate opt-in permission for teleport" + "Gated by the existing automation-risk
    /// acknowledgement" + "Refuses when travel cannot safely happen": checked in that order so the reason
    /// reported is always the first thing actually blocking the command, matching every other
    /// ForceApply-shaped result in this codebase.
    public (bool Success, string? Reason) Apply(string world, uint shardId)
    {
        if (!config.Permissions.Teleport)
            return (false, "Teleport permission is not enabled.");
        if (!config.TosAcknowledged)
            return (false, "The automation-risk acknowledgement has not been given.");
        if (Plugin.Condition[ConditionFlag.BoundByDuty] || Plugin.Condition[ConditionFlag.BoundByDuty56] || Plugin.Condition[ConditionFlag.BoundByDuty95])
            return (false, "Refused: currently bound by a duty.");
        if (Plugin.Condition[ConditionFlag.InCombat])
            return (false, "Refused: currently in combat.");
        if (!lifestream.IsAvailable)
            return (false, "Lifestream is not installed or not responding.");

        // design.md "Lock starts before TryChangeWorld": a cross-world hop is travel the Sub could otherwise
        // interrupt just as much as the teleport itself, so the lock covers both from here on.
        movementLock.EngageImmobilize(MovementLockOwner);
        teleportInProgress = true;
        hasObservedTravelBusy = false;

        var currentWorld = Plugin.ObjectTable.LocalPlayer?.CurrentWorld.Value.Name.ExtractText();
        if (currentWorld is not null && !string.Equals(currentWorld, world, System.StringComparison.OrdinalIgnoreCase))
        {
            if (!lifestream.TryChangeWorld(world))
            {
                ReleaseMovementLock();
                return (false, $"Failed to change world to \"{world}\".");
            }
        }

        // Uses the aetheryte teleport spell (works from anywhere in the world, unlike aethernet travel
        // which requires already being within range of a shard) - subIndex 0 covers every plain aetheryte;
        // the carried id always comes from GetActiveAetheryte, never a housing/aethernet-only id.
        if (!lifestream.TryTeleport(shardId, 0))
        {
            ReleaseMovementLock();
            return (false, "Lifestream failed to travel to the requested destination.");
        }

        return (true, null);
    }

    private void ReleaseMovementLock()
    {
        teleportInProgress = false;
        hasObservedTravelBusy = false;
        movementLock.ReleaseImmobilize(MovementLockOwner);
    }

    /// Parses `world:"<name>" shard:<id>`, same shape as `RestraintCommand.TryParseWearCommand`'s
    /// quoted-value token parsing.
    public static bool TryParsePayload(string rest, out string world, out uint shardId)
    {
        world = "";
        shardId = 0;

        const string worldPrefix = "world:\"";
        var trimmed = rest.Trim();
        if (!trimmed.StartsWith(worldPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var afterPrefix = trimmed[worldPrefix.Length..];
        var closing = afterPrefix.IndexOf('"');
        if (closing < 0)
            return false;

        world = afterPrefix[..closing];
        var tail = afterPrefix[(closing + 1)..].Trim();

        const string shardPrefix = "shard:";
        if (!tail.StartsWith(shardPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        return world.Length > 0 && uint.TryParse(tail[shardPrefix.Length..].Trim(), out shardId);
    }
}
