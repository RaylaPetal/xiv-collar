using System;
using System.Numerics;
using CollarSystem.Plugin.Config;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace CollarSystem.Plugin.UI;

/// Role (Owner/Sub) and relay URL live only in the saved config with no other way to change them, so
/// this is the one place both get edited. Opened via the plugin installer's gear icon (OpenConfigUi) and
/// the `/collarsettings` command - deliberately reachable regardless of which role's main window you're
/// currently on, since switching roles is exactly the case where the role-gated window isn't the one to open.
public class SettingsWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string relayUrlInput = "";
    private static readonly string[] RoleNames = ["Sub", "Owner"];

    public SettingsWindow(Plugin plugin) : base("Collar - Settings###CollarSettingsWindow")
    {
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(360, 160), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void OnOpen() => relayUrlInput = plugin.Configuration.RelayUrl;

    public override void Draw()
    {
        var config = plugin.Configuration;

        ImGui.TextWrapped("Role determines which window `/collar` opens, and which side of the pairing you are.");
        var roleIndex = config.Role == PluginRole.Owner ? 1 : 0;
        if (ImGui.Combo("Role", ref roleIndex, RoleNames, RoleNames.Length))
        {
            config.Role = roleIndex == 1 ? PluginRole.Owner : PluginRole.Sub;
            config.Save();
        }

        ImGui.Spacing();
        ImGui.TextWrapped("Relay address (websocket URL of your CollarSystem.Relay instance).");
        if (ImGui.InputText("Relay URL", ref relayUrlInput, 256))
        {
            config.RelayUrl = relayUrlInput;
            config.Save();
        }

        ImGui.Spacing();
        ImGui.TextDisabled($"Currently paired: {(config.Pairing.IsPaired ? config.Pairing.PeerName : "no")}");
    }
}
