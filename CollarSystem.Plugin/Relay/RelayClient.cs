using System;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CollarSystem.Plugin.Relay;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
}

/// Websocket client for the Owner<->Sub command channel (design.md, "Relay" decision). Carries
/// CommandEnvelope down and AckEnvelope back over one connection; never touches in-game chat.
/// Auto-reconnects with backoff on an unexpected drop (design.md, "Auto-reconnect ... gated by an
/// explicit intentional-disconnect flag") - a deliberate Disconnect() (panic/unpair) never reconnects.
public sealed class RelayClient : IDisposable
{
    private const int InitialBackoffSeconds = 1;
    private const int MaxBackoffSeconds = 30;

    public event Action<CommandEnvelope>? CommandReceived;
    public event Action<AckEnvelope>? AckReceived;
    public event Action<DeliveryFailedEnvelope>? DeliveryFailed;
    public event Action<Exception>? ConnectionLost;
    public event Action? Reconnected;

    private ClientWebSocket? socket;
    private CancellationTokenSource? receiveCts;
    private CancellationTokenSource? reconnectCts;
    private Uri? lastUri;
    private bool intentionalDisconnect;

    public ConnectionState ConnectionState { get; private set; } = ConnectionState.Disconnected;

    public bool IsConnected => ConnectionState == ConnectionState.Connected;

    /// Explicit connect: always resets the intentional-disconnect flag and remembers `relayUri` as the
    /// target the reconnect loop should use if this connection later drops unexpectedly.
    public async Task ConnectAsync(Uri relayUri, CancellationToken cancellationToken = default)
    {
        StopReconnectLoop();
        DisconnectSocketOnly();

        lastUri = relayUri;
        intentionalDisconnect = false;
        ConnectionState = ConnectionState.Connecting;
        await ConnectCoreAsync(relayUri, cancellationToken).ConfigureAwait(false);
    }

    /// Deliberate disconnect (panic, manual unpair): must never trigger a reconnect attempt.
    public void Disconnect()
    {
        intentionalDisconnect = true;
        StopReconnectLoop();
        DisconnectSocketOnly();
        ConnectionState = ConnectionState.Disconnected;
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

    /// Does not touch ConnectionState on entry - callers set it beforehand (Connecting for an explicit
    /// ConnectAsync, Reconnecting for every attempt in the backoff loop), so a failed retry attempt
    /// doesn't flicker the visible status away from "Reconnecting" and back.
    private async Task ConnectCoreAsync(Uri relayUri, CancellationToken cancellationToken)
    {
        var newSocket = new ClientWebSocket();
        var cts = new CancellationTokenSource();
        await newSocket.ConnectAsync(relayUri, cancellationToken).ConfigureAwait(false);

        socket = newSocket;
        receiveCts = cts;
        ConnectionState = ConnectionState.Connected;
        _ = Task.Run(() => ReceiveLoopAsync(cts.Token));
    }

    private void DisconnectSocketOnly()
    {
        receiveCts?.Cancel();
        socket?.Dispose();
        socket = null;
        receiveCts?.Dispose();
        receiveCts = null;
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
                    {
                        HandleUnexpectedDisconnect(new IOException("Relay closed the connection."));
                        return;
                    }
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
            // Expected on Disconnect()/Dispose() - intentional, never treated as a drop to recover from.
        }
        catch (Exception ex)
        {
            HandleUnexpectedDisconnect(ex);
        }
    }

    /// Common tail for both a mid-stream exception and a graceful server-initiated close: neither is
    /// intentional from this client's perspective, so both go through the same reconnect decision.
    private void HandleUnexpectedDisconnect(Exception ex)
    {
        DisconnectSocketOnly();

        if (intentionalDisconnect)
        {
            ConnectionState = ConnectionState.Disconnected;
            return;
        }

        ConnectionState = ConnectionState.Reconnecting;
        ConnectionLost?.Invoke(ex);
        StartReconnectLoop();
    }

    private void StartReconnectLoop()
    {
        if (lastUri is not { } uri)
            return;

        StopReconnectLoop();
        reconnectCts = new CancellationTokenSource();
        _ = Task.Run(() => ReconnectLoopAsync(uri, reconnectCts.Token));
    }

    private async Task ReconnectLoopAsync(Uri uri, CancellationToken cancellationToken)
    {
        var delaySeconds = InitialBackoffSeconds;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken).ConfigureAwait(false);
                await ConnectCoreAsync(uri, cancellationToken).ConfigureAwait(false);
                Reconnected?.Invoke();
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                delaySeconds = Math.Min(delaySeconds * 2, MaxBackoffSeconds);
            }
        }
    }

    private void StopReconnectLoop()
    {
        reconnectCts?.Cancel();
        reconnectCts?.Dispose();
        reconnectCts = null;
    }

    public void Dispose()
    {
        intentionalDisconnect = true;
        StopReconnectLoop();
        DisconnectSocketOnly();
    }
}
