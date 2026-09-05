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
/// to (title text, which scanned design/animation, follow words) lives in CollarWindow's own Title/Outfit/
/// Animation/Collar tabs instead. Safeword configuration stays in the always-visible character header.
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

    /// Split into tabs (previously one long vertically-stacked flow) once this window's growth made it too
    /// tall to comfortably navigate in one scroll region - each tab now scrolls independently within the
    /// window's remaining space. Grouped by what they're for, not just by prior visual order: Identity &
    /// Pairing is setup you do once; ToS bundles every risk acknowledgement together with the one local
    /// testing tool, since testing a command is usually the next thing you do right after enabling a
    /// permission those acknowledgements gate. Scanning itself moved to the main window's Sync tab
    /// (collar/ui-organization) - it's catalog upkeep, grouped with the rest of catalog sync now rather
    /// than living here.
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
            ImGui.Spacing();
            DrawTutorialCard(config);
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
            ("Outfit · Unlock", "unlock"),
            ($"Follow · Engage", aliases.Follow.EngageAlias),
            ($"Follow · Release", aliases.Follow.ReleaseAlias),
            ($"Moodle · Clear", aliases.ClearMoodleAlias),
            ("Collar · Lock", "collar lock"),
            ("Collar · Unlock", "collar unlock"),
            ("Restraints · Unlock all", "restraint unlock"),
        };
        savedTriggers.AddRange(aliases.Titles.Select(a => ($"Title · {a.Alias}", a.Alias)));
        savedTriggers.AddRange(aliases.Outfits.Select(a => ($"Outfit · {a.Alias}", a.Alias)));
        savedTriggers.AddRange(aliases.Gestures.Select(a => ($"Animation · {a.Alias}", a.Alias)));
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

        ImGui.TextWrapped("Role determines which side of the pairing you are - every shared category tab in the main window shows its Sub or Owner view based on this, so nothing here is hidden by Role.");
        using (ImRaii.Disabled(subLocked))
        {
            var roleIndex = config.Role == PluginRole.Owner ? 1 : 0;
            if (ImGui.Combo("Role", ref roleIndex, RoleNames, RoleNames.Length))
            {
                config.Role = roleIndex == 1 ? PluginRole.Owner : PluginRole.Sub;
                config.Save();
                // collar/onboarding "Tutorial completion is tracked independently per Role": the shared
                // path (also used by the Welcome window) for launching a Role's guided tutorial the first
                // time that Role is ever selected on this install.
                plugin.TutorialDriver.StartIfUnseen(config.Role);
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

    /// collar/onboarding "Settings offers a control to rerun the current Role's tutorial": its own card at
    /// the bottom of Identity & Pairing, below the quick-access button card - a deliberate on-demand action
    /// rather than one more control folded into the pairing/identity card above it.
    private void DrawTutorialCard(PluginConfig config)
    {
        IconGlyph.Text(FontAwesomeIcon.GraduationCap, "Guided tutorial");
        ImGui.Separator();
        ImGui.TextWrapped("Replays the guided tour of each tab for your current Role, even if you've already seen it.");

        if (ImGui.Button("Rerun Tutorial"))
            plugin.TutorialDriver.Start(config.Role);
        IconGlyph.HelpMarker("Doesn't affect the other Role's own first-time tutorial.");
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

    private void DrawTosCard(PluginConfig config)
    {
        IconGlyph.Text(FontAwesomeIcon.ExclamationTriangle, "Automation risk acknowledgement");
        ImGui.Separator();
        ImGui.TextWrapped("Required before the Animation/Follow permission toggles can be enabled - see the README.");

        if (ImGuiCheckbox("I understand the automation-risk caveat (input blocking; sending is always your own click)", config.TosAcknowledged, out var newTos))
        {
            config.TosAcknowledged = newTos;
            config.Save();
        }
        IconGlyph.HelpMarker("Required once before the Animation and Follow permission toggles (in the Sub window's Permissions tab) can be enabled at all - Title and Outfit don't need it.");
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
        IconGlyph.WrappedColored(Theme.Danger, "A Custom Trigger's chat action can send ANY text to ANY channel (including public party/say/yell chat), as your own character, triggered remotely by your Owner - unlike Animation, which only ever fires a closed set of self-targeting pose/emote commands. Required before the \"Custom chat messages\" permission (Permissions tab) can be enabled at all.");

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
