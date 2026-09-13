using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buttplug.Client;
using Buttplug.Core.Messages;

namespace Oathbound.Plugin.Ipc;

/// collar/toy-control: wraps the official `Buttplug` NuGet client library for talking to a locally-running
/// Intiface Central instance over its WebSocket server - structurally different from every other `Ipc/`
/// wrapper in this plugin, since Intiface Central is a standalone companion application, not a Dalamud
/// plugin reachable through Dalamud's IPC call-gate bus. Every public method is fail-closed: a missing
/// Intiface install, an unreachable WebSocket, or any Buttplug-side exception is caught and reported as
/// unavailable/failed rather than thrown, matching every other `Ipc/` wrapper's posture. Connect/vibrate/
/// stop are all fire-and-forget from the caller's perspective (never blocks the calling thread on network
/// I/O) - callers read `IsConnected`/`IsConnecting` each frame instead of awaiting a result, the same
/// "poll a flag every ImGui frame" shape the rest of this plugin's connection-status UI already uses.
public sealed class IntifaceIpc : IDisposable
{
    private readonly ButtplugClient client = new("Oathbound");
    private CancellationTokenSource? connectionCts;

    public bool IsConnected => client.Connected;
    public bool IsConnecting { get; private set; }
    public string? LastError { get; private set; }
    public int ConnectedDeviceCount => IsConnected ? client.Devices.Length : 0;

    public void Connect(string address)
    {
        if (IsConnecting || IsConnected) return;
        IsConnecting = true;
        LastError = null;
        connectionCts = new CancellationTokenSource();
        _ = ConnectAsync(address, connectionCts.Token);
    }

    private async Task ConnectAsync(string address, CancellationToken token)
    {
        try
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
            {
                LastError = "Invalid Intiface address.";
                return;
            }
            var connector = new ButtplugWebsocketConnector(uri);
            await client.ConnectAsync(connector, token);
            await client.StartScanningAsync(token);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Plugin.Log.Warning(ex, "IntifaceIpc: failed to connect to Intiface Central.");
        }
        finally
        {
            IsConnecting = false;
        }
    }

    public void Disconnect()
    {
        connectionCts?.Cancel();
        if (!IsConnected) return;
        _ = DisconnectAsync();
    }

    private async Task DisconnectAsync()
    {
        try { await client.DisconnectAsync(); }
        catch (Exception ex) { Plugin.Log.Warning(ex, "IntifaceIpc: error while disconnecting from Intiface Central."); }
    }

    /// Runs a vibration output at `intensityFraction` (0.0-1.0) against every vibrate-capable feature on
    /// every currently connected device - collar/toy-control "Commands apply uniformly to all connected
    /// devices". Fire-and-forget; a per-device/per-feature failure is logged and does not affect any
    /// other device.
    public void VibrateAll(double intensityFraction)
    {
        if (!IsConnected) return;
        _ = VibrateAllAsync(Math.Clamp(intensityFraction, 0.0, 1.0));
    }

    private async Task VibrateAllAsync(double intensityFraction)
    {
        var command = new DeviceOutputCommand(OutputType.Vibrate, PercentOrSteps.FromPercent(intensityFraction), null);
        foreach (var device in client.Devices)
        {
            foreach (var feature in device.GetFeaturesWithOutput(OutputType.Vibrate))
            {
                try { await feature.RunOutputAsync(command, CancellationToken.None); }
                catch (Exception ex) { Plugin.Log.Warning(ex, $"IntifaceIpc: failed to vibrate device '{device.Name}'."); }
            }
        }
    }

    /// Stops every currently connected device via Buttplug's own StopAllDevices message - the protocol-
    /// level "stop everything" call, used for both the ordinary explicit stop command and panic.
    public void StopAll()
    {
        if (!IsConnected) return;
        _ = StopAllAsync();
    }

    private async Task StopAllAsync()
    {
        try { await client.StopAllDevicesAsync(CancellationToken.None); }
        catch (Exception ex) { Plugin.Log.Warning(ex, "IntifaceIpc: failed to stop all devices."); }
    }

    public void Dispose()
    {
        connectionCts?.Cancel();
        client.Dispose();
    }
}
