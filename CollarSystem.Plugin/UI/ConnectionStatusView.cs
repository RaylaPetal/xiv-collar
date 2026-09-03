using CollarSystem.Plugin.Relay;
using Dalamud.Bindings.ImGui;

namespace CollarSystem.Plugin.UI;

/// Shared connection-status strip for DomWindow/SubWindow (design.md's "Connection-status color distinct
/// from the panic-red" decision - deliberately never uses the same saturated red as the panic button).
public static class ConnectionStatusView
{
    public static void Draw(ConnectionState state)
    {
        var (color, label) = state switch
        {
            ConnectionState.Connected => (Theme.Success, "Connected"),
            ConnectionState.Connecting => (Theme.Warning, "Connecting..."),
            ConnectionState.Reconnecting => (Theme.Warning, "Reconnecting..."),
            _ => (Theme.Danger, "Disconnected"),
        };

        ImGui.TextColored(color, "●");
        ImGui.SameLine();
        ImGui.TextUnformatted(label);
    }
}
