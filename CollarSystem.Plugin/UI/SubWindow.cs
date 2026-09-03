using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace CollarSystem.Plugin.UI;

/// Sub's incoming-request / permission-toggle / panic-button window. The panic button is drawn
/// unconditionally on every frame this window is open - collar/pairing requires it always available -
/// but sits below the header/pairing card rather than at the very top, so it isn't the first thing
/// competing for attention before the Sub even knows who they'd be unpairing from.
public class SubWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string activeModule = "wardrobe";

    private static readonly (string Id, FontAwesomeIcon Icon, string Tooltip)[] NavItems =
    [
        ("wardrobe", FontAwesomeIcon.Tshirt, "Wardrobe"),
        ("gesture", FontAwesomeIcon.TheaterMasks, "Gesture"),
        ("permissions", FontAwesomeIcon.ShieldAlt, "Permissions"),
    ];

    public SubWindow(Plugin plugin) : base("Collar - Sub###CollarSubWindow")
    {
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(460, 500), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
        this.plugin = plugin;

        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Cog,
            Click = _ => plugin.ToggleSettingsUi(),
            ShowTooltip = () => ImGui.SetTooltip("Settings"),
        });
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawStatusBar();
        ImGui.Spacing();

        DrawPairingCard();
        ImGui.Spacing();

        DrawPanicButton();
        ImGui.Spacing();

        if (NavBar.Draw(activeModule, NavItems) is { } clicked)
            activeModule = clicked;

        ImGui.Spacing();
        using var card = Card.Begin("moduleCard");
        switch (activeModule)
        {
            case "wardrobe":
                DrawWardrobeModule();
                break;
            case "gesture":
                DrawGestureModule();
                break;
            case "permissions":
                DrawPermissionsModule();
                break;
        }
    }

    private void DrawPanicButton()
    {
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 10f);
        using var thickness = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 2f);
        using var border = ImRaii.PushColor(ImGuiCol.Border, Theme.PanicRedHover);
        using var bg = ImRaii.PushColor(ImGuiCol.Button, Theme.PanicRed);
        using var bgHover = ImRaii.PushColor(ImGuiCol.ButtonHovered, Theme.PanicRedHover);
        if (IconGlyph.SideIconButton(FontAwesomeIcon.ExclamationTriangle, "PANIC - unpair & revert everything", new Vector2(-1, 46)))
            plugin.PanicHandler.Panic();
    }

    private void DrawStatusBar()
    {
        using var card = Card.Begin("statusBar", new Vector2(0, 36), noScroll: true);
        ConnectionStatusView.Draw(plugin.Relay.ConnectionState);
        ImGui.SameLine();
        ImGui.TextColored(Theme.TextMuted, "|");
        ImGui.SameLine();
        ImGui.TextUnformatted("Role: Sub");

        ImGui.SameLine();
        ImGui.TextColored(Theme.TextMuted, "|");
        ImGui.SameLine();
        var pairing = plugin.Configuration.Pairing;
        if (pairing.IsPaired)
            ImGui.TextColored(Theme.Success, $"Owned by: {pairing.PeerName}");
        else
            ImGui.TextColored(Theme.TextMuted, "Not owned yet");
    }

    private void DrawPairingCard()
    {
        // A size of (0,0) tells ImGui's child region to fill ALL remaining space, not "auto-size to
        // content" - fine for the last card in a window, wrong here since Permissions/NavBar/module
        // content still need to render below it. Every non-last card in this window needs a fixed height.
        using var card = Card.Begin("pairingCard", new Vector2(0, 90));
        DrawIncomingRequest();
        DrawPairingStatus();
    }

    private void DrawIncomingRequest()
    {
        if (plugin.IncomingPairingRequestFrom is not { } peerName)
            return;

        ImGui.TextColored(Theme.Warning, $"Pairing request from \"{peerName}\"");
        if (ImGui.Button("Accept"))
        {
            Plugin.FireAndForget(plugin.PairingCommand.ExplicitAcceptAsync(peerName));
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
            ImGui.TextColored(Theme.Success, $"Paired with {pairing.PeerName}");
            return;
        }

        if (pairing.PairingId is { } pendingCode)
        {
            ImGui.TextUnformatted($"Your pairing code: {pendingCode}");
            ImGui.SameLine();
            if (ImGui.SmallButton("Copy"))
                ImGui.SetClipboardText(pendingCode);
            ImGui.TextDisabled("Share this with your Owner out of band, then wait for their request.");
            return;
        }

        if (ImGui.Button("Generate pairing code"))
            Plugin.FireAndForget(plugin.PairingCommand.GeneratePairingCodeAsync());
    }

    private void DrawPermissionsModule()
    {
        IconGlyph.Text(FontAwesomeIcon.ShieldAlt, "Permissions");
        ImGui.Separator();
        ImGui.Spacing();

        var permissions = plugin.Configuration.Permissions;
        if (ImGuiCheckbox("Title", permissions.Title, out var newTitle))
            SavePermission(() => permissions.Title = newTitle);
        if (ImGuiCheckbox("Outfit / Wardrobe", permissions.Outfit, out var newOutfit))
            SavePermission(() => permissions.Outfit = newOutfit);

        ImGui.Spacing();
        var config = plugin.Configuration;
        if (!config.TosAcknowledged)
            ImGui.TextColored(Theme.Warning, "Gesture/Follow require the ToS acknowledgement in Settings (gear icon) first.");

        using (ImRaii.Disabled(!config.TosAcknowledged))
        {
            if (ImGuiCheckbox("Gesture", permissions.Gesture, out var newGesture))
                SavePermission(() => permissions.Gesture = newGesture);
            if (ImGuiCheckbox("Follow / Leash (hardcore)", permissions.Follow, out var newFollow))
                SavePermission(() => permissions.Follow = newFollow);
        }
    }

    private void DrawWardrobeModule()
    {
        IconGlyph.Text(FontAwesomeIcon.Tshirt, "Wardrobe");
        ImGui.Separator();
        ImGui.TextDisabled("Design folder allowlist lives in Settings (gear icon) now.");

        if (ImGui.Button("Rescan & share wardrobe"))
            Plugin.FireAndForget(plugin.OutfitCommand.RescanAndPushDesignsAsync());

        DrawWardrobeScanFeedback();
    }

    private void DrawWardrobeScanFeedback()
    {
        var wardrobe = plugin.Configuration.WardrobeMapping;
        var lastScanTotal = plugin.OutfitCommand.LastScanTotalDesigns;

        if (lastScanTotal is null)
        {
            ImGui.TextDisabled("Not scanned yet this session.");
            return;
        }

        var matched = wardrobe.LocalDesigns.Count;
        var color = matched > 0 ? Theme.Success : Theme.Warning;
        ImGui.TextColored(color, $"Found {lastScanTotal} saved design(s), {matched} matched your allowlist folder(s).");

        if (!plugin.Configuration.Pairing.IsPaired)
            ImGui.TextDisabled("Not paired - scan saved locally, not shared with an Owner yet.");
        else if (!plugin.Configuration.Permissions.Outfit)
            ImGui.TextDisabled("Outfit permission is off - scan saved locally, not shared with the Owner.");

        if (matched == 0)
            return;

        using var _ = ImRaii.Child("wardrobeCatalog", new Vector2(0, 100), true);
        foreach (var entry in wardrobe.LocalDesigns.Values)
            ImGui.BulletText(entry.Name);
    }

    private void DrawGestureModule()
    {
        IconGlyph.Text(FontAwesomeIcon.TheaterMasks, "Gesture");
        ImGui.Separator();
        ImGui.TextDisabled("Mod folder allowlist lives in Settings (gear icon) now.");

        if (ImGui.Button("Rescan & share catalog"))
            Plugin.FireAndForget(plugin.GestureCommand.RescanAndPushAsync());

        DrawScanFeedback();

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

    /// The scan result was previously only visible on the Owner's side after a successful push - a Sub
    /// checking their own window (or one who isn't paired/permitted yet) saw nothing at all, with no way
    /// to tell "found nothing" apart from "didn't run" or "allowlist folder name doesn't match Penumbra's".
    private void DrawScanFeedback()
    {
        var gestureMapping = plugin.Configuration.GestureMapping;
        var lastScanTotal = plugin.GestureCommand.LastScanTotalMods;

        if (lastScanTotal is null)
        {
            ImGui.TextDisabled("Not scanned yet this session.");
            return;
        }

        var matched = gestureMapping.LocalCatalog.Count;
        var color = matched > 0 ? Theme.Success : Theme.Warning;
        ImGui.TextColored(color, $"Found {lastScanTotal} installed mod(s), {matched} matched your allowlist folder(s).");

        if (matched == 0 && lastScanTotal > 0)
        {
            ImGui.TextWrapped(
                "Zero matches usually means either the allowlist is empty, or a folder name in Settings " +
                "doesn't exactly match the sort folder shown in Penumbra's own mod list.");
        }

        if (!plugin.Configuration.Pairing.IsPaired)
            ImGui.TextDisabled("Not paired - scan saved locally, not shared with an Owner yet.");
        else if (!plugin.Configuration.Permissions.Gesture)
            ImGui.TextDisabled("Gesture permission is off - scan saved locally, not shared with the Owner.");

        if (matched == 0)
            return;

        using var _ = ImRaii.Child("gestureCatalog", new Vector2(0, 100), true);
        foreach (var entry in gestureMapping.LocalCatalog.Values)
        {
            var summary = entry.EmoteNames.Count > 0 ? string.Join(", ", entry.EmoteNames) : "unresolved - no matching emote";
            ImGui.BulletText($"{entry.ModName}: {summary}");
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
