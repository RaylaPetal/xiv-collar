using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CollarSystem.Plugin.Commands;
using CollarSystem.Plugin.Config;
using CollarSystem.Plugin.Ipc;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace CollarSystem.Plugin.UI;

/// Role, pairing identity, trigger phrase, scan scopes, and scanning live here - the "infrastructure"
/// side of setup, shared regardless of which alias you're about to define. What each alias actually maps
/// to (title text, which scanned design/gesture, follow words) lives in CollarWindow's own Title/Wardrobe/
/// Gesture/Collar tabs instead. Safeword configuration stays in the always-visible character header.
/// Everything here stays
/// visible regardless of Role, same as CollarWindow's tabs - you can scan/configure before ever flipping
/// to Sub. Opened via the plugin installer's gear icon (OpenConfigUi) and the `/collarsettings` command.
public class SettingsWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string peerCodeInput = "";
    private string triggerPhraseInput = "";
    private string gestureModSearch = "";
    private string newWardrobeAllowlistFolder = "";
    private string? scanAndExportResult;
    private string testCommandInput = "";

    /// Transient, session-only per-action local Test feedback (collar/ui-organization) - see
    /// CollarWindow's matching field for the rest of the Test controls; Moodles' only lives here since it
    /// has no Sub-facing module of its own. Each entry auto-clears a short time after being shown (see
    /// DrawTestButton).
    private readonly Dictionary<string, (LocalTestResult Result, long ShownAtTicks)> testResults = new();

    /// How long a local Test control's result stays visible before auto-clearing (collar/ui-organization).
    private const long TestResultDisplayMs = 4_000;

    private static readonly string[] RoleNames = ["Sub", "Owner"];

    public SettingsWindow(Plugin plugin) : base("Collar - Settings###CollarSettingsWindow")
    {
        // Raised from the original 480: Scan & Export (collar/catalog-sync) now stacks four scan sections
        // in the window's own scroll region instead of its own nested one - a taller default minimum means
        // less scrolling to reach it and everything below (ToS card) in the common case.
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(460, 700), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void OnOpen()
    {
        var config = plugin.Configuration;
        peerCodeInput = config.Pairing.PeerCode ?? "";
        triggerPhraseInput = config.TriggerPhrase;
    }

    /// Split into tabs (previously one long vertically-stacked flow) once Scan & Export's own growth made
    /// the whole window too tall to comfortably navigate in one scroll region - each tab now scrolls
    /// independently within the window's remaining space. Grouped by what they're for, not just by prior
    /// visual order: Identity & Pairing is setup you do once; Scanning is catalog upkeep you repeat as your
    /// designs/mods/statuses change; ToS bundles every risk acknowledgement together with the one local
    /// testing tool, since testing a command is usually the next thing you do right after enabling a
    /// permission those acknowledgements gate.
    public override void Draw()
    {
        var config = plugin.Configuration;

        if (!ImGui.BeginTabBar("settingsTabs"))
            return;

        if (ImGui.BeginTabItem("Identity & Pairing"))
        {
            DrawIdentityCard(config);
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Scanning"))
        {
            DrawScanAndExportCard(config);
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("ToS"))
        {
            DrawTosCard(config);
            ImGui.Spacing();
            DrawCustomChatCard(config);
            ImGui.Spacing();
            DrawTestCommandCard(config);
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    /// collar/chat-transport "An Owner-style command can be tested entirely locally": type the exact raw
    /// text an Owner would send (trigger phrase included) and run it through the real dispatch path
    /// (ChatCommandListener.TestIncomingCommand) - no pairing, no peer, nothing sent or received. A
    /// different, complementary tool to the per-action Test buttons elsewhere: those bypass command-text
    /// parsing entirely (calling the underlying action directly), this exercises the parsing itself - the
    /// trigger-phrase/permission/dispatch layer where every bug this card exists to catch was found.
    private void DrawTestCommandCard(PluginConfig config)
    {
        IconGlyph.Text(FontAwesomeIcon.FlaskVial, "Test an Owner command");
        ImGui.Separator();
        IconGlyph.WrappedDisabled("Type the exact text an Owner would send after \"/tell you\" - trigger phrase included - and run it locally. No pairing or peer needed, and nothing is sent or received.");

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        ImGui.InputTextWithHint("##testCommandInput", "e.g. ray outfit lock kagome", ref testCommandInput, 128);
        DrawTestButton("testOwnerCommand", "Run test", () => plugin.ChatCommandListener.TestIncomingCommand(testCommandInput));
    }

    /// collar/pairing "Sub's pairing identity configuration locks while paired": Role, both codes, and the
    /// trigger phrase all become read-only for a paired Sub - enforced here in the rendering layer only
    /// (ImRaii.Disabled), never in PluginConfig/PairingCommand, the same "UI-only lock" shape pairing's own
    /// "Sub can't unpair except via panic" has always used (see PairingCommand.ReleasePeer's comment). The
    /// Owner side is never locked, paired or not.
    private void DrawIdentityCard(PluginConfig config)
    {
        var pending = plugin.ChatCommandListener.Pending;
        var sameRoleWarning = pending is { } pendingCheck && pendingCheck.SenderRole == config.Role;
        var subLocked = config.Role == PluginRole.Sub && config.Pairing.IsPaired;

        IconGlyph.Text(FontAwesomeIcon.UserShield, "Identity & Pairing");
        ImGui.Separator();

        ImGui.TextWrapped("Role determines which side of the pairing you are - it doesn't hide anything in the main window, so you can set up aliases or use the Owner tab regardless.");
        using (ImRaii.Disabled(subLocked))
        {
            var roleIndex = config.Role == PluginRole.Owner ? 1 : 0;
            if (ImGui.Combo("Role", ref roleIndex, RoleNames, RoleNames.Length))
            {
                config.Role = roleIndex == 1 ? PluginRole.Owner : PluginRole.Sub;
                config.Save();
            }
        }
        IconGlyph.HelpMarker("Sub reacts to trigger tells and applies commands locally - only Sub actually gates anything. Owner is mostly informational (shown in the pairing handshake, and which pairing-release behavior applies). Either role can send the handshake first - whoever does, the other side just needs to Accept.");

        ImGui.Spacing();
        ImGui.TextWrapped("Your code - share it with your pair out of band (voice, DM, etc), then have them enter it below.");
        ImGui.TextUnformatted(config.Pairing.MyCode);
        ImGui.SameLine();
        if (ImGui.SmallButton("Copy##myCode"))
            ImGui.SetClipboardText(config.Pairing.MyCode);
        ImGui.SameLine();
        using (ImRaii.Disabled(subLocked))
        {
            if (ImGui.SmallButton("Regenerate##myCode"))
                plugin.PairingCommand.RegenerateMyCode();
        }
        IconGlyph.HelpMarker("Generated once per install. Only used to gate the one-time pairing handshake message below - never checked against ongoing command tells. Regenerating invalidates any handshake attempt still using the old code.");

        ImGui.Spacing();
        ImGui.TextWrapped("Their code - the code they shared with you.");
        using (ImRaii.Disabled(subLocked))
        {
            if (ImGui.InputText("Their code", ref peerCodeInput, 32))
                plugin.PairingCommand.SetPeerCode(peerCodeInput);
        }
        IconGlyph.HelpMarker("Required before a pairing handshake tell from them can produce a Pending request below - a wrong or missing code is silently ignored.");

        ImGui.Spacing();
        ImGui.TextWrapped("Once both codes are entered, either of you sends this as a tell - only one of you needs to. They accept it, and you're both paired automatically.");
        ImGui.TextUnformatted(plugin.ChatComposer.ComposePairing());
        ImGui.SameLine();
        if (ImGui.SmallButton("Copy##pairingMsg"))
            ImGui.SetClipboardText(plugin.ChatComposer.ComposePairing());
        IconGlyph.HelpMarker("Copies this handshake message to your clipboard only - it's never sent for you. Paste it after typing /tell TheirName@World yourself. Whoever sends it becomes paired automatically once the other side clicks Accept - no need for both of you to send one.");

        using (ImRaii.Disabled(subLocked))
        {
            if (ImGui.InputText("Trigger phrase", ref triggerPhraseInput, 32))
            {
                config.TriggerPhrase = triggerPhraseInput;
                config.Save();
            }
        }
        IconGlyph.HelpMarker("The word that must start every ongoing command tell, e.g. \"command strip\". Only used after pairing is locked - the handshake message above always starts with \"collarpair\" regardless of this setting.");

        if (subLocked)
            IconGlyph.WrappedColored(Theme.TextMuted, "Locked while paired - trigger /collarpanic to release pairing and change these again.");

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
            if (config.Pairing.PeerTriggerPhrase is { Length: > 0 } peerPhrase)
                IconGlyph.WrappedDisabled($"Trigger phrase in effect: \"{peerPhrase}\" (from your paired peer).");
            else
                IconGlyph.WrappedDisabled($"Trigger phrase in effect: \"{config.TriggerPhrase}\" (your own - peer hasn't sent theirs).");
            if (config.Role == PluginRole.Owner)
            {
                if (ImGui.Button("Release pairing"))
                    plugin.PairingCommand.ReleasePeer();
                IconGlyph.HelpMarker("Clears who you're paired with on your own client only - doesn't touch your Sub's plugin at all. Fixes a stale/wrong pairing or frees them up to pair with someone else.");
            }
            else
            {
                IconGlyph.WrappedDisabled("Locked - only /collarpanic (your safeword, below) unpairs, not this screen.");
            }
        }
        else
        {
            IconGlyph.WrappedColored(Theme.TextMuted, "Not paired - no pending handshake.");
        }
    }

    /// collar/catalog-sync: one section replacing the former separate Wardrobe/Gesture/Moodles scan cards
    /// - each category's own scope controls and feedback are unchanged, just grouped here with a "Scan
    /// all" action and the unified file export on top. Restraints scans independently of Wardrobe, with
    /// its own folder allowlist - bondage/restriction-themed designs and everyday outfits live in
    /// different Glamourer folders in practice, so they need different filters.
    ///
    /// Deliberately NOT wrapped in a Card (unlike every other section here) - a Card is a fixed-height
    /// BeginChild, and this section's content has grown every time a category was added to it (most
    /// recently Restraints) and will again. A hand-guessed fixed height drifts out of sync with actual
    /// content and creates a second, nested scroll region on top of the Settings window's own - the
    /// "can't reach the bottom" bug this replaces. Rendering directly into the window's own flow means
    /// there's exactly one scrollbar (the window's), which already grows/shrinks correctly with content.
    private void DrawScanAndExportCard(PluginConfig config)
    {
        IconGlyph.Text(FontAwesomeIcon.Search, "Scan & Export");
        ImGui.Separator();
        IconGlyph.WrappedDisabled("Scan every catalog at once below, then export one file to send your Owner - they fill every quick-command list from it in one action via the Owner tab's \"Import commands\" button.");

        if (ImGui.Button("Scan all"))
        {
            plugin.OutfitCommand.Rescan();
            plugin.GestureCommand.Rescan();
            plugin.MoodlesCommand.Rescan();
            scanAndExportResult = null;
        }
        IconGlyph.HelpMarker("Rescans Wardrobe, Gesture, and Moodles together, each using its own currently-configured scope below - the same result as triggering each one's own Rescan individually. Restraints has no scan step - capture devices individually in the Restraints tab.");

        ImGui.SameLine();
        var hasAnythingToExport = plugin.OutfitCommand.LastScanTotalDesigns is not null || plugin.GestureCommand.LastScanTotalMods is not null ||
            plugin.MoodlesCommand.LastScanTotalStatuses is not null || config.RestraintMapping.Devices.Count > 0;
        using (ImRaii.Disabled(!hasAnythingToExport))
        {
            if (ImGui.Button("Export..."))
            {
                plugin.FileDialogManager.SaveFileDialog("Export Collar catalog", ".txt", "collar-export", ".txt", (ok, path) =>
                {
                    if (!ok)
                        return;
                    try
                    {
                        System.IO.File.WriteAllText(path, plugin.CatalogSyncService.BuildExport());
                        scanAndExportResult = $"Exported to {path} - send this file to your Owner.";
                    }
                    catch (Exception ex)
                    {
                        scanAndExportResult = $"Export failed: {ex.Message}";
                    }
                });
            }
        }
        if (!hasAnythingToExport)
            IconGlyph.HelpMarker("Scan at least one category, or tag a Restraints device, before exporting.");
        if (scanAndExportResult is not null)
            IconGlyph.WrappedColored(scanAndExportResult.StartsWith("Export failed", StringComparison.Ordinal) ? Theme.Danger : Theme.Success, scanAndExportResult);

        ImGui.Spacing();
        ImGui.Separator();
        DrawWardrobeScanBody(config);
        ImGui.Spacing();
        DrawGestureScanBody(config);
        ImGui.Spacing();
        DrawMoodlesScanBody(config);
        ImGui.Spacing();
        ImGui.Separator();
    }

    private void DrawWardrobeScanBody(PluginConfig config)
    {
        IconGlyph.Text(FontAwesomeIcon.Tshirt, "Wardrobe design allowlist & scan");
        ImGui.Separator();
        IconGlyph.WrappedDisabled("No folders = all saved designs. Add folders only when you want to restrict the catalog. Outfit aliases live in the main window's Wardrobe tab.");
        IconGlyph.HelpMarker("With folders configured, only designs inside those Glamourer design-browser folder prefixes are scanned. Clear every folder to scan all saved designs.");

        DrawAllowlistBody(config.WardrobeFolderAllowlist, ref newWardrobeAllowlistFolder, "wardrobe");

        ImGui.Spacing();
        if (ImGui.Button("Rescan wardrobe"))
            plugin.OutfitCommand.Rescan();
        IconGlyph.HelpMarker("Re-reads your saved Glamourer designs. An empty folder list includes all designs; otherwise only matching folders are included.");

        DrawWardrobeScanFeedback();
    }

    private void DrawWardrobeScanFeedback()
    {
        var wardrobe = plugin.Configuration.WardrobeMapping;
        var lastScanTotal = plugin.OutfitCommand.LastScanTotalDesigns;

        if (lastScanTotal is null)
        {
            IconGlyph.WrappedDisabled("Not scanned yet this session.");
            return;
        }

        var matched = wardrobe.LocalDesigns.Count;
        var color = matched > 0 ? Theme.Success : Theme.Warning;
        var scope = plugin.Configuration.WardrobeFolderAllowlist.Count == 0 ? "all-design mode" : "folder-filtered mode";
        IconGlyph.WrappedColored(color, $"Found {lastScanTotal} saved design(s); {matched} available ({scope}).");

        if (matched == 0)
            return;

        if (ImGui.SmallButton("Copy names##wardrobe"))
            ImGui.SetClipboardText(string.Join("\n", wardrobe.LocalDesigns.Values.Select(d => d.Name)));
        IconGlyph.HelpMarker("Copies the list below as plain text, one design per line - paste it to your Owner (Discord, voice-to-text, etc) so they know exactly what names they can reference with a direct override (\"outfit lock <name>\").");

        using var _ = ImRaii.Child("wardrobeCatalog", new Vector2(0, 80), true);
        foreach (var entry in wardrobe.LocalDesigns.Values)
            ImGui.BulletText(entry.Name);
    }

    private void DrawGestureScanBody(PluginConfig config)
    {
        IconGlyph.Text(FontAwesomeIcon.TheaterMasks, "Animation mods to scan");
        ImGui.Separator();
        IconGlyph.WrappedDisabled("No selected mods = scan every installed mod. Select one or more to restrict scanning. Folder and text fields only filter this list.");

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
                if (mod.SortPath != null) { ImGui.SameLine(); IconGlyph.WrappedDisabled(mod.SortPath); }
            }
        }

        ImGui.Spacing();
        if (ImGui.Button("Rescan gestures"))
            plugin.GestureCommand.Rescan();
        IconGlyph.HelpMarker("Reads every installed mod when none are selected, or only explicit selections otherwise. Disabled mods remain eligible and are enabled temporarily when played.");

        DrawGestureScanFeedback();
    }

    private void DrawGestureScanFeedback()
    {
        var gestureMapping = plugin.Configuration.GestureMapping;
        var lastScanTotal = plugin.GestureCommand.LastScanTotalMods;

        if (lastScanTotal is null)
        {
            IconGlyph.WrappedDisabled("Not scanned yet this session.");
            return;
        }

        if (plugin.GestureCommand.LastScanError is { } error)
        {
            IconGlyph.WrappedColored(Theme.Danger, error);
            return;
        }
        var matched = gestureMapping.LocalCatalog.Count;
        var color = matched > 0 ? Theme.Success : Theme.Warning;
        var selectionCount = plugin.Configuration.SelectedGestureMods.Count;
        var scope = selectionCount == 0 ? "all-mod mode" : $"{selectionCount} explicitly selected";
        IconGlyph.WrappedColored(color, $"Found {lastScanTotal} installed mod(s); {scope}; {matched} animation trigger(s) discovered.");

        if (matched == 0 && lastScanTotal > 0)
        {
            ImGui.TextWrapped("No playable animation options were found in the current scan scope.");
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

    /// collar/moodles: no folder allowlist, unlike Wardrobe/Gesture - Moodles statuses have no folder-
    /// organization concept, every registered status is eligible (design.md's decision). Reads individual
    /// statuses (buffs/debuffs) rather than bundled presets, so the Owner can command a single status.
    private void DrawMoodlesScanBody(PluginConfig config)
    {
        IconGlyph.Text(FontAwesomeIcon.TheaterMasks, "Moodles status scan");
        ImGui.Separator();
        IconGlyph.WrappedDisabled("Reads your own registered Moodles statuses (buffs/debuffs) directly - nothing to allowlist. Moodles apply/clear commands live in the main window's Owner tab.");

        if (ImGui.Button("Rescan Moodles statuses"))
            plugin.MoodlesCommand.Rescan();
        IconGlyph.HelpMarker("Re-reads your registered statuses from your own Moodles plugin - run this after adding a new status before it'll show up for your Owner to reference.");

        DrawMoodlesScanFeedback();
    }

    private void DrawMoodlesScanFeedback()
    {
        var moodlesMapping = plugin.Configuration.MoodlesMapping;
        var lastScanTotal = plugin.MoodlesCommand.LastScanTotalStatuses;

        if (plugin.MoodlesCommand.LastScanStatus is MoodlesScanStatus.Unavailable or MoodlesScanStatus.Failed)
        {
            IconGlyph.WrappedColored(Theme.Danger, plugin.MoodlesCommand.LastScanError ?? "Moodles status scan failed.");
            if (moodlesMapping.LocalCatalog.Count > 0)
                IconGlyph.WrappedDisabled($"Keeping {moodlesMapping.LocalCatalog.Count} status(es) from the last successful scan.");
            return;
        }

        if (lastScanTotal is null)
        {
            IconGlyph.WrappedDisabled("Not scanned yet this session.");
            return;
        }

        IconGlyph.WrappedColored(Theme.Success, $"Scan succeeded: found {lastScanTotal} registered status(es).");

        if (lastScanTotal == 0)
            return;

        if (ImGui.SmallButton("Copy names##moodles"))
            ImGui.SetClipboardText(string.Join("\n", moodlesMapping.LocalCatalog.Values.Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x)));
        IconGlyph.HelpMarker("Copies the list below as plain text, one status per line - paste it to your Owner (Discord, voice-to-text, etc) so they know exactly what names they can reference with \"moodle apply <name>\".");

        using var _ = ImRaii.Child("moodlesCatalog", new Vector2(0, 80), true);
        foreach (var entry in moodlesMapping.LocalCatalog.Values)
        {
            ImGui.PushID(entry.StatusId);
            ImGui.BulletText(entry.Name);
            ImGui.PopID();
        }
    }

    /// Backs Settings' "Test an Owner command" control (`collar/chat-transport`'s "An Owner-style command
    /// can be tested entirely locally") - the one remaining local-test surface, now that every per-action
    /// Test button (collar/ui-organization) has been removed.
    private void DrawTestButton(string key, string label, Func<LocalTestResult> run)
    {
        if (ImGui.SmallButton($"{label}##{key}"))
            testResults[key] = (run(), Environment.TickCount64);

        if (testResults.TryGetValue(key, out var last))
        {
            if (Environment.TickCount64 - last.ShownAtTicks >= TestResultDisplayMs)
            {
                testResults.Remove(key);
            }
            else
            {
                ImGui.SameLine();
                IconGlyph.WrappedColored(last.Result.Success ? Theme.Success : Theme.Danger, last.Result.Message);
            }
        }
    }

    /// Wardrobe folder scopes use "empty = all" semantics and prefix matching when narrowed.
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

    private void DrawTosCard(PluginConfig config)
    {
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

    /// collar/custom-triggers "Sending a chat message requires its own dedicated permission and
    /// acknowledgement": deliberately its own card, visibly distinct from the general Automation risk
    /// acknowledgement above - a Custom Trigger's chat action is a materially broader surface (arbitrary
    /// text, any channel) than anything the general acknowledgement covers, so it gets its own explicit
    /// disclosure rather than riding on that existing checkbox.
    private void DrawCustomChatCard(PluginConfig config)
    {
        IconGlyph.Text(FontAwesomeIcon.Comments, "Custom Trigger chat messages");
        ImGui.Separator();
        IconGlyph.WrappedColored(Theme.Danger, "A Custom Trigger's chat action can send ANY text to ANY channel (including public party/say/yell chat), as your own character, triggered remotely by your Owner - unlike Gesture, which only ever fires a closed set of self-targeting pose/emote commands. Required before the \"Custom chat messages\" permission (Permissions tab) can be enabled at all.");

        if (ImGuiCheckbox("I understand a Custom Trigger's chat action can send arbitrary text to any channel, visible to other players, as my own character", config.CustomChatAcknowledged, out var newAck))
        {
            config.CustomChatAcknowledged = newAck;
            config.Save();
        }
    }

    /// A checkbox whose label can run long (the ToS/Custom-chat acknowledgement text) without getting cut
    /// off in a narrower window - `ImGui.Checkbox` never wraps its own label, so the checkbox itself
    /// carries no visible label (`##label` - id only) and the text is drawn separately, wrapped, right
    /// next to it.
    private static bool ImGuiCheckbox(string label, bool value, out bool newValue)
    {
        newValue = value;
        var changed = ImGui.Checkbox($"##{label}", ref newValue);
        ImGui.SameLine();
        ImGui.PushTextWrapPos(0f);
        ImGui.TextUnformatted(label);
        ImGui.PopTextWrapPos();
        return changed;
    }
}
