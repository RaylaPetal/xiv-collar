using System.Net.WebSockets;

namespace CollarSystem.Relay;

/// A single pairing's transport session: at most one "owner" socket and one "sub" socket, matched by
/// the pairing code the two plugin clients were given out of band. The relay never inspects command
/// payloads - it only routes frames between the two slots and reports when the addressed slot is empty.
public sealed class PairingSession
{
    public required string PairingId { get; init; }
    public WebSocket? OwnerSocket { get; set; }
    public WebSocket? SubSocket { get; set; }

    public WebSocket? SlotFor(string role) => role == "owner" ? OwnerSocket : SubSocket;
    public WebSocket? PeerOf(string role) => role == "owner" ? SubSocket : OwnerSocket;

    public void SetSlot(string role, WebSocket? socket)
    {
        if (role == "owner")
            OwnerSocket = socket;
        else
            SubSocket = socket;
    }

    public bool IsEmpty => OwnerSocket is null && SubSocket is null;
}
