namespace CollarSystem.Relay;

/// Mirrors the wire shape of CollarSystem.Plugin.Relay.RelayFrame/RelayFrameType exactly (System.Text.Json's
/// default enum encoding is the ordinal int). The relay is intentionally a separate deployable from the
/// Dalamud plugin, so it keeps its own tiny copy of the wire contract rather than referencing the plugin
/// assembly - see design.md's "thin relay" framing.
public sealed class WireFrame
{
    public int Type { get; set; }
    public string Payload { get; set; } = "";
}

public sealed class WireCommandEnvelope
{
    public string PairingId { get; set; } = "";
    public string CommandId { get; set; } = "";
}

public sealed class WireDeliveryFailedEnvelope
{
    public string CommandId { get; set; } = "";
    public string Reason { get; set; } = "";
}

public static class WireFrameType
{
    public const int Command = 0;
    public const int Ack = 1;
    public const int DeliveryFailed = 2;
}
