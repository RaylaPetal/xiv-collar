using System;

namespace CollarSystem.Plugin.Relay;

public enum CommandCategory
{
    Pairing,
    Title,
    Outfit,
    Gesture,
    Follow,
}

public enum AckStatus
{
    Applied,
    Rejected,
    Failed,
}

/// The envelope every relay message rides in, as decided in design.md - uniform regardless of which
/// category's payload it wraps. `Payload` is the category-specific command payload, JSON-encoded.
public sealed class CommandEnvelope
{
    public string PairingId { get; set; } = "";
    public CommandCategory Category { get; set; }
    public string CommandId { get; set; } = Guid.NewGuid().ToString("N");
    public string Payload { get; set; } = "";
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

public sealed class AckEnvelope
{
    public string PairingId { get; set; } = "";
    public string CommandId { get; set; } = "";
    public AckStatus Status { get; set; }
    public string? Detail { get; set; }
}

/// Sent back by the relay itself (never by a peer client) when it could not forward a frame because
/// the addressed peer isn't connected to this pairing session - satisfies collar/relay's "Sub offline"
/// scenario without overloading AckEnvelope's application-level applied/rejected/failed meaning.
public sealed class DeliveryFailedEnvelope
{
    public string CommandId { get; set; } = "";
    public string Reason { get; set; } = "";
}

internal enum RelayFrameType
{
    Command,
    Ack,
    DeliveryFailed,
}

/// The outermost wire frame: distinguishes a command/ack/delivery-failure so one socket carries all of it.
internal sealed class RelayFrame
{
    public RelayFrameType Type { get; set; }
    public string Payload { get; set; } = "";
}
