using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using CollarSystem.Plugin.Config;
using CollarSystem.Plugin.Ipc;
using CollarSystem.Plugin.Relay;
using CollarSystem.Plugin.Safety;

namespace CollarSystem.Plugin.Commands;

public sealed class TitlePayload
{
    public bool Clear { get; set; }
    public string? Title { get; set; }
    public bool IsPrefix { get; set; }
    public float[]? Color { get; set; }
    public float[]? Glow { get; set; }
}

/// collar/title: Owner-issued title commands applied via Honorific on the Sub's own client.
public sealed class TitleCommand
{
    private readonly PluginConfig config;
    private readonly RelayClient relay;
    private readonly HonorificIpc honorific;
    private readonly SubRuntimeState runtimeState;

    public TitleCommand(PluginConfig config, RelayClient relay, HonorificIpc honorific, SubRuntimeState runtimeState)
    {
        this.config = config;
        this.relay = relay;
        this.honorific = honorific;
        this.runtimeState = runtimeState;
    }

    public Task SendSetAsync(string title, bool isPrefix, Vector3? color, Vector3? glow) => SendAsync(new TitlePayload
    {
        Clear = false,
        Title = title,
        IsPrefix = isPrefix,
        Color = ToArray(color),
        Glow = ToArray(glow),
    });

    public Task SendClearAsync() => SendAsync(new TitlePayload { Clear = true });

    /// Sub-side inbound handling. The "title" permission is checked by CommandDispatcher before this
    /// runs, matching design.md's "permission gate in one place per category" decision.
    public AckStatus Handle(CommandEnvelope envelope)
    {
        var payload = JsonSerializer.Deserialize<TitlePayload>(envelope.Payload);
        if (payload is null)
            return AckStatus.Failed;

        if (payload.Clear)
        {
            honorific.ClearTitle();
            runtimeState.TitleApplied = false;
        }
        else
        {
            honorific.SetTitle(new HonorificTitleData
            {
                Title = payload.Title ?? "",
                IsPrefix = payload.IsPrefix,
                Color = ToVector3(payload.Color),
                Glow = ToVector3(payload.Glow),
            });
            runtimeState.TitleApplied = true;
        }

        return AckStatus.Applied;
    }

    private Task SendAsync(TitlePayload payload) => relay.SendCommandAsync(new CommandEnvelope
    {
        PairingId = config.Pairing.PairingId ?? "",
        Category = CommandCategory.Title,
        Payload = JsonSerializer.Serialize(payload),
    });

    private static float[]? ToArray(Vector3? v) => v is { } value ? [value.X, value.Y, value.Z] : null;
    private static Vector3? ToVector3(float[]? a) => a is { Length: 3 } ? new Vector3(a[0], a[1], a[2]) : null;
}
