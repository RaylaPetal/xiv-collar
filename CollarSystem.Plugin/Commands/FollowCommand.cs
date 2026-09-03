using System.Text.Json;
using System.Threading.Tasks;
using CollarSystem.Plugin.Config;
using CollarSystem.Plugin.Relay;
using CollarSystem.Plugin.Safety;

namespace CollarSystem.Plugin.Commands;

public sealed class FollowPayload
{
    public bool Engage { get; set; }
}

/// collar/follow: movement-lock (leash) enforcement, gated behind its own "Follow" permission
/// (config.Permissions.Follow) which CommandDispatcher checks before Handle ever runs - the same
/// dedicated opt-in the spec requires, kept separate from the other three categories by construction.
public sealed class FollowCommand
{
    private readonly PluginConfig config;
    private readonly RelayClient relay;
    private readonly MovementLockService movementLock;
    private readonly SubRuntimeState runtimeState;

    public FollowCommand(PluginConfig config, RelayClient relay, MovementLockService movementLock, SubRuntimeState runtimeState)
    {
        this.config = config;
        this.relay = relay;
        this.movementLock = movementLock;
        this.runtimeState = runtimeState;
    }

    public Task SendEngageAsync() => SendAsync(new FollowPayload { Engage = true });
    public Task SendReleaseAsync() => SendAsync(new FollowPayload { Engage = false });

    public AckStatus Handle(CommandEnvelope envelope)
    {
        var payload = JsonSerializer.Deserialize<FollowPayload>(envelope.Payload);
        if (payload is null)
            return AckStatus.Failed;

        if (payload.Engage)
        {
            if (!movementLock.IsAvailable)
                return AckStatus.Failed;

            movementLock.Engage();
            runtimeState.MovementLockActive = true;
        }
        else
        {
            movementLock.Release();
            runtimeState.MovementLockActive = false;
        }

        return AckStatus.Applied;
    }

    private Task SendAsync(FollowPayload payload) => relay.SendCommandAsync(new CommandEnvelope
    {
        PairingId = config.Pairing.PairingId ?? "",
        Category = CommandCategory.Follow,
        Payload = JsonSerializer.Serialize(payload),
    });
}
