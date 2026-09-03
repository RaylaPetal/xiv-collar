using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace CollarSystem.Plugin.UI;

/// Sub's incoming-request / permission-toggle / panic-button window. The panic button is drawn first,
/// unconditionally, every frame this window is open - collar/pairing requires it always available.
public class SubWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string newAllowlistFolder = "";
    private string subDisplayName = "Sub";

    public SubWindow(Plugin plugin) : base("Collar - Sub###CollarSubWindow")
    {
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(420, 400), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawPanicButton();
        if (ImGui.Button("Settings (role, relay URL)"))
            plugin.ToggleSettingsUi();
        ImGui.Separator();

        DrawIncomingRequest();
        DrawPairingStatus();
        ImGui.Spacing();
        DrawPermissions();
        ImGui.Spacing();
        DrawGesturePanel();
    }

    private void DrawPanicButton()
    {
        using var color = Dalamud.Interface.Utility.Raii.ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.7f, 0.1f, 0.1f, 1f));
        if (ImGui.Button("PANIC - unpair & revert everything", new Vector2(-1, 40)))
            plugin.PanicHandler.Panic();
    }

    private void DrawIncomingRequest()
    {
        if (plugin.IncomingPairingRequestFrom is not { } peerName)
            return;

        ImGui.TextColored(new Vector4(1f, 0.85f, 0.2f, 1f), $"Pairing request from \"{peerName}\"");
        ImGui.InputText("Your name", ref subDisplayName, 64);
        if (ImGui.Button("Accept"))
        {
            Plugin.FireAndForget(plugin.PairingCommand.ExplicitAcceptAsync(peerName, subDisplayName));
            plugin.IncomingPairingRequestFrom = null;
        }
        ImGui.SameLine();
        if (ImGui.Button("Decline"))
        {
            Plugin.FireAndForget(plugin.PairingCommand.DeclineAsync());
            plugin.IncomingPairingRequestFrom = null;
        }
    }

    private void DrawPairingStatus()
    {
        var pairing = plugin.Configuration.Pairing;
        if (pairing.IsPaired)
        {
            ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), $"Paired with {pairing.PeerName}");
            return;
        }

        if (pairing.PairingId is { } pendingCode)
        {
            ImGui.TextUnformatted($"Your pairing code: {pendingCode}");
            ImGui.TextDisabled("Share this with your Owner out of band, then wait for their request.");
            return;
        }

        if (ImGui.Button("Generate pairing code"))
            Plugin.FireAndForget(plugin.PairingCommand.GeneratePairingCodeAsync());
    }

    private void DrawPermissions()
    {
        var permissions = plugin.Configuration.Permissions;
        ImGui.TextUnformatted("Permissions");
        ImGui.Separator();

        if (ImGuiCheckbox("Title", permissions.Title, out var newTitle))
            SavePermission(() => permissions.Title = newTitle);
        if (ImGuiCheckbox("Outfit", permissions.Outfit, out var newOutfit))
            SavePermission(() => permissions.Outfit = newOutfit);

        ImGui.Spacing();
        var config = plugin.Configuration;
        if (ImGuiCheckbox("I understand the automation-risk caveat (chat injection, input blocking) in the README", config.TosAcknowledged, out var newTos))
            SavePermission(() => config.TosAcknowledged = newTos);

        using (Dalamud.Interface.Utility.Raii.ImRaii.Disabled(!config.TosAcknowledged))
        {
            if (ImGuiCheckbox("Gesture", permissions.Gesture, out var newGesture))
                SavePermission(() => permissions.Gesture = newGesture);
            if (ImGuiCheckbox("Follow / Leash (hardcore)", permissions.Follow, out var newFollow))
                SavePermission(() => permissions.Follow = newFollow);
        }
    }

    private void DrawGesturePanel()
    {
        if (!ImGui.CollapsingHeader("Gesture"))
            return;

        ImGui.TextUnformatted("Allowed mod folders (empty = scan finds nothing)");
        var allowlist = plugin.Configuration.GestureFolderAllowlist;
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

        ImGui.InputText("##newFolder", ref newAllowlistFolder, 128);
        ImGui.SameLine();
        if (ImGui.Button("Add folder") && newAllowlistFolder.Length > 0)
        {
            allowlist.Add(newAllowlistFolder);
            plugin.Configuration.Save();
            newAllowlistFolder = "";
        }

        if (ImGui.Button("Rescan & share catalog"))
            Plugin.FireAndForget(plugin.GestureCommand.RescanAndPushAsync());

        ImGui.Spacing();
        ImGui.TextUnformatted("Pending gesture prompts");
        foreach (var prompt in plugin.GestureCommand.PendingPrompts.ToArray())
        {
            ImGui.PushID(prompt.CommandId);
            ImGui.TextUnformatted($"{prompt.EmoteName} ({prompt.ModName})");
            ImGui.SameLine();
            if (ImGui.Button("Confirm"))
                plugin.GestureCommand.ConfirmAndTrigger(prompt.CommandId);
            ImGui.SameLine();
            if (ImGui.Button("Dismiss"))
                plugin.GestureCommand.DismissPrompt(prompt.CommandId);
            ImGui.PopID();
        }
    }

    private void SavePermission(Action apply)
    {
        apply();
        plugin.Configuration.Save();
    }

    private static bool ImGuiCheckbox(string label, bool value, out bool newValue)
    {
        newValue = value;
        var changed = ImGui.Checkbox(label, ref newValue);
        return changed;
    }
}
