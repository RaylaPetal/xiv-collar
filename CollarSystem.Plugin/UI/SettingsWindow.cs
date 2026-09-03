using System;
using System.Collections.Generic;
using System.Numerics;
using CollarSystem.Plugin.Config;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace CollarSystem.Plugin.UI;

/// Role (Owner/Sub) and relay URL live only in the saved config with no other way to change them, so
/// this is the one place both get edited. Opened via the plugin installer's gear icon (OpenConfigUi) and
/// the `/collarsettings` command - deliberately reachable regardless of which role's main window you're
/// currently on, since switching roles is exactly the case where the role-gated window isn't the one to open.
/// Also holds pre-pairing Sub setup (gesture/wardrobe allowlists, ToS ack) - design.md's "Settings window
/// scope" decision, extended to wardrobe design selection alongside the existing gesture folder allowlist.
public class SettingsWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string relayUrlInput = "";
    private string newGestureAllowlistFolder = "";
    private string newWardrobeAllowlistFolder = "";
    private static readonly string[] RoleNames = ["Sub", "Owner"];

    public SettingsWindow(Plugin plugin) : base("Collar - Settings###CollarSettingsWindow")
    {
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(420, 420), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void OnOpen() => relayUrlInput = plugin.Configuration.RelayUrl;

    public override void Draw()
    {
        var config = plugin.Configuration;

        DrawConnectionCard(config);
        ImGui.Spacing();
        DrawFolderAllowlistCard("Wardrobe design allowlist", FontAwesomeIcon.Tshirt, config.WardrobeFolderAllowlist, ref newWardrobeAllowlistFolder);
        ImGui.Spacing();
        DrawFolderAllowlistCard("Gesture mod folder allowlist", FontAwesomeIcon.TheaterMasks, config.GestureFolderAllowlist, ref newGestureAllowlistFolder);
        ImGui.Spacing();
        DrawTosCard(config);
    }

    private void DrawConnectionCard(PluginConfig config)
    {
        // Fixed height: a (0,0) card size fills ALL remaining space in ImGui, not "auto-size to
        // content" - fine for the last card in a window, wrong for one with more cards after it.
        using var card = Card.Begin("connectionCard", new Vector2(0, 170));

        IconGlyph.Text(FontAwesomeIcon.PlugCircleBolt, "Connection");
        ImGui.Separator();

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
        ConnectionStatusView.Draw(plugin.Relay.ConnectionState);
        ImGui.SameLine();
        ImGui.TextColored(Theme.TextMuted, "|");
        ImGui.SameLine();
        ImGui.TextDisabled($"Paired: {(config.Pairing.IsPaired ? config.Pairing.PeerName : "no")}");
    }

    /// Shared by the wardrobe (Glamourer design folders) and gesture (Penumbra mod folders) allowlists -
    /// same "empty = finds nothing" semantics, same folder-prefix matching on the other side.
    private void DrawFolderAllowlistCard(string title, FontAwesomeIcon icon, List<string> allowlist, ref string newFolderInput)
    {
        using var card = Card.Begin($"allowlistCard_{title}", new Vector2(0, 160));

        IconGlyph.Text(icon, title);
        ImGui.Separator();
        ImGui.TextDisabled("Empty = scan finds nothing.");

        for (var i = 0; i < allowlist.Count; i++)
        {
            ImGui.PushID(i);
            ImGui.BulletText(allowlist[i]);
            ImGui.SameLine();
            if (ImGui.SmallButton("Remove"))
            {
                allowlist.RemoveAt(i);
                plugin.Configuration.Save();
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }

        ImGui.InputText($"##newFolder_{title}", ref newFolderInput, 128);
        ImGui.SameLine();
        if (ImGui.Button($"Add folder##{title}") && newFolderInput.Length > 0)
        {
            allowlist.Add(newFolderInput);
            plugin.Configuration.Save();
            newFolderInput = "";
        }
    }

    private void DrawTosCard(PluginConfig config)
    {
        using var card = Card.Begin("tosCard");

        IconGlyph.Text(FontAwesomeIcon.ExclamationTriangle, "Automation risk acknowledgement");
        ImGui.Separator();
        ImGui.TextWrapped("Required before the Gesture/Follow permission toggles can be enabled - see the README.");

        if (ImGuiCheckbox("I understand the automation-risk caveat (chat injection, input blocking)", config.TosAcknowledged, out var newTos))
        {
            config.TosAcknowledged = newTos;
            config.Save();
        }
    }

    private static bool ImGuiCheckbox(string label, bool value, out bool newValue)
    {
        newValue = value;
        return ImGui.Checkbox(label, ref newValue);
    }
}
