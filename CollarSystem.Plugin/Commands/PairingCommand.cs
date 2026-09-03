using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CollarSystem.Plugin.Config;
using CollarSystem.Plugin.Relay;

namespace CollarSystem.Plugin.Commands;

public enum PairingKind
{
    Request,
    Accept,
    Decline,
    Unpair,
}

public sealed class PairingPayload
{
    public PairingKind Kind { get; set; }
    public string PeerName { get; set; } = "";
}

/// Owns the pairing handshake state machine (collar/pairing). Deliberately has no auto-accept path:
/// an inbound Request only ever raises IncomingPairingRequest for the Sub's UI to show; Confirmed only
/// flips true from ExplicitAccept, which must be a direct user action.
public sealed class PairingCommand
{
    private static readonly HttpClient Http = new();

    private readonly PluginConfig config;
    private readonly RelayClient relay;

    public event Action<string /* peerName */>? IncomingPairingRequest;
    public event Action? PairingConfirmed;
    public event Action? PairingEnded;

    public PairingCommand(PluginConfig config, RelayClient relay)
    {
        this.config = config;
        this.relay = relay;
    }

    /// Sub-side: obtain a fresh pairing code from the relay and open the transport for it. The code is
    /// shared with the Owner out of band (voice, chat, etc.) - obtaining it is not itself consent.
    public async Task<string> GeneratePairingCodeAsync()
    {
        var response = await Http.PostAsync($"{DeriveHttpBaseUrl(config.RelayUrl)}/pairing/sessions", content: null).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<PairingSessionResponse>().ConfigureAwait(false);
        var pairingId = body?.PairingId ?? throw new InvalidOperationException("Relay did not return a pairing code.");

        config.Pairing = new PairingState { PairingId = pairingId, Confirmed = false };
        config.Save();

        await relay.ConnectAsync(BuildRelayUri(config.RelayUrl, pairingId, "sub")).ConfigureAwait(false);
        return pairingId;
    }

    /// Owner-side: connect using a code the Sub shared, then ask to pair. Nothing is applied on the Sub's
    /// end until the Sub's own client explicitly accepts.
    public async Task RequestPairingAsync(string pairingCode, string ownerName)
    {
        config.Pairing = new PairingState { PairingId = pairingCode, Confirmed = false };
        config.Save();

        await relay.ConnectAsync(BuildRelayUri(config.RelayUrl, pairingCode, "owner")).ConfigureAwait(false);
        await SendAsync(new PairingPayload { Kind = PairingKind.Request, PeerName = ownerName }).ConfigureAwait(false);
    }

    /// Sub-side: the only path that can ever set Confirmed = true. Must be wired to a direct UI action.
    public Task ExplicitAcceptAsync(string peerName, string subName)
    {
        config.Pairing.Confirmed = true;
        config.Pairing.PeerName = peerName;
        config.Save();
        PairingConfirmed?.Invoke();
        return SendAsync(new PairingPayload { Kind = PairingKind.Accept, PeerName = subName });
    }

    public Task DeclineAsync()
    {
        var task = SendAsync(new PairingPayload { Kind = PairingKind.Decline, PeerName = "" });
        config.Pairing = new PairingState();
        config.Save();
        return task;
    }

    /// Ends the pairing locally first (so this always succeeds even if the relay is unreachable, per
    /// collar/pairing's panic requirement), then best-effort notifies the peer.
    public void EndPairingLocally()
    {
        config.Pairing = new PairingState();
        config.Save();
        relay.Disconnect();
        PairingEnded?.Invoke();
    }

    public AckStatus HandleIncoming(CommandEnvelope envelope)
    {
        var payload = JsonSerializer.Deserialize<PairingPayload>(envelope.Payload) ?? throw new InvalidOperationException("Malformed pairing payload.");

        switch (payload.Kind)
        {
            case PairingKind.Request:
                // Never auto-accept - only surface it. Confirmed stays false until ExplicitAcceptAsync.
                IncomingPairingRequest?.Invoke(payload.PeerName);
                return AckStatus.Applied;

            case PairingKind.Accept:
                config.Pairing.Confirmed = true;
                config.Pairing.PeerName = payload.PeerName;
                config.Save();
                PairingConfirmed?.Invoke();
                return AckStatus.Applied;

            case PairingKind.Decline:
            case PairingKind.Unpair:
                config.Pairing = new PairingState();
                config.Save();
                relay.Disconnect();
                PairingEnded?.Invoke();
                return AckStatus.Applied;

            default:
                return AckStatus.Rejected;
        }
    }

    private Task SendAsync(PairingPayload payload) =>
        relay.SendCommandAsync(new CommandEnvelope
        {
            PairingId = config.Pairing.PairingId ?? "",
            Category = CommandCategory.Pairing,
            Payload = JsonSerializer.Serialize(payload),
        });

    private static Uri BuildRelayUri(string relayUrl, string pairingId, string role) =>
        new($"{relayUrl.TrimEnd('/')}?pairingId={Uri.EscapeDataString(pairingId)}&role={role}");

    /// The relay hosts both the websocket endpoint and the plain-HTTP pairing-code endpoint on the same
    /// origin (see CollarSystem.Relay/Program.cs) - only the scheme and path differ.
    private static string DeriveHttpBaseUrl(string relayUrl)
    {
        var uri = new Uri(relayUrl);
        var scheme = uri.Scheme == "wss" ? "https" : "http";
        return $"{scheme}://{uri.Authority}";
    }

    private sealed class PairingSessionResponse
    {
        [JsonPropertyName("pairingId")]
        public string PairingId { get; set; } = "";
    }
}
