using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CollarSystem.Relay;

var builder = WebApplication.CreateBuilder(args);

// Match PluginConfig.RelayUrl's default (ws://localhost:5099/collar) so a plain `dotnet run` here just
// works for local testing. ASPNETCORE_URLS (set explicitly, or by the Dockerfile for a real deployment)
// always takes precedence over this.
if (Environment.GetEnvironmentVariable("ASPNETCORE_URLS") is null)
    builder.WebHost.UseUrls("http://localhost:5099");

var app = builder.Build();

app.UseWebSockets();

var sessions = new ConcurrentDictionary<string, PairingSession>();

// Sub's client calls this to obtain a fresh pairing code before sharing it with an Owner out of band.
// Nothing routes through a session until both a "sub" and an "owner" socket have connected to it, and
// applying any command still requires the Sub's own explicit accept inside the plugin (collar/pairing) -
// this endpoint only stands up the transport session, it never itself constitutes consent.
app.MapPost("/pairing/sessions", () =>
{
    var pairingId = GeneratePairingCode();
    sessions[pairingId] = new PairingSession { PairingId = pairingId };
    return Results.Ok(new { pairingId });
});

app.Map("/collar", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var pairingId = context.Request.Query["pairingId"].ToString();
    var role = context.Request.Query["role"].ToString();
    if (string.IsNullOrEmpty(pairingId) || role is not ("owner" or "sub"))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var session = sessions.GetOrAdd(pairingId, id => new PairingSession { PairingId = id });
    if (session.SlotFor(role) is not null)
    {
        // A slot only ever holds one live connection - reject a second one rather than silently hijacking it.
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    session.SetSlot(role, socket);

    try
    {
        await RelayLoopAsync(session, role, socket, context.RequestAborted);
    }
    finally
    {
        session.SetSlot(role, null);
        if (session.IsEmpty)
            sessions.TryRemove(pairingId, out _);
    }
});

app.Run();

static async Task RelayLoopAsync(PairingSession session, string role, WebSocket socket, CancellationToken cancellationToken)
{
    var buffer = new byte[8192];

    while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
    {
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
                return;
            stream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        var bytes = stream.ToArray();
        var peer = session.PeerOf(role);

        if (peer is { State: WebSocketState.Open })
        {
            await peer.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
            continue;
        }

        // collar/relay: "Sub offline" (and symmetrically, Owner offline) - tell the sender delivery failed
        // instead of silently dropping the frame. Only meaningful for Command frames; Acks and delivery
        // failures themselves aren't retried.
        var frame = JsonSerializer.Deserialize<WireFrame>(bytes);
        if (frame is { Type: WireFrameType.Command })
        {
            var command = JsonSerializer.Deserialize<WireCommandEnvelope>(frame.Payload);
            var failure = new WireFrame
            {
                Type = WireFrameType.DeliveryFailed,
                Payload = JsonSerializer.Serialize(new WireDeliveryFailedEnvelope
                {
                    CommandId = command?.CommandId ?? "",
                    Reason = "peer not connected",
                }),
            };
            await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(failure), WebSocketMessageType.Text, true, cancellationToken);
        }
    }
}

static string GeneratePairingCode()
{
    Span<byte> bytes = stackalloc byte[6];
    RandomNumberGenerator.Fill(bytes);
    var builder = new StringBuilder(12);
    foreach (var b in bytes)
        builder.Append(b.ToString("X2"));
    return builder.ToString();
}
