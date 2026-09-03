using System;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CollarSystem.Plugin.Relay;

/// Websocket client for the Owner<->Sub command channel (design.md, "Relay" decision). Carries
/// CommandEnvelope down and AckEnvelope back over one connection; never touches in-game chat.
public sealed class RelayClient : IDisposable
{
    public event Action<CommandEnvelope>? CommandReceived;
    public event Action<AckEnvelope>? AckReceived;
    public event Action<DeliveryFailedEnvelope>? DeliveryFailed;
    public event Action<Exception>? ConnectionLost;

    private ClientWebSocket? socket;
    private CancellationTokenSource? receiveCts;
    private Task? receiveLoop;

    public bool IsConnected => socket is { State: WebSocketState.Open };

    public async Task ConnectAsync(Uri relayUri, CancellationToken cancellationToken = default)
    {
        Disconnect();

        socket = new ClientWebSocket();
        receiveCts = new CancellationTokenSource();
        await socket.ConnectAsync(relayUri, cancellationToken).ConfigureAwait(false);
        receiveLoop = Task.Run(() => ReceiveLoopAsync(receiveCts.Token));
    }

    public void Disconnect()
    {
        receiveCts?.Cancel();
        socket?.Dispose();
        socket = null;
        receiveCts?.Dispose();
        receiveCts = null;
    }

    public Task SendCommandAsync(CommandEnvelope envelope, CancellationToken cancellationToken = default) =>
        SendFrameAsync(new RelayFrame { Type = RelayFrameType.Command, Payload = JsonSerializer.Serialize(envelope) }, cancellationToken);

    public Task SendAckAsync(AckEnvelope envelope, CancellationToken cancellationToken = default) =>
        SendFrameAsync(new RelayFrame { Type = RelayFrameType.Ack, Payload = JsonSerializer.Serialize(envelope) }, cancellationToken);

    private async Task SendFrameAsync(RelayFrame frame, CancellationToken cancellationToken)
    {
        if (socket is not { State: WebSocketState.Open } activeSocket)
            throw new InvalidOperationException("Relay is not connected.");

        var bytes = JsonSerializer.SerializeToUtf8Bytes(frame);
        await activeSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var activeSocket = socket!;
        var buffer = new byte[8192];

        try
        {
            while (!cancellationToken.IsCancellationRequested && activeSocket.State == WebSocketState.Open)
            {
                using var stream = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await activeSocket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                        return;
                    stream.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                var frame = JsonSerializer.Deserialize<RelayFrame>(stream.ToArray());
                if (frame is null)
                    continue;

                switch (frame.Type)
                {
                    case RelayFrameType.Command:
                        var command = JsonSerializer.Deserialize<CommandEnvelope>(frame.Payload);
                        if (command != null)
                            CommandReceived?.Invoke(command);
                        break;
                    case RelayFrameType.Ack:
                        var ack = JsonSerializer.Deserialize<AckEnvelope>(frame.Payload);
                        if (ack != null)
                            AckReceived?.Invoke(ack);
                        break;
                    case RelayFrameType.DeliveryFailed:
                        var failure = JsonSerializer.Deserialize<DeliveryFailedEnvelope>(frame.Payload);
                        if (failure != null)
                            DeliveryFailed?.Invoke(failure);
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on Disconnect()/Dispose().
        }
        catch (Exception ex)
        {
            ConnectionLost?.Invoke(ex);
        }
    }

    public void Dispose() => Disconnect();
}
