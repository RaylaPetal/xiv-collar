using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CollarSystem.Plugin.Config;
using CollarSystem.Plugin.Ipc;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace CollarSystem.Plugin.UI;

/// Role, pairing identity, trigger phrase, folder allowlists, and scanning live here - the "infrastructure"
/// side of setup, shared regardless of which alias you're about to define. What each alias actually maps
/// to (title text, which scanned design/gesture, follow words) lives in CollarWindow's own Title/Wardrobe/
/// Gesture tabs instead, next to the scan results and pending prompts it depends on. Everything here stays
/// visible regardless of Role, same as CollarWindow's tabs - you can scan/configure before ever flipping
/// to Sub. Opened via the plugin installer's gear icon (OpenConfigUi) and the `/collarsettings` command.
public class SettingsWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string peerCodeInput = "";
    private string triggerPhraseInput = "";
    private string safewordInput = "";
    private string gestureModSearch = "";
    private string newWardrobeAllowlistFolder = "";

    private static readonly string[] RoleNames = ["Sub", "Owner"];

    public SettingsWindow(Plugin plugin) : base("Collar - Settings###CollarSettingsWindow")
    {
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(460, 480), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void OnOpen()
    {
        var config = plugin.Configuration;
        peerCodeInput = config.Pairing.PeerCode ?? "";
        triggerPhraseInput = config.TriggerPhrase;
        safewordInput = config.PanicSafeword ?? "";
    }

    public override void Draw()
    {
        var config = plugin.Configuration;

        DrawIdentityCard(config);
        ImGui.Spacing();

        DrawSafetyCard(config);
        ImGui.Spacing();

        DrawWardrobeScanCard(config);
        ImGui.Spacing();
        DrawGestureScanCard(config);
        ImGui.Spacing();
        DrawMoodlesScanCard(config);
        ImGui.Spacing();
        DrawFollowAliasesCard(config);
        ImGui.Spacing();

        DrawTosCard(config);
    }

    private void DrawIdentityCard(PluginConfig config)
    {
        var pending = plugin.ChatCommandListener.Pending;
        var sameRoleWarning = pending is { } pendingCheck && pendingCheck.SenderRole == config.Role;
        var height = 260 + (pending is not null ? (sameRoleWarning ? 80 : 50) : 0) + (config.Pairing.IsPaired ? 35 : 0);
        using var card = Card.Begin("identityCard", new Vector2(0, height));

        IconGlyph.Text(FontAwesomeIcon.UserShield, "Identity & Pairing");
        ImGui.Separator();

        ImGui.TextWrapped("Role determines which side of the pairing you are - it doesn't hide anything in the main window, so you can set up aliases or use the Owner tab regardless.");
        var roleIndex = config.Role == PluginRole.Owner ? 1 : 0;
        if (ImGui.Combo("Role", ref roleIndex, RoleNames, RoleNames.Length))
        {
            config.Role = roleIndex == 1 ? PluginRole.Owner : PluginRole.Sub;
            config.Save();
        }
        IconGlyph.HelpMarker("Sub reacts to trigger tells and applies commands locally - only Sub actually gates anything. Owner is mostly informational (shown in the pairing handshake, and which pairing-release behavior applies). Both roles use the same code-handshake flow to pair.");

        ImGui.Spacing();
        ImGui.TextWrapped("Your code - share it with your pair out of band (voice, DM, etc), then have them enter it below.");
        ImGui.TextUnformatted(config.Pairing.MyCode);
        ImGui.SameLine();
        if (ImGui.SmallButton("Copy##myCode"))
            ImGui.SetClipboardText(config.Pairing.MyCode);
        ImGui.SameLine();
        if (ImGui.SmallButton("Regenerate##myCode"))
            plugin.PairingCommand.RegenerateMyCode();
        IconGlyph.HelpMarker("Generated once per install. Only used to gate the one-time pairing handshake message below - never checked against ongoing command tells. Regenerating invalidates any handshake attempt still using the old code.");

        ImGui.Spacing();
        ImGui.TextWrapped("Their code - the code they shared with you.");
        if (ImGui.InputText("Their code", ref peerCodeInput, 32))
            plugin.PairingCommand.SetPeerCode(peerCodeInput);
        IconGlyph.HelpMarker("Required before a pairing handshake tell from them can produce a Pending request below - a wrong or missing code is silently ignored.");

        ImGui.Spacing();
        ImGui.TextWrapped("Once both codes are entered on both sides, send this as a tell to them - it starts the handshake.");
        ImGui.TextUnformatted(plugin.ChatComposer.ComposePairing());
        ImGui.SameLine();
        if (ImGui.SmallButton("Copy##pairingMsg"))
            ImGui.SetClipboardText(plugin.ChatComposer.ComposePairing());
        IconGlyph.HelpMarker("Copies this handshake message to your clipboard only - it's never sent for you. Paste it after typing /tell TheirName@World yourself. Either side can send first.");

        if (ImGui.InputText("Trigger phrase", ref triggerPhraseInput, 32))
        {
            config.TriggerPhrase = triggerPhraseInput;
            config.Save();
        }
        IconGlyph.HelpMarker("The word that must start every ongoing command tell, e.g. \"command strip\". Only used after pairing is locked - the handshake message above always starts with \"collarpair\" regardless of this setting.");

        ImGui.Spacing();
        if (pending is { } request)
        {
            var roleLabel = request.SenderRole == PluginRole.Owner ? "your Owner" : "your Sub";
            IconGlyph.WrappedColored(Theme.Warning, $"Pairing request from {request.Name}@{request.World} (code matched) - they say they'll be {roleLabel}.");
            if (sameRoleWarning)
                IconGlyph.WrappedColored(Theme.Danger, $"You're both set to {config.Role} - one of you should switch Role above, or nothing will ever trigger.");

            if (ImGui.Button("Accept"))
                plugin.ChatCommandListener.AcceptPending();
            IconGlyph.HelpMarker("Trusts this sender as your paired peer from now on and locks pairing on.");
            ImGui.SameLine();
            if (ImGui.Button("Reject"))
                plugin.ChatCommandListener.DismissPending();
        }
        else if (config.Pairing.IsPaired)
        {
            IconGlyph.WrappedColored(Theme.Success, $"Paired with {config.Pairing.PeerName}@{config.Pairing.PeerWorld}.");
            if (config.Role == PluginRole.Owner)
            {
                if (ImGui.Button("Release pairing"))
                    plugin.PairingCommand.ReleasePeer();
                IconGlyph.HelpMarker("Clears who you're paired with on your own client only - doesn't touch your Sub's plugin at all. Fixes a stale/wrong pairing or frees them up to pair with someone else.");
            }
            else
            {
                ImGui.TextDisabled("Locked - only /collarpanic (your safeword, below) unpairs, not this screen.");
            }
        }
        else
        {
            IconGlyph.WrappedColored(Theme.TextMuted, "Not paired - no pending handshake.");
        }
    }

    /// There is deliberately no panic button anywhere in this plugin's UI - /collarpanic is a typed
    /// safeword instead, the same convention as any other safeword: memorized, not clicked, and not
    /// visible on screen for someone else to see or accidentally hit. Leaving this blank keeps
    /// /collarpanic working unconditionally with no argument, exactly like before - a forgotten/unset
    /// safeword must never be the reason panic stops working.
    private void DrawSafetyCard(PluginConfig config)
    {
        using var card = Card.Begin("safetyCard", new Vector2(0, 100));

        IconGlyph.Text(FontAwesomeIcon.ExclamationTriangle, "Safety");
        ImGui.Separator();
        ImGui.TextWrapped("/collarpanic always reverts everything (unpair, outfit, title, movement lock) from local state only.");

        if (ImGui.InputText("Safeword", ref safewordInput, 32))
        {
            config.PanicSafeword = safewordInput.Trim().Length > 0 ? safewordInput.Trim() : null;
            config.Save();
        }
        IconGlyph.HelpMarker("If set, /collarpanic requires this exact word as its argument (e.g. \"/collarpanic red\") - typed panic instead of a clickable button. Leave blank and plain /collarpanic keeps working with no argument needed.");
    }

    private void DrawWardrobeScanCard(PluginConfig config)
    {
        using var card = Card.Begin("wardrobeScanCard", new Vector2(0, 320));

        IconGlyph.Text(FontAwesomeIcon.Tshirt, "Wardrobe design allowlist & scan");
        ImGui.Separator();
        IconGlyph.WrappedDisabled("Empty allowlist = scan finds nothing. Outfit aliases (which design each one applies) live in the main window's Wardrobe tab.");
        IconGlyph.HelpMarker("Only designs inside these folders (matched as a Glamourer design-browser folder prefix) are ever scanned or available to pick as an outfit alias's target.");

        DrawAllowlistBody(config.WardrobeFolderAllowlist, ref newWardrobeAllowlistFolder, "wardrobe");

        ImGui.Spacing();
        if (ImGui.Button("Rescan wardrobe"))
            plugin.OutfitCommand.Rescan();
        IconGlyph.HelpMarker("Re-reads your saved Glamourer designs and keeps the ones inside the allowlisted folders above - run this after saving/renaming a design before it'll show up as an option in the Wardrobe tab.");

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
        IconGlyph.WrappedColored(color, $"Found {lastScanTotal} saved design(s), {matched} matched your allowlist folder(s).");

        if (matched == 0)
            return;

        if (ImGui.SmallButton("Copy names##wardrobe"))
            ImGui.SetClipboardText(string.Join("\n", wardrobe.LocalDesigns.Values.Select(d => d.Name)));
        IconGlyph.HelpMarker("Copies the list below as plain text, one design per line - paste it to your Owner (Discord, voice-to-text, etc) so they know exactly what names they can reference with a direct override (\"outfit lock <name>\").");

        using var _ = ImRaii.Child("wardrobeCatalog", new Vector2(0, 80), true);
        foreach (var entry in wardrobe.LocalDesigns.Values)
            ImGui.BulletText(entry.Name);
    }

    private void DrawGestureScanCard(PluginConfig config)
    {
        using var card = Card.Begin("gestureScanCard", new Vector2(0, 430));

        IconGlyph.Text(FontAwesomeIcon.TheaterMasks, "Animation mods to scan");
        ImGui.Separator();
        IconGlyph.WrappedDisabled("Select the exact Penumbra mods whose named animation options should be scanned. Disabled mods are allowed and will be enabled temporarily when played.");

        var folderFilter = config.GestureModFolderFilter;
        if (ImGui.InputTextWithHint("##gestureFolder", "Penumbra sort folder (optional)...", ref folderFilter, 128))
        {
            config.GestureModFolderFilter = folderFilter;
            config.Save();
        }
        ImGui.InputTextWithHint("##gestureModSearch", "Search mod names...", ref gestureModSearch, 128);
        var installed = plugin.GestureCommand.GetInstalledMods();
        using (ImRaii.Child("gestureModPicker", new Vector2(0, 180), true))
        {
            foreach (var mod in installed.Where(m =>
                         (string.IsNullOrWhiteSpace(config.GestureModFolderFilter) || (m.SortPath is { } path &&
                             (path.Equals(config.GestureModFolderFilter.Trim().TrimEnd('/'), StringComparison.OrdinalIgnoreCase) ||
                              path.StartsWith(config.GestureModFolderFilter.Trim().TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)))) &&
                         (string.IsNullOrWhiteSpace(gestureModSearch) || m.Name.Contains(gestureModSearch.Trim(), StringComparison.OrdinalIgnoreCase))))
            {
                var selected = config.SelectedGestureMods.Contains(mod.Directory);
                if (ImGui.Checkbox($"{mod.Name}##gestureMod_{mod.Directory}", ref selected))
                {
                    if (selected) config.SelectedGestureMods.Add(mod.Directory); else config.SelectedGestureMods.Remove(mod.Directory);
                    config.Save();
                }
                if (mod.SortPath != null) { ImGui.SameLine(); ImGui.TextDisabled(mod.SortPath); }
            }
        }

        ImGui.Spacing();
        if (ImGui.Button("Rescan gestures"))
            plugin.GestureCommand.Rescan();
        IconGlyph.HelpMarker("Reads each selected mod's Penumbra option names and animation redirects, including currently disabled mods.");

        DrawGestureScanFeedback();
    }

    private void DrawGestureScanFeedback()
    {
        var gestureMapping = plugin.Configuration.GestureMapping;
        var lastScanTotal = plugin.GestureCommand.LastScanTotalMods;

        if (lastScanTotal is null)
        {
            ImGui.TextDisabled("Not scanned yet this session.");
            return;
        }

        if (plugin.GestureCommand.LastScanError is { } error)
        {
            IconGlyph.WrappedColored(Theme.Danger, error);
            return;
        }
        var matched = gestureMapping.LocalCatalog.Count;
        var color = matched > 0 ? Theme.Success : Theme.Warning;
        IconGlyph.WrappedColored(color, $"Found {lastScanTotal} installed mod(s); {plugin.Configuration.SelectedGestureMods.Count} selected; {matched} animation trigger(s) discovered.");

        if (matched == 0 && lastScanTotal > 0)
        {
            ImGui.TextWrapped(
                "No animation options were found. Select at least one mod above, then rescan.");
            return;
        }

        if (matched == 0)
            return;

        if (ImGui.SmallButton("Copy names##gesture"))
        {
            ImGui.SetClipboardText(plugin.GestureCommand.ExportCatalog());
        }
        IconGlyph.HelpMarker("Copies versioned entries containing the mod, animation option, tied trigger, and selections for the Owner's Add from clipboard action.");

        using var _ = ImRaii.Child("gestureCatalog", new Vector2(0, 80), true);
        foreach (var entry in gestureMapping.LocalCatalog.Values)
        {
            ImGui.BulletText(entry.Label);
        }
    }

    /// collar/moodles: no folder allowlist, unlike Wardrobe/Gesture - Moodles presets have no folder-
    /// organization concept, every saved preset is eligible (design.md's decision).
    private void DrawMoodlesScanCard(PluginConfig config)
    {
        using var card = Card.Begin("moodlesScanCard", new Vector2(0, 200));

        IconGlyph.Text(FontAwesomeIcon.TheaterMasks, "Moodles preset scan");
        ImGui.Separator();
        IconGlyph.WrappedDisabled("Reads your own saved Moodles presets directly - nothing to allowlist. Moodles apply/clear commands live in the main window's Owner tab.");

        if (ImGui.Button("Rescan Moodles presets"))
            plugin.MoodlesCommand.Rescan();
        IconGlyph.HelpMarker("Re-reads your saved presets from your own Moodles plugin - run this after saving a new preset before it'll show up for your Owner to reference.");

        DrawMoodlesScanFeedback();
    }

    private void DrawMoodlesScanFeedback()
    {
        var moodlesMapping = plugin.Configuration.MoodlesMapping;
        var lastScanTotal = plugin.MoodlesCommand.LastScanTotalPresets;

        if (plugin.MoodlesCommand.LastScanStatus is MoodlesScanStatus.Unavailable or MoodlesScanStatus.Failed)
        {
            IconGlyph.WrappedColored(Theme.Danger, plugin.MoodlesCommand.LastScanError ?? "Moodles preset scan failed.");
            if (moodlesMapping.LocalCatalog.Count > 0)
                ImGui.TextDisabled($"Keeping {moodlesMapping.LocalCatalog.Count} preset(s) from the last successful scan.");
            return;
        }

        if (lastScanTotal is null)
        {
            ImGui.TextDisabled("Not scanned yet this session.");
            return;
        }

        IconGlyph.WrappedColored(Theme.Success, $"Scan succeeded: found {lastScanTotal} saved preset(s).");

        if (lastScanTotal == 0)
            return;

        if (ImGui.SmallButton("Copy names##moodles"))
            ImGui.SetClipboardText(string.Join("\n", moodlesMapping.LocalCatalog.Values.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x)));
        IconGlyph.HelpMarker("Copies the list below as plain text, one preset per line - paste it to your Owner (Discord, voice-to-text, etc) so they know exactly what names they can reference with \"moodle apply <name>\".");

        using var _ = ImRaii.Child("moodlesCatalog", new Vector2(0, 80), true);
        foreach (var entry in moodlesMapping.LocalCatalog.Values)
            ImGui.BulletText(entry.Name);
    }

    /// Shared by the wardrobe (Glamourer design folders) and gesture (Penumbra mod folders) allowlists -
    /// same "empty = finds nothing" semantics, same folder-prefix matching on the other side.
    private void DrawAllowlistBody(List<string> allowlist, ref string newFolderInput, string idSuffix)
    {
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

        ImGui.InputText($"##newFolder_{idSuffix}", ref newFolderInput, 128);
        ImGui.SameLine();
        if (ImGui.Button($"Add folder##{idSuffix}") && newFolderInput.Length > 0)
        {
            allowlist.Add(newFolderInput);
            plugin.Configuration.Save();
            newFolderInput = "";
        }
    }

    private void DrawFollowAliasesCard(PluginConfig config)
    {
        using var card = Card.Begin("followAliasesCard", new Vector2(0, 110));
        IconGlyph.Text(FontAwesomeIcon.Link, "Follow / Leash Triggers");
        ImGui.Separator();

        var follow = config.Aliases.Follow;
        var engage = follow.EngageAlias;
        var release = follow.ReleaseAlias;
        if (ImGui.InputText("Engage alias", ref engage, 32))
        {
            follow.EngageAlias = engage;
            config.Save();
        }
        IconGlyph.HelpMarker("Locks your movement to follow the Owner and blocks your own WASD input until released - the hardcore permission, gated separately below by its own toggle and the ToS acknowledgement.");
        if (ImGui.InputText("Release alias", ref release, 32))
        {
            follow.ReleaseAlias = release;
            config.Save();
        }
        IconGlyph.HelpMarker("Releases the movement lock and returns normal input control.");
    }

    private void DrawTosCard(PluginConfig config)
    {
        using var card = Card.Begin("tosCard");

        IconGlyph.Text(FontAwesomeIcon.ExclamationTriangle, "Automation risk acknowledgement");
        ImGui.Separator();
        ImGui.TextWrapped("Required before the Gesture/Follow permission toggles can be enabled - see the README.");

        if (ImGuiCheckbox("I understand the automation-risk caveat (input blocking; sending is always your own click)", config.TosAcknowledged, out var newTos))
        {
            config.TosAcknowledged = newTos;
            config.Save();
        }
        IconGlyph.HelpMarker("Required once before the Gesture and Follow permission toggles (in the Sub window's Permissions tab) can be enabled at all - Title and Outfit don't need it.");
    }

    private static bool ImGuiCheckbox(string label, bool value, out bool newValue)
    {
        newValue = value;
        return ImGui.Checkbox(label, ref newValue);
    }
}
