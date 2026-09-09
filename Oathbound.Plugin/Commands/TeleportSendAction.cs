using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;

namespace Oathbound.Plugin.Commands;

/// collar/teleport: the Owner-side "come here" resolve-compose-send action, shared by the always-visible
/// header button and the quick-access menu's favorited Teleport entry (design.md "Teleport's resolve-and-
/// send logic moves to a new TeleportSendAction static helper") - previously lived inline in
/// CollarWindow.DrawTeleportSection, but two call sites now need it and QuickAccessMenu has no CollarWindow
/// reference to call back into.
public static class TeleportSendAction
{
    /// Resolves the Owner's current world/nearest aetheryte and sends the teleport command, exactly as
    /// `DrawTeleportSection` used to. Callers render the failure message however fits their own surface -
    /// the header persists it inline (see CollarWindow.teleportResolveError), the quick-access menu shows a
    /// transient notification since its popup closes on click and has no persistent surface of its own.
    public static (bool Success, string? Error) TryResolveAndSend(Plugin plugin)
    {
        if (!plugin.LifestreamIpc.IsAvailable)
            return (false, "Requires the Lifestream plugin, installed and running on your own client.");

        var world = Plugin.ObjectTable.LocalPlayer?.CurrentWorld.Value.Name.ExtractText();
        var shardId = plugin.LifestreamIpc.TryGetActiveAetheryte();
        if (shardId == 0)
            shardId = plugin.LifestreamIpc.TryGetActiveCustomAetheryte();
        if (shardId == 0)
            shardId = plugin.LifestreamIpc.TryGetActiveResidentialAetheryte();
        // design.md "Independent ObjectTable + IDataManager scan": Lifestream's three getters above never
        // report a standalone field aetheryte (AethernetGroup == 0 aetherytes are silently excluded from
        // its own lookup table), so this covers exactly that gap, tried last.
        if (shardId == 0)
            shardId = TryResolveNearestFieldAetheryte();

        if (world is null || shardId == 0)
            return (false, "Could not resolve your current world/aetheryte - move near an aetheryte and try again.");

        plugin.ChatSender.Send(plugin.ChatComposer.ComposeTeleport(world, shardId));
        return (true, null);
    }

    /// Fallback for `LifestreamIpc`'s three getters, which can never report a standalone field aetheryte
    /// (see design.md - `Lifestream.DataStore.Aetherytes` only keeps rows with `AethernetGroup != 0`, so a
    /// plain overworld aetheryte is never in that table regardless of distance). Mirrors Lifestream's own
    /// `Utils.GetValidAetheryte()` proximity check (nearest targetable `ObjectKind.Aetheryte` within ~11y 2D
    /// / ~15y 3D) independently via `ObjectTable`, then resolves that object's `DataId` - which is the
    /// Aetheryte Excel sheet's row id directly - the same id `Teleport(destination, subIndex)` expects.
    private static uint TryResolveNearestFieldAetheryte()
    {
        var player = Plugin.ObjectTable.LocalPlayer;
        if (player is null)
            return 0;

        IGameObject? nearest = null;
        var nearestDistance2D = float.MaxValue;
        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj.ObjectKind != ObjectKind.Aetheryte || !obj.IsTargetable)
                continue;

            var distance2D = Vector2.Distance(new Vector2(player.Position.X, player.Position.Z), new Vector2(obj.Position.X, obj.Position.Z));
            var distance3D = Vector3.Distance(player.Position, obj.Position);
            if (distance2D >= 11f || distance3D >= 15f || distance2D >= nearestDistance2D)
                continue;

            nearest = obj;
            nearestDistance2D = distance2D;
        }

        if (nearest is null)
            return 0;

        return Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Aetheryte>().GetRowOrDefault(nearest.BaseId)?.RowId ?? 0;
    }
}
