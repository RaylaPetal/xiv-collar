using System;
using CollarSystem.Plugin.Config;
using CollarSystem.Plugin.Relay;

namespace CollarSystem.Plugin.Commands;

/// Routes inbound relay commands to the right handler and enforces permissions in one place before any
/// IPC call happens (design.md's "Permissions stored per-category, checked at the Sub before any IPC
/// call" decision). Pairing commands bypass the permission gate - they aren't a category a Sub toggles,
/// they're the handshake collar/pairing itself depends on.
public sealed class CommandDispatcher : IDisposable
{
    private readonly PluginConfig config;
    private readonly RelayClient relay;
    private readonly PairingCommand pairing;
    private readonly TitleCommand title;
    private readonly OutfitCommand outfit;
    private readonly GestureCommand gesture;
    private readonly FollowCommand follow;

    public event Action<CommandCategory, string /* commandId */>? CommandRejectedByPermission;

    public CommandDispatcher(
        PluginConfig config,
        RelayClient relay,
        PairingCommand pairing,
        TitleCommand title,
        OutfitCommand outfit,
        GestureCommand gesture,
        FollowCommand follow)
    {
        this.config = config;
        this.relay = relay;
        this.pairing = pairing;
        this.title = title;
        this.outfit = outfit;
        this.gesture = gesture;
        this.follow = follow;

        relay.CommandReceived += OnCommandReceived;
    }

    private void OnCommandReceived(CommandEnvelope envelope)
    {
        // collar/pairing: nothing applies until the Sub has explicitly accepted, and every non-pairing
        // command must belong to the currently active pairing.
        if (envelope.Category != CommandCategory.Pairing)
        {
            if (!config.Pairing.IsPaired || envelope.PairingId != config.Pairing.PairingId)
                return;

            if (!HasPermission(envelope.Category))
            {
                CommandRejectedByPermission?.Invoke(envelope.Category, envelope.CommandId);
                Ack(envelope, AckStatus.Rejected, "permission disabled");
                return;
            }
        }

        AckStatus status;
        string? detail = null;
        try
        {
            status = envelope.Category switch
            {
                CommandCategory.Pairing => pairing.HandleIncoming(envelope),
                CommandCategory.Title => title.Handle(envelope),
                CommandCategory.Outfit => outfit.Handle(envelope),
                CommandCategory.Gesture => gesture.Handle(envelope),
                CommandCategory.Follow => follow.Handle(envelope),
                _ => AckStatus.Rejected,
            };
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Failed to apply {envelope.Category} command {envelope.CommandId}.");
            status = AckStatus.Failed;
            detail = ex.Message;
        }

        Ack(envelope, status, detail);
    }

    private bool HasPermission(CommandCategory category) => category switch
    {
        CommandCategory.Title => config.Permissions.Title,
        CommandCategory.Outfit => config.Permissions.Outfit,
        CommandCategory.Gesture => config.Permissions.Gesture,
        CommandCategory.Follow => config.Permissions.Follow,
        _ => false,
    };

    private void Ack(CommandEnvelope envelope, AckStatus status, string? detail) =>
        _ = relay.SendAckAsync(new AckEnvelope
        {
            PairingId = envelope.PairingId,
            CommandId = envelope.CommandId,
            Status = status,
            Detail = detail,
        });

    public void Dispose() => relay.CommandReceived -= OnCommandReceived;
}
