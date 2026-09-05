using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using Oathbound.Plugin.Commands;
using Oathbound.Plugin.Config;
using Oathbound.Plugin.Ipc;
using Oathbound.Plugin.Relay;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace Oathbound.Plugin.UI;

/// Role, pairing identity, trigger phrase, scan scopes, and scanning live here - the "infrastructure"
/// side of setup, shared regardless of which alias you're about to define. What each alias actually maps
/// to (title text, which scanned design/gesture, follow words) lives in CollarWindow's own Title/Wardrobe/
/// Gesture/Collar tabs instead. Safeword configuration stays in the always-visible character header.
/// Everything here stays
/// visible regardless of Role, same as CollarWindow's tabs - you can scan/configure before ever flipping
/// to Sub. Opened via the plugin installer's gear icon (OpenConfigUi) and the `/oathboundsettings` command.
public class SettingsWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string inviteTargetInput = "";
    private bool sendingInvitation;
    private bool acceptingInvitation;
    private bool confirmingIdentityReset;
    private bool confirmingInviteReplace;
    private string triggerPhraseInput = "";
    private string gestureModSearch = "";
    private string penumbraFolderSearch = "";
    private string newWardrobeAllowlistFolder = "";
    private string? scanAndExportResult;
    private string testCommandInput = "";
    private int testCustomTriggerIndex;

    /// Transient, session-only per-action local Test feedback (collar/ui-organization) - see
    /// CollarWindow's matching field for the rest of the Test controls; Moodles' only lives here since it
    /// has no Sub-facing module of its own. Each entry auto-clears a short time after being shown (see
    /// DrawTestButton).
    private readonly Dictionary<string, (LocalTestResult Result, long ShownAtTicks)> testResults = new();

    /// How long a local Test control's result stays visible before auto-clearing (collar/ui-organization).
    private const long TestResultDisplayMs = 4_000;

    private static readonly string[] RoleNames = ["Sub", "Owner"];

    public SettingsWindow(Plugin plugin) : base("Oathbound - Settings###CollarSettingsWindow")
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
            ImGui.Spacing();
            DrawFavoritesButtonCard(config);
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

        var aliases = config.Aliases;
        var savedTriggers = new List<(string Label, string Command)>
        {
            ($"Title · Clear", aliases.ClearTitleAlias),
            ("Wardrobe · Unlock", "unlock"),
            ($"Follow · Engage", aliases.Follow.EngageAlias),
            ($"Follow · Release", aliases.Follow.ReleaseAlias),
            ($"Moodle · Clear", aliases.ClearMoodleAlias),
            ("Collar · Lock", "collar lock"),
            ("Collar · Unlock", "collar unlock"),
            ("Restraints · Unlock all", "restraint unlock"),
        };
        savedTriggers.AddRange(aliases.Titles.Select(a => ($"Title · {a.Alias}", a.Alias)));
        savedTriggers.AddRange(aliases.Outfits.Select(a => ($"Wardrobe · {a.Alias}", a.Alias)));
        savedTriggers.AddRange(aliases.Gestures.Select(a => ($"Gesture · {a.Alias}", a.Alias)));
        savedTriggers.AddRange(aliases.Moodles.Select(a => ($"Moodle · {a.Alias}", a.Alias)));
        savedTriggers.AddRange(aliases.Restraints.Select(a => ($"Restraint · {a.Alias}", a.Alias)));
        savedTriggers.AddRange(aliases.CustomTriggers.Select(a => ($"Custom Trigger · {a.Alias}", a.Alias)));
        savedTriggers.AddRange(config.RestraintMapping.ConfiguredMods
            .Where(x => x.ItemId > 0 && GlamourerIpc.GetItemSlot((uint)x.ItemId.Value) is not null && x.Rules.Count > 0 &&
                        config.RestraintMapping.LocalCatalog.ContainsKey(x.CatalogId))
            .Select(x => ($"Restraint · {x.Name}", RestraintCommand.BuildCatalogLockCommand(
                x.CatalogId, x.Name, x.ItemId!.Value, x.Rules))));

        ImGui.TextUnformatted("Choose one of your triggers");
        testCustomTriggerIndex = Math.Clamp(testCustomTriggerIndex, 0, savedTriggers.Count - 1);
        var triggerNames = savedTriggers.Select(t => t.Label).ToArray();
        ImGui.SetNextItemWidth(Math.Max(180, ImGui.GetContentRegionAvail().X));
        ImGui.Combo("##testSavedTrigger", ref testCustomTriggerIndex, triggerNames, triggerNames.Length);
        var selectedCommand = $"{config.TriggerPhrase.Trim()} {savedTriggers[testCustomTriggerIndex].Command}".Trim();
        if (ImGui.SmallButton("Put in test box"))
            testCommandInput = selectedCommand;
        ImGui.SameLine();
        DrawTestButton("testSelectedTrigger", "Run selected trigger", () => plugin.ChatCommandListener.TestIncomingCommand(selectedCommand));
        IconGlyph.WrappedDisabled($"What the Owner sends: {selectedCommand}");
        ImGui.Spacing();

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        ImGui.InputTextWithHint("##testCommandInput", "e.g. ray outfit lock kagome", ref testCommandInput, 500);
        if (ImGui.SmallButton("Paste"))
        {
            var clipboard = ImGui.GetClipboardText() ?? "";
            testCommandInput = clipboard[..Math.Min(clipboard.Length, 500)];
        }
        ImGui.SameLine();
        using (ImRaii.Disabled(testCommandInput.Length == 0))
        {
            if (ImGui.SmallButton("Copy"))
                ImGui.SetClipboardText(testCommandInput);
            ImGui.SameLine();
            if (ImGui.SmallButton("Clear"))
                testCommandInput = "";
        }
        DrawTestButton("testOwnerCommand", "Run test", () => plugin.ChatCommandListener.TestIncomingCommand(testCommandInput));
    }

    /// collar/pairing "Sub's pairing identity configuration locks while paired": Role and the trigger
    /// phrase become read-only for a paired Sub - enforced here in the rendering layer only
    /// (ImRaii.Disabled), never in PluginConfig/PairingService, the same "UI-only lock" shape pairing's own
    /// "Sub can't unpair except via panic" has always used (see PairingService.ReleasePeer's comment). The
    /// Owner side is never locked, paired or not. Pairing is relay-assisted only - there is no manual
    /// fallback (collar/pairing "Pairing has no manual fallback and never silently weakens").
    private void DrawIdentityCard(PluginConfig config)
    {
        var pairingService = plugin.PairingService;
        var pending = pairingService.Pending;
        var sameRoleWarning = pending is { } pendingCheck && pendingCheck.SenderRole == config.Role;
        var pairingLocked = config.Pairing.IsPaired;
        var subLocked = config.Role == PluginRole.Sub && pairingLocked;

        IconGlyph.Text(FontAwesomeIcon.UserShield, "Identity & Pairing");
        ImGui.Separator();

        DrawDeviceIdentitySection();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

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
        IconGlyph.HelpMarker("Sub reacts to trigger tells and applies commands locally - only Sub actually gates anything. Owner is mostly informational. Either role can send the invitation first - whoever does, the other side just needs to Accept.");

        ImGui.Spacing();
        IconGlyph.WrappedDisabled("Secure Oathbound relay enabled.");
        IconGlyph.HelpMarker("Pairing and encrypted catalog synchronization use Oathbound's fixed Cloudflare relay. The endpoint cannot be changed by plugin configuration.");
        if (plugin.RelayClient.LastReachable is { } reachable)
            IconGlyph.WrappedColored(reachable ? Theme.Success : Theme.Warning,
                reachable ? "Relay connection verified." : "Relay was unreachable on the last attempt; existing pairing and panic remain local-first.");
        ImGui.Spacing();
        ImGui.TextWrapped("Send an invitation: enter who to pair with, exactly as you'd address a tell, then click Send.");
        using (ImRaii.Disabled(pairingLocked || sendingInvitation))
        {
            ImGui.InputTextWithHint("Pair with", "Name Surname@World", ref inviteTargetInput, 64);
            using (ImRaii.Disabled(inviteTargetInput.Trim().Length == 0 || confirmingInviteReplace))
            {
                if (ImGui.Button(sendingInvitation ? "Sending..." : "Send Invitation"))
                {
                    if (plugin.PairingService.DescribeOutstandingInvitation() is { } outstanding)
                        confirmingInviteReplace = true;
                    else
                    {
                        sendingInvitation = true;
                        Plugin.FireAndForget(SendInvitationAsync(inviteTargetInput.Trim()));
                    }
                }
            }
        }
        if (confirmingInviteReplace && plugin.PairingService.DescribeOutstandingInvitation() is { } outstandingInvite)
        {
            IconGlyph.WrappedColored(Theme.Danger, $"You already have an unconfirmed invitation outstanding to {outstandingInvite.Target}. Sending a new one abandons it - if they accept it later, nothing will happen on your side.");
            if (ImGui.Button("Send new invitation anyway"))
            {
                confirmingInviteReplace = false;
                sendingInvitation = true;
                Plugin.FireAndForget(SendInvitationAsync(inviteTargetInput.Trim()));
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
                confirmingInviteReplace = false;
        }
        else if (confirmingInviteReplace)
        {
            // The outstanding invitation expired/completed on its own while this prompt was open.
            confirmingInviteReplace = false;
        }
        IconGlyph.HelpMarker("Creates a single-use relay invitation (expires in 15 minutes) and sends its reference in one tell. They accept it, an acknowledgement tell comes back automatically, and you're both paired.");

        using (ImRaii.Disabled(subLocked))
        {
            if (ImGui.InputText("Trigger phrase", ref triggerPhraseInput, 32))
            {
                config.TriggerPhrase = triggerPhraseInput;
                config.Save();
            }
        }
        IconGlyph.HelpMarker("The word that must start every ongoing command tell, e.g. \"command strip\".");

        if (pairingLocked)
            IconGlyph.WrappedColored(Theme.TextMuted, "Locked while paired - trigger /oathboundpanic to release pairing and change these again.");

        if (pairingService.LastError is { Length: > 0 } lastError)
            IconGlyph.WrappedColored(Theme.Danger, lastError);

        IconGlyph.WrappedDisabled($"Pairing status: {pairingService.Phase}.");
        if (pairingService.OutgoingInvitationExpiresAt is { } outgoingExpiry)
        {
            var remaining = TimeSpan.FromSeconds(Math.Max(0, outgoingExpiry - DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            IconGlyph.WrappedDisabled($"Invitation sent to {pairingService.OutgoingInvitationTarget}; expires in {remaining.Minutes}m {remaining.Seconds}s.");
        }

        ImGui.Spacing();
        if (config.Pairing.IsPaired)
        {
            IconGlyph.WrappedColored(Theme.Success, $"Paired with {config.Pairing.PeerName}@{config.Pairing.PeerWorld}.");
            if (config.Pairing.PeerTriggerPhrase is { Length: > 0 } peerPhrase)
                IconGlyph.WrappedDisabled($"Trigger phrase in effect: \"{peerPhrase}\" (from your paired peer).");
            else
                IconGlyph.WrappedDisabled($"Trigger phrase in effect: \"{config.TriggerPhrase}\" (your own - peer hasn't sent theirs).");
            if (config.Role == PluginRole.Owner)
            {
                if (ImGui.Button("Release pairing"))
                    pairingService.ReleasePeer();
                IconGlyph.HelpMarker("Clears who you're paired with on your own client only - doesn't touch your Sub's plugin at all. Fixes a stale/wrong pairing or frees them up to pair with someone else.");
            }
            else
            {
                IconGlyph.WrappedDisabled("Locked - only /oathboundpanic (your safeword, below) unpairs, not this screen.");
            }
        }
        else if (pending is { } request)
        {
            var roleLabel = request.SenderRole == PluginRole.Owner ? "your Owner" : "your Sub";
            var expiresIn = TimeSpan.FromSeconds(Math.Max(0, request.ExpiresAt - DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            var invitationExpired = request.ExpiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            IconGlyph.WrappedColored(Theme.Warning, $"Invitation from {request.Name}@{request.World} (verified sender, signature checked) - they say they'll be {roleLabel}. Expires in {expiresIn.Minutes}m {expiresIn.Seconds}s.");
            if (sameRoleWarning)
                IconGlyph.WrappedColored(Theme.Danger, $"You're both set to {config.Role} - one of you should switch Role above, or nothing will ever trigger.");

            using (ImRaii.Disabled(acceptingInvitation || invitationExpired))
            {
                if (ImGui.Button(acceptingInvitation ? "Accepting..." : "Accept"))
                {
                    acceptingInvitation = true;
                    Plugin.FireAndForget(AcceptInvitationAsync());
                }
            }
            IconGlyph.HelpMarker("Trusts this sender as your paired peer from now on and locks pairing on.");
            if (invitationExpired)
                IconGlyph.WrappedColored(Theme.Warning, "This invitation expired. Reject it and ask the sender to create another.");
            ImGui.SameLine();
            if (ImGui.Button("Reject"))
                pairingService.DismissPending();
        }
        else if (pairingService.AwaitingActivation)
        {
            IconGlyph.WrappedColored(Theme.Warning, "Waiting for the other side to confirm...");
        }
        else
        {
            IconGlyph.WrappedColored(Theme.TextMuted, "Not paired - no pending invitation.");
        }

        if (config.Pairing.LastRevocationDeliveryStatus is { Length: > 0 } delivery)
        {
            var label = delivery switch
            {
                "delivered" => "Last unpair/panic relay notice was delivered.",
                "pending" => "Local unpair/panic completed; relay notification is pending retry.",
                "expired" => "Local unpair/panic completed; its relay notification expired before delivery.",
                _ => "Local unpair/panic completed; its relay notification failed.",
            };
            IconGlyph.WrappedColored(delivery == "delivered" ? Theme.Success : Theme.Warning, label);
        }
    }

    private async System.Threading.Tasks.Task SendInvitationAsync(string target)
    {
        try
        {
            await plugin.PairingService.CreateAndSendInvitationAsync(target, CancellationToken.None);
        }
        finally
        {
            sendingInvitation = false;
        }
    }

    private async System.Threading.Tasks.Task AcceptInvitationAsync()
    {
        try
        {
            await plugin.PairingService.AcceptPendingAsync(CancellationToken.None);
        }
        finally
        {
            acceptingInvitation = false;
        }
    }

    /// collar/pairing "Device-key lifecycle is recoverable and explicit". Fingerprint only - never the
    /// public key coordinates or, obviously, the private key.
    private void DrawDeviceIdentitySection()
    {
        var identity = plugin.DeviceIdentityService;
        IconGlyph.Text(FontAwesomeIcon.Fingerprint, "Device Identity");
        var fingerprint = identity.DeviceKeyId is { Length: >= 16 } id ? id[..16] : identity.DeviceKeyId ?? "(none)";
        ImGui.TextUnformatted($"Fingerprint: {fingerprint}...");
        IconGlyph.HelpMarker("Identifies this installation to the relay - never your character. Regenerated only on an explicit reset below.");

        if (!OperatingSystem.IsWindows())
        {
            IconGlyph.WrappedColored(Theme.Warning, "Running under Wine: the private key is stored without a real OS-backed protection guarantee (Wine's DPAPI does not provide one). Treat a compromised machine as requiring a reset below.");
        }

        using (ImRaii.Disabled(confirmingIdentityReset))
        {
            if (ImGui.Button("Reset device identity"))
                confirmingIdentityReset = true;
        }
        if (confirmingIdentityReset)
        {
            IconGlyph.WrappedColored(Theme.Danger, "This ends every relay-assisted pairing this device holds and cannot be undone. Are you sure?");
            if (ImGui.Button("Confirm reset"))
            {
                Plugin.FireAndForget(plugin.PairingService.ResetDeviceIdentityAsync(CancellationToken.None));
                confirmingIdentityReset = false;
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
                confirmingIdentityReset = false;
        }
    }

    private static readonly string[] FavoritesButtonCornerNames = ["Top Left", "Top Right", "Bottom Left", "Bottom Right"];

    /// collar/ui-organization "A movable on-screen button opens the quick-access favorites menu": lets the
    /// Owner reposition FavoritesBarButton via a corner preset + pixel margin, instead of dragging it
    /// directly (design.md's recorded scope decision).
    private void DrawFavoritesButtonCard(PluginConfig config)
    {
        IconGlyph.Text(FontAwesomeIcon.Star, "Quick-access button");
        ImGui.Separator();
        ImGui.TextWrapped("A small on-screen button that opens your favorited quick commands - the same menu the server info bar entry opens.");

        var favoritesButton = config.FavoritesButton;
        var cornerIndex = (int)favoritesButton.Corner;
        if (ImGui.Combo("Position##favoritesButton", ref cornerIndex, FavoritesButtonCornerNames, FavoritesButtonCornerNames.Length))
        {
            favoritesButton.Corner = (ScreenCorner)cornerIndex;
            config.Save();
        }

        var margin = favoritesButton.Margin;
        if (ImGui.DragFloat2("Margin##favoritesButton", ref margin, 1f, 0f, 400f))
        {
            favoritesButton.Margin = margin;
            config.Save();
        }
        IconGlyph.HelpMarker("How far the button sits from the chosen screen corner, in pixels.");
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
            plugin.RestraintCommand.RescanCatalog();
            scanAndExportResult = null;
        }
        IconGlyph.HelpMarker("Rescans Wardrobe, Gesture, Moodles, and the explicitly shared Penumbra restraint folders. Captured item devices are left untouched.");

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
        DrawRestraintScanBody(config);
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
        IconGlyph.WrappedDisabled("No folders and no selected mods scans everything. Folders select a union; explicit mods narrow that union.");
        DrawPenumbraFolderPicker("Animation folders", config.SelectedGestureFolders, config);
        ImGui.InputTextWithHint("##gestureModSearch", "Search mod names...", ref gestureModSearch, 128);
        var installed = plugin.GestureCommand.GetInstalledMods();
        using (ImRaii.Child("gestureModPicker", new Vector2(0, 180), true))
        {
            foreach (var mod in installed.Where(m =>
                         (config.SelectedGestureFolders.Count == 0 || (m.SortPath is { } path && config.SelectedGestureFolders.Any(f =>
                             path.Equals(f, StringComparison.OrdinalIgnoreCase) || path.StartsWith(f + "/", StringComparison.OrdinalIgnoreCase)))) &&
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

    private void DrawRestraintScanBody(PluginConfig config)
    {
        IconGlyph.Text(FontAwesomeIcon.Lock, "Shared Penumbra restraints");
        ImGui.Separator();
        IconGlyph.WrappedDisabled("Only options below folders selected here are shared with your Owner. No folders means no Penumbra restraints are shared.");
        DrawPenumbraFolderPicker("Restraint folders", config.SelectedRestraintFolders, config);
        if (ImGui.Button("Rescan restraints")) plugin.RestraintCommand.RescanCatalog();
        var command = plugin.RestraintCommand;
        if (command.LastScanError is { } error) IconGlyph.WrappedColored(Theme.Danger, error);
        else if (command.LastScanTotalMods is not null)
            IconGlyph.WrappedColored(config.RestraintMapping.LocalCatalog.Count > 0 ? Theme.Success : Theme.Warning,
                $"Matched {command.LastScanMatchedMods} mod(s); found {config.RestraintMapping.LocalCatalog.Count} restraint option(s).");
        if (config.SelectedRestraintFolders.Count == 0)
            IconGlyph.WrappedColored(Theme.Warning, "No folders selected: the shared Penumbra restraint catalog is empty.");
        using var child = ImRaii.Child("restraintCatalogPreview", new Vector2(0, 100), true);
        foreach (var entry in config.RestraintMapping.LocalCatalog.Values.OrderBy(x => x.ModName))
            ImGui.BulletText(entry.ModName);
    }

    private void DrawPenumbraFolderPicker(string label, List<string> selected, PluginConfig config)
    {
        var installed = plugin.GestureCommand.GetInstalledMods();
        var folders = installed.Select(x => x.SortPath).Where(x => !string.IsNullOrWhiteSpace(x))
            .SelectMany(x => ParentFolders(x!)).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        var preview = selected.Count == 0 ? "None" : $"{selected.Count} selected";
        if (ImGui.BeginCombo($"{label}##{label}", preview))
        {
            ImGui.InputTextWithHint($"##folderSearch{label}", "Search folders...", ref penumbraFolderSearch, 128);
            foreach (var folder in folders.Where(x => string.IsNullOrWhiteSpace(penumbraFolderSearch) || x.Contains(penumbraFolderSearch, StringComparison.OrdinalIgnoreCase)))
            {
                var chosen = selected.Contains(folder, StringComparer.OrdinalIgnoreCase);
                if (ImGui.Selectable(folder, chosen, ImGuiSelectableFlags.DontClosePopups))
                {
                    if (chosen) selected.RemoveAll(x => x.Equals(folder, StringComparison.OrdinalIgnoreCase)); else selected.Add(folder);
                    config.Save();
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(folder);
            }
            ImGui.EndCombo();
        }
        foreach (var folder in selected.ToList())
        {
            var missing = !folders.Contains(folder, StringComparer.OrdinalIgnoreCase);
            ImGui.TextUnformatted(missing ? $"{folder} (missing)" : folder);
            ImGui.SameLine();
            if (ImGui.SmallButton($"Remove##{label}{folder}")) { selected.Remove(folder); config.Save(); }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(folder);
        }
    }

    private static IEnumerable<string> ParentFolders(string path)
    {
        var normalized = path.Replace('\\', '/').Trim('/');
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 1; i < parts.Length; i++) yield return string.Join('/', parts.Take(i));
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
