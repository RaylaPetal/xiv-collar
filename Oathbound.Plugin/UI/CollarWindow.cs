using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Oathbound.Plugin.Commands;
using Oathbound.Plugin.Config;
using Oathbound.Plugin.Ipc;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Glamourer.Api.Enums;

namespace Oathbound.Plugin.UI;

/// One window for both roles - Role (Settings) only changes what a few tabs say and whether incoming
/// tells apply locally (ChatCommandListener), it no longer decides which window opens. Title/Wardrobe/
/// Gesture/Permissions are "what I've set up for someone who might command me" and stay available
/// regardless of Role (you might configure your own aliases before ever flipping to Sub); Owner is "what
/// I need to command someone else" and is the one tab that's actually role-specific in spirit, even though
/// nothing stops using it while set to Sub. Pairing status stays permanently above the nav bar. There is
/// deliberately no panic button here - panic is the /oathboundpanic safeword (Settings), typed rather than
/// clicked, so it can't be hit by accident or spotted by someone watching over a shoulder.
public class CollarWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string activeModule = "title";

    /// collar/ui-organization: lets the quick-access menu (or anything else outside this class) bring the
    /// main window forward already on the Owner tab, instead of leaving it wherever it last was. A no-op
    /// for the tab switch specifically when Role is Sub, matching the tab's own hidden state in Draw() -
    /// still opens the window itself, just without landing on a tab that isn't there to land on.
    public void OpenOwnerTab()
    {
        if (plugin.Configuration.Role == PluginRole.Owner)
            activeModule = "owner";
        IsOpen = true;
    }

    /// collar/ui-organization "A movable on-screen button opens the quick-access favorites menu": the
    /// menu's "Open main window" control - opens the window wherever it last was, unlike OpenOwnerTab
    /// which forces the Owner tab.
    public void OpenMainWindow() => IsOpen = true;

    private string newTitleAlias = "";
    private string newTitleText = "";
    private bool newTitleIsPrefix;
    private Vector3 newTitleColor = new(1, 1, 1);

    private string newOutfitAlias = "";
    private int newOutfitDesignIndex;
    private bool newOutfitLocked = true;

    private string newGestureAlias = "";
    private GestureCatalogEntry? selectedAliasGesture;

    private string newMoodleAlias = "";
    private int newMoodleStatusIndex;

    private string ctNewAlias = "";
    private readonly List<CustomTriggerAction> ctDraftActions = new();
    private int ctNewActionKindIndex;
    private string ctTitleText = "";
    private bool ctTitleIsPrefix;
    private Vector3 ctTitleColor = new(1, 1, 1);
    private int ctOutfitDesignIndex;
    private GestureCatalogEntry? ctSelectedGesture;
    private int ctMoodleStatusIndex;
    private int ctRestraintDeviceIndex;
    private string ctChatText = "";
    private int? editingCustomTriggerIndex;
    private int? editingCustomTriggerActionIndex;

    /// Owner-side ad-hoc Custom Trigger draft (collar/custom-triggers "custom commands should also be
    /// creatable via the Owner commands menu") - separate field set from the Sub-side draft above (ct*),
    /// since these live on a different tab and build actions by NAME only rather than by picking from this
    /// client's own local catalogs (the Owner's install has no access to the Sub's WardrobeMapping/
    /// GestureMapping/MoodlesMapping/RestraintMapping - only the Sub does), mirroring the freeform
    /// "type the exact name your Sub told you" pattern the other Owner quick-command sections already use.
    private string ctqLabel = "";
    private readonly List<CustomTriggerAction> ctqDraftActions = new();
    private int ctqKindIndex;
    private string ctqTitleText = "";
    private bool ctqTitleIsPrefix;
    private Vector3 ctqTitleColor = new(1, 1, 1);
    private string ctqOutfitName = "";
    private string ctqGestureName = "";
    private string ctqMoodleName = "";
    private string ctqRestraintName = "";
    private string ctqChatText = "";
    private int? editingOwnerActionIndex;
    private QuickCommand? editingOwnerBundle;

    private string newDeviceName = "";
    private int newDeviceSlotIndex;
    private ulong? newDeviceItemId;
    private readonly RestraintRuleEditState newDeviceRuleEdit = new();
    private string? editingDeviceId;

    private string newRestraintAlias = "";
    private int newRestraintDeviceIndex;

    /// Owner-side ad-hoc device draft (collar/restraints "Owner-authored ad-hoc restraint device") - a
    /// slot+item picked directly, with no Sub-side captured device to reference by name.
    private int newAdHocSlotIndex;
    private ulong? newAdHocItemId;
    private string newAdHocLabel = "";
    private readonly RestraintRuleEditState newAdHocRuleEdit = new();

    private static readonly string[] PoseNames = ["Ground Sit", "Sit", "Doze"];

    private string commandInput = "";
    private string newTitleQuickText = "";
    private bool newTitleQuickIsPrefix;
    private Vector3 newTitleQuickColor = new(1, 1, 1);
    private string newFollowQuickText = "";
    private string newRestraintQuickText = "";
    private string? importResult;
    private string? resetImportsResult;
    private bool revealSafeword;
    private int newCollarMoodleStatusIndex;

    /// collar/ui-organization: search text filtering the Owner's Gesture quick-command list.
    private string gestureQuickSearch = "";
    private QuickCommand? editingQuickCommand;
    private List<QuickCommand>? editingQuickList;
    private string editingQuickLabel = "";
    private string editingQuickPayload = "";
    private string editingQuickTarget = "";
    private string editingQuickOriginalTarget = "";
    private bool editingQuickTitleIsPrefix;
    private bool editingQuickOriginalTitleIsPrefix;
    private Vector3 editingQuickTitleColor = new(1, 1, 1);
    private Vector3 editingQuickOriginalTitleColor = new(1, 1, 1);

    private enum QuickEditCategory { Raw, Title, Outfit, Gesture, Follow, Moodle }
    private QuickEditCategory editingQuickCategory;

    /// Owner-side per-quick-command rule editor state (collar/restraints "Owner assigns rules to a quick
    /// command"), keyed by the quick command's Label - transient UI-only state, not persisted itself (the
    /// chosen rules are saved onto the QuickCommand on "Save rules").
    private readonly HashSet<string> expandedRestraintRuleEditors = new();
    private readonly Dictionary<string, RestraintRuleEditState> restraintRuleEdits = new();

    private sealed class RestraintRuleEditState
    {
        public bool ForcedPose;
        public int PoseIndex;
        public bool WalkOnly;
        public bool ActionBlock;
        public bool GagChat;
        public bool ArmsCuffed;
        public string? ArmsCuffedAnimationId;
        public bool LegsCuffed;
        public string? LegsCuffedAnimationId;
        public bool FullBodyCuffed;
        public string? FullBodyCuffedAnimationId;
    }

    private static readonly (string Id, FontAwesomeIcon Icon, string Tooltip)[] NavItems =
    [
        ("title", FontAwesomeIcon.Heading, "Title"),
        ("wardrobe", FontAwesomeIcon.Tshirt, "Wardrobe"),
        ("gesture", FontAwesomeIcon.TheaterMasks, "Gesture"),
        ("moodles", FontAwesomeIcon.Smile, "Moodles"),
        ("restraints", FontAwesomeIcon.Handcuffs, "Restraints"),
        ("customtriggers", FontAwesomeIcon.BoltLightning, "Custom Triggers"),
        ("collar", FontAwesomeIcon.Lock, "Collar"),
        ("permissions", FontAwesomeIcon.ShieldAlt, "Permissions"),
        ("owner", FontAwesomeIcon.Crown, "Owner"),
    ];

    public CollarWindow(Plugin plugin) : base("Oathbound###CollarWindow")
    {
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(460, 520), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
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
        DrawCharacterHeader();
        ImGui.Spacing();

        // The Owner tab sends commands meant to apply to a *different*, paired Sub - a character
        // currently configured as Sub has no use for it (their own client never reacts to anything it
        // sends, per ChatCommandListener.OnChatMessage's Role check), and its presence was misleading Subs
        // into thinking sending themselves a command through it was a supported way to test their own
        // aliases. Hidden outright rather than merely disabled, so there's nothing there to misread.
        var isOwner = plugin.Configuration.Role == PluginRole.Owner;
        var visibleNavItems = isOwner ? NavItems : NavItems.Where(item => item.Id != "owner").ToArray();
        if (!isOwner && activeModule == "owner")
            activeModule = "title";

        if (NavBar.Draw(activeModule, "owner", visibleNavItems) is { } clicked)
            activeModule = clicked;

        ImGui.Spacing();
        using var card = Card.Begin("moduleCard");
        switch (activeModule)
        {
            case "title":
                DrawTitleModule();
                break;
            case "wardrobe":
                DrawWardrobeModule();
                break;
            case "gesture":
                DrawGestureModule();
                break;
            case "moodles":
                DrawMoodlesModule();
                break;
            case "restraints":
                DrawRestraintsModule();
                break;
            case "customtriggers":
                DrawCustomTriggersModule();
                break;
            case "collar":
                DrawCollarModule();
                break;
            case "owner":
                DrawOwnerModule();
                break;
            case "permissions":
                DrawPermissionsCard();
                break;
        }
        DrawQuickCommandEditor();
    }

    /// Both roles can receive a Pending handshake now (collarpair's role token - see
    /// ChatCommandListener), so this is one role-aware card instead of two windows each handling their own
    /// half. Sub's accepted pairing stays locked (only /oathboundpanic, the safeword command, undoes it);
    /// Owner's has a plain Release button, since nothing is actually applied to the Owner's own character
    /// for panic to revert.
    private void DrawCharacterHeader()
    {
        var pending = plugin.PairingService.Pending;
        var peerUnpairedNotice = plugin.ChatCommandListener.PeerUnpairedNotice;
        var pairing = plugin.Configuration.Pairing;
        var config = plugin.Configuration;
        var character = CharacterHeaderModel.Current();
        var sameRoleWarning = pending is { } p && p.SenderRole == config.Role;

        ImGui.PushID("characterHeader");
        if (!ImGui.BeginTable("banner", 1, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.PopID();
            return;
        }
        ImGui.TableNextRow();
        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(Theme.CardBg));
        ImGui.TableNextColumn();

        ImGui.PushStyleColor(ImGuiCol.Text, Theme.AccentHover);
        IconGlyph.Text(FontAwesomeIcon.UserCircle, character.Name ?? "Character loading…");
        ImGui.PopStyleColor();

        if (character.IsAvailable)
        {
            var details = character.HomeWorld is { Length: > 0 } ? character.HomeWorld : "Home world unavailable";
            if (character.FreeCompany is { Length: > 0 })
                details += $"  ·  «{character.FreeCompany}»";
            IconGlyph.WrappedDisabled(details);
        }
        else
        {
            IconGlyph.WrappedDisabled("Local character details will appear after login. Pairing and safety controls remain available.");
        }

        ImGui.Spacing();
        ImGui.Separator();

        if (pending is { } request)
        {
            var roleLabel = request.SenderRole == PluginRole.Owner ? "your Owner" : "your Sub";
            var expiresIn = TimeSpan.FromSeconds(Math.Max(0, request.ExpiresAt - DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            var invitationExpired = request.ExpiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            IconGlyph.WrappedColored(Theme.Warning, $"Pending request: {request.Name}@{request.World} wants to pair as {roleLabel} (expires in {expiresIn.Minutes}m {expiresIn.Seconds}s).");
            if (sameRoleWarning)
                IconGlyph.WrappedColored(Theme.Danger, $"You're both set to {config.Role} in Settings - one of you should switch, or nothing will ever trigger.");

            using (ImRaii.Disabled(invitationExpired))
                if (ImGui.Button("Accept"))
                    Plugin.FireAndForget(plugin.PairingService.AcceptPendingAsync(System.Threading.CancellationToken.None));
            IconGlyph.HelpMarker("Trusts this sender as your paired peer from now on. Locks pairing on if you're set to Sub (only /oathboundpanic, your safeword command, undoes it) - if you're set to Owner, Release pairing (below, once accepted) undoes it any time.");
            ImGui.SameLine();
            if (ImGui.Button("Reject"))
                plugin.PairingService.DismissPending();
        }
        else if (peerUnpairedNotice is { } ownerNotice && config.Role == PluginRole.Owner)
        {
            var peerLabel = ownerNotice.PeerRole == PluginRole.Owner ? "Your Owner's" : "Your Sub's";
            IconGlyph.WrappedColored(Theme.Warning, $"{peerLabel} side ended pairing via panic - they will not receive any commands until you pair again.");
            if (ImGui.Button("Release pairing"))
            {
                plugin.PairingService.ReleasePeer();
                plugin.ChatCommandListener.DismissPeerUnpairedNotice();
            }
            IconGlyph.HelpMarker("Clears who you're paired with on your own client only - doesn't touch your former Sub's plugin at all.");
        }
        else if (!pairing.IsPaired)
        {
            IconGlyph.WrappedColored(Theme.TextMuted, "Not paired");
            IconGlyph.WrappedDisabled("Send or accept a relay invitation from Settings when you're ready.");
        }
        else if (config.Role == PluginRole.Owner)
        {
            IconGlyph.WrappedColored(Theme.Success, $"Owns: {pairing.PeerName}@{pairing.PeerWorld}");
            if (ImGui.Button("Release pairing"))
                plugin.PairingService.ReleasePeer();
            IconGlyph.HelpMarker("Clears who you're paired with on your own client only - doesn't touch your Sub's plugin at all. Use this to fix a stale/wrong pairing or to free them up to pair with someone else.");
        }
        else
        {
            IconGlyph.WrappedColored(Theme.Success, $"Owned by: {pairing.PeerName}@{pairing.PeerWorld}");
            IconGlyph.WrappedDisabled("Locked until you use /oathboundpanic.");
            if (peerUnpairedNotice is { } subNotice)
            {
                var peerLabel = subNotice.PeerRole == PluginRole.Owner ? "your Owner's" : "your Sub's";
                IconGlyph.WrappedColored(Theme.Warning, $"Note: {peerLabel} side ended pairing via panic. You're still paired and locked until you use /oathboundpanic - this doesn't change that.");
                if (ImGui.SmallButton("Dismiss##peerUnpairedNotice"))
                    plugin.ChatCommandListener.DismissPeerUnpairedNotice();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        IconGlyph.Text(FontAwesomeIcon.ShieldAlt, "Safeword");
        SafewordEditor.Draw(config, "mainHeader", ref revealSafeword);
        IconGlyph.HelpMarker("This only configures the typed /oathboundpanic command; editing it never triggers panic or changes pairing.");
        ImGui.Spacing();
        ImGui.EndTable();
        ImGui.PopID();
    }

    private void DrawPermissionsCard()
    {
        var permissions = plugin.Configuration.Permissions;
        IconGlyph.Text(FontAwesomeIcon.ShieldAlt, "Permissions");
        ImGui.Separator();
        IconGlyph.WrappedDisabled("What you'll accept from a paired Owner while you're set to Sub - each category is independent.");

        if (ImGuiCheckbox("Title", permissions.Title, out var newTitle))
            SavePermission(() => permissions.Title = newTitle);
        IconGlyph.HelpMarker("Lets a paired Owner apply or clear your Honorific title via a trigger tell.");

        if (ImGuiCheckbox("Outfit / Wardrobe", permissions.Outfit, out var newOutfit))
            SavePermission(() => permissions.Outfit = newOutfit);
        IconGlyph.HelpMarker("Lets a paired Owner apply or unlock a Glamourer design via a trigger tell.");

        ImGui.Spacing();
        var config = plugin.Configuration;
        if (!config.TosAcknowledged)
            IconGlyph.WrappedColored(Theme.Warning, "Gesture/Follow/Restraints require the ToS acknowledgement in Settings (gear icon) first.");

        using (ImRaii.Disabled(!config.TosAcknowledged))
        {
            if (ImGuiCheckbox("Gesture", permissions.Gesture, out var newGesture))
                SavePermission(() => permissions.Gesture = newGesture);
            IconGlyph.HelpMarker("Lets a paired Owner temporarily enable a selected animation mod and immediately play its tied gesture. Disable this permission at any time to reject commands.");

            if (ImGuiCheckbox("Follow / Leash (hardcore)", permissions.Follow, out var newFollow))
                SavePermission(() => permissions.Follow = newFollow);
            IconGlyph.HelpMarker("Lets a paired Owner lock your movement to follow them, blocking your own WASD input until released. Heavier automation footprint than the other three - see the README's Automation risk section.");

            if (ImGuiCheckbox("Restraints", permissions.Restraints, out var newRestraints))
                SavePermission(() => permissions.Restraints = newRestraints);
            IconGlyph.HelpMarker("Lets a paired Owner apply or release a restraint device via a trigger tell. Restraint devices can suppress movement, force walking, block actions, garble your outgoing chat (Gagged), or hold you in a chosen animation (Arms/Legs/Full Body Cuffed) while active - Gagged rewrites content you actually typed, a heavier automation footprint than the others - see the Restraints tab and the README's Automation risk section.");
        }

        ImGui.Spacing();
        if (ImGuiCheckbox("Collar", permissions.Collar, out var newCollar))
            SavePermission(() => permissions.Collar = newCollar);
        IconGlyph.HelpMarker("Lets your configured collar item apply and lock automatically when you accept a pairing (Collar tab). Configuring an item alone does nothing without this enabled too.");

        if (ImGuiCheckbox("Moodles", permissions.Moodles, out var newMoodles))
            SavePermission(() => permissions.Moodles = newMoodles);
        IconGlyph.HelpMarker("Lets a paired Owner apply or clear a Moodle (status effect) from your own registered statuses via a trigger tell - applies immediately, no confirmation queue.");

        ImGui.Spacing();
        if (ImGuiCheckbox("Catalog sync (relay)", permissions.RelayCatalogSync, out var newRelayCatalogSync))
            SavePermission(() => permissions.RelayCatalogSync = newRelayCatalogSync);
        IconGlyph.HelpMarker("Lets your paired Owner request an automatic, end-to-end encrypted refresh of your exported catalog (at most once every four hours) instead of you sending a file manually. The relay never sees the plaintext. Off by default; manual file export/import always remains available regardless of this setting.");

        ImGui.Spacing();
        if (!config.CustomChatAcknowledged)
            IconGlyph.WrappedColored(Theme.Warning, "Custom chat messages require their own dedicated acknowledgement in Settings (gear icon) first - separate from the general ToS checkbox above.");

        using (ImRaii.Disabled(!config.CustomChatAcknowledged))
        {
            if (ImGuiCheckbox("Custom chat messages", permissions.CustomChatMessages, out var newCustomChat))
                SavePermission(() => permissions.CustomChatMessages = newCustomChat);
            IconGlyph.HelpMarker("Lets a Custom Trigger's chat action send arbitrary text to any channel (including public chat) as your own character. A materially broader automation surface than Gesture's closed set of self-targeting commands - see the README's Automation risk section.");
        }
    }

    private void DrawTitleModule()
    {
        var config = plugin.Configuration;
        IconGlyph.Text(FontAwesomeIcon.Heading, "Title Aliases");
        ImGui.Separator();

        DrawClearAliasField("Clear-title alias", () => config.Aliases.ClearTitleAlias, v => config.Aliases.ClearTitleAlias = v, config);
        IconGlyph.HelpMarker("The alias that removes your current Honorific title when triggered - separate from the named aliases below, which each apply a specific title.");

        ImGui.Spacing();
        var titles = config.Aliases.Titles;
        for (var i = 0; i < titles.Count; i++)
        {
            ImGui.PushID($"title_{i}");
            var t = titles[i];
            ImGui.BulletText($"{t.Alias} -> \"{t.Text}\" ({(t.IsPrefix ? "prefix" : "suffix")})");
            ImGui.SameLine();
            if (ImGui.SmallButton("Remove"))
            {
                titles.RemoveAt(i);
                config.Save();
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }

        ImGui.Spacing();
        ImGui.InputText("Alias##newTitle", ref newTitleAlias, 32);
        IconGlyph.HelpMarker("Short word the Owner types after the trigger phrase to apply this title, e.g. \"command goodgirl\".");
        ImGui.InputText("Title text##newTitle", ref newTitleText, 64);
        IconGlyph.HelpMarker("The exact title text applied via Honorific.");
        ImGui.Checkbox("Prefix (not suffix)##newTitle", ref newTitleIsPrefix);
        IconGlyph.HelpMarker("Show the title before your name instead of after it.");
        ImGui.ColorEdit3("Color##newTitle", ref newTitleColor);
        IconGlyph.HelpMarker("Honorific title color.");
        DrawReservedWordWarning(newTitleAlias);
        if (ImGui.Button("Add title alias") && newTitleAlias.Length > 0 && newTitleText.Length > 0 && !IsReserved(newTitleAlias))
        {
            titles.Add(new TitleAliasDefinition { Alias = newTitleAlias, Text = newTitleText, IsPrefix = newTitleIsPrefix, Color = newTitleColor });
            config.Save();
            newTitleAlias = "";
            newTitleText = "";
        }
    }

    private void DrawWardrobeModule()
    {
        var config = plugin.Configuration;
        IconGlyph.Text(FontAwesomeIcon.Tshirt, "Wardrobe");
        ImGui.Separator();
        IconGlyph.WrappedDisabled("Design folder allowlist and scanning live in Settings (gear icon). Define your outfit aliases below.");

        IconGlyph.WrappedDisabled("Wardrobe release is always `unlock`. For example: kae unlock. This releases the currently locked Glamourer outfit.");

        ImGui.Spacing();
        var outfits = config.Aliases.Outfits;
        for (var i = 0; i < outfits.Count; i++)
        {
            ImGui.PushID($"outfit_{i}");
            var o = outfits[i];
            ImGui.BulletText($"{o.Alias} -> {o.DesignName} ({(o.Locked ? "locked" : "unlocked")})");
            ImGui.SameLine();
            if (ImGui.SmallButton("Remove"))
            {
                outfits.RemoveAt(i);
                config.Save();
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }

        ImGui.Spacing();
        var designs = config.WardrobeMapping.LocalDesigns.Values.ToList();
        if (designs.Count == 0)
        {
            IconGlyph.WrappedDisabled("No scanned designs yet - rescan in Settings (gear icon) first.");
            return;
        }

        var designNames = designs.Select(d => d.Name).ToArray();
        newOutfitDesignIndex = Math.Clamp(newOutfitDesignIndex, 0, designNames.Length - 1);
        ImGui.InputText("Alias##newOutfit", ref newOutfitAlias, 32);
        IconGlyph.HelpMarker("Short word the Owner types after the trigger phrase to apply this outfit.");
        ImGui.Combo("Design##newOutfit", ref newOutfitDesignIndex, designNames, designNames.Length);
        IconGlyph.HelpMarker("Which scanned Glamourer design this alias applies - any design inside your allowlisted folders (Settings) is fair game, no separate approval step. Rescan in Settings if the one you want isn't listed.");
        ImGui.Checkbox("Lock##newOutfit", ref newOutfitLocked);
        IconGlyph.HelpMarker("Lock the design's own equipment slots after applying, so only this plugin's release action can change them - every other slot stays freely editable.");
        DrawReservedWordWarning(newOutfitAlias);
        if (ImGui.Button("Add outfit alias") && newOutfitAlias.Length > 0 && !IsReserved(newOutfitAlias))
        {
            var design = designs[newOutfitDesignIndex];
            outfits.Add(new OutfitAliasDefinition
            {
                Alias = newOutfitAlias,
                DesignId = design.DesignId,
                DesignName = design.Name,
                Locked = newOutfitLocked,
            });
            config.Save();
            newOutfitAlias = "";
        }
    }

    /// collar/restraints: captures a single equipped gear piece (Wrists, Body, etc. - any of the 10
    /// lockable slots) directly from what's currently equipped, the same capture mechanism CollarState uses
    /// for the collar item, as a named restraint device carrying restriction rules - no scan/design library
    /// involved. Then create Sub-alias entries that toggle them (RestraintCommand.Toggle).
    private void DrawRestraintsModule()
    {
        var config = plugin.Configuration;
        IconGlyph.Text(FontAwesomeIcon.Handcuffs, "Restraints");
        ImGui.Separator();
        IconGlyph.WrappedDisabled("Pick a slot and an item below to capture it as a named restraint device carrying restriction rules, then alias it so an Owner - or your own alias - can apply/release it. The item doesn't need to be equipped or owned.");
        IconGlyph.WrappedDisabled("Owner force-release is always `restraint unlock`. Individual restraint aliases toggle their own device on and off.");

        var devices = config.RestraintMapping.Devices.Values.ToList();
        if (devices.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextUnformatted("Captured devices");
            foreach (var device in devices)
            {
                ImGui.PushID($"device_{device.Id}");
                var active = plugin.RestraintCommand.IsActive(device.Id);
                var ruleSummary = string.Join(" · ", device.Rules.Select(CommandPresentation.Rule));
                ImGui.TextUnformatted($"{device.Name}{(active ? "  • Active" : "")}");
                ImGui.Indent();
                IconGlyph.WrappedDisabled($"{device.Slot} · {GetItemName(device.ItemId)}");
                IconGlyph.WrappedDisabled(ruleSummary);
                var staleAnimation = device.Rules.Any(r =>
                    r.Kind is RestraintRuleKind.ArmsCuffed or RestraintRuleKind.LegsCuffed or RestraintRuleKind.FullBodyCuffed
                    && (string.IsNullOrWhiteSpace(r.AnimationId) || !config.GestureMapping.LocalCatalog.ContainsKey(r.AnimationId)));
                if (staleAnimation)
                    IconGlyph.WrappedColored(Theme.Warning, "A cuff animation is stale. Choose Edit and select the animation again before using this restraint.");
                ImGui.Unindent();
                ImGui.SameLine();
                if (ImGui.SmallButton("Edit"))
                {
                    LoadDeviceDraft(device);
                    ImGui.PopID();
                    break;
                }
                ImGui.SameLine();
                if (ImGui.SmallButton("Remove"))
                {
                    plugin.RestraintCommand.RemoveDevice(device.Id);
                    ImGui.PopID();
                    break;
                }
                ImGui.PopID();
            }
        }

        ImGui.Spacing();
        ImGui.TextUnformatted(editingDeviceId is null ? "Capture a new device" : "Edit captured device");
        var slotNames = LockableEquipSlots.All.Select(s => s.ToString()).ToArray();
        newDeviceSlotIndex = Math.Clamp(newDeviceSlotIndex, 0, slotNames.Length - 1);
        ImGui.InputText("Alias##newDevice", ref newDeviceName, 32);
        IconGlyph.HelpMarker("The command name your Owner uses for this restraint. For example, an alias of \"armcuffs\" is applied with \"restraint lock armcuffs\".");
        ImGui.Combo("Slot##newDevice", ref newDeviceSlotIndex, slotNames, slotNames.Length);
        IconGlyph.HelpMarker("Which equipment slot this device occupies. Applying it locks only this one slot, the same way a locked Outfit alias locks only the slots its design touches.");

        var newDeviceChosenLabel = newDeviceItemId is { } id ? GetItemName(id) : "(none chosen)";
        ImGui.TextUnformatted($"Item: {newDeviceChosenLabel}");
        ImGui.SameLine();
        if (ImGui.SmallButton("Choose item...##newDevice"))
        {
            var slot = LockableEquipSlots.All[newDeviceSlotIndex];
            plugin.ItemPickerWindow.Open(slot, (chosenId, _) => newDeviceItemId = chosenId);
        }
        IconGlyph.HelpMarker("Pick any item valid for the chosen slot - it does not need to be equipped or owned.");

        DrawRestraintRuleCheckboxes(newDeviceRuleEdit, "newDevice");

        var hasAnyRule = HasAnyRule(newDeviceRuleEdit);
        var boundAnimationsConfigured = BoundAnimationsConfigured(newDeviceRuleEdit);
        if (hasAnyRule && !boundAnimationsConfigured)
            IconGlyph.WrappedColored(Theme.Warning, "Choose an animation for every checked Arms/Legs/Full Body Cuffed rule before capturing.");

        var duplicateDeviceName = devices.Any(d => d.Id != editingDeviceId &&
            string.Equals(d.Name, newDeviceName.Trim(), StringComparison.OrdinalIgnoreCase));
        var prospectiveRules = ToRules(newDeviceRuleEdit);
        var safeDeviceCommand = CommandSelector.Fits(RestraintCommand.BuildLockCommand(newDeviceName.Trim(), prospectiveRules));
        if (duplicateDeviceName)
            IconGlyph.WrappedColored(Theme.Warning, "A restraint device already uses this name.");
        if (!safeDeviceCommand)
            IconGlyph.WrappedColored(Theme.Warning, "This restraint name and rule set are too long for a safe command.");
        using (ImRaii.Disabled(newDeviceName.Trim().Length == 0 || newDeviceItemId is null || !hasAnyRule || !boundAnimationsConfigured || duplicateDeviceName || !safeDeviceCommand))
        {
            if (ImGui.Button(editingDeviceId is null ? "Capture device" : "Save device") && newDeviceItemId is { } chosenItemId)
            {
                var slot = LockableEquipSlots.All[newDeviceSlotIndex];
                var rules = ToRules(newDeviceRuleEdit);

                var saved = editingDeviceId is null
                    ? plugin.RestraintCommand.CaptureDeviceFromItem(slot, chosenItemId, newDeviceName, rules)
                    : SaveDeviceDraft(slot, chosenItemId, rules);
                if (saved)
                {
                    ResetDeviceDraft();
                }
            }
        }
        if (editingDeviceId is not null)
        {
            ImGui.SameLine();
            if (ImGui.Button("Cancel##editDevice"))
                ResetDeviceDraft();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("Aliases");
        var aliases = config.Aliases.Restraints;
        for (var i = 0; i < aliases.Count; i++)
        {
            ImGui.PushID($"restraintAlias_{i}");
            var a = aliases[i];
            ImGui.BulletText($"{a.Alias} -> {a.DeviceName} (toggles)");
            ImGui.SameLine();
            if (ImGui.SmallButton("Remove"))
            {
                aliases.RemoveAt(i);
                config.Save();
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }

        if (devices.Count > 0)
        {
            ImGui.Spacing();
            var deviceNames = devices.Select(d => d.Name).ToArray();
            newRestraintDeviceIndex = Math.Clamp(newRestraintDeviceIndex, 0, deviceNames.Length - 1);
            ImGui.InputText("Alias##newRestraint", ref newRestraintAlias, 32);
            IconGlyph.HelpMarker("Short word that toggles this device: applies it if inactive, releases it if active.");
            ImGui.Combo("Device##newRestraint", ref newRestraintDeviceIndex, deviceNames, deviceNames.Length);
            DrawReservedWordWarning(newRestraintAlias);
            if (ImGui.Button("Add restraint alias") && newRestraintAlias.Length > 0 && !IsReserved(newRestraintAlias))
            {
                var device = devices[newRestraintDeviceIndex];
                aliases.Add(new RestraintAliasDefinition { Alias = newRestraintAlias, DeviceId = device.Id, DeviceName = device.Name });
                config.Save();
                newRestraintAlias = "";
            }
        }
    }

    private static string PoseName(int poseModeId) => poseModeId is >= 1 and <= 3 ? PoseNames[poseModeId - 1] : "unknown";

    private static bool HasAnyRule(RestraintRuleEditState edit) =>
        edit.ForcedPose || edit.WalkOnly || edit.ActionBlock || edit.GagChat || edit.ArmsCuffed || edit.LegsCuffed || edit.FullBodyCuffed;

    private bool BoundAnimationsConfigured(RestraintRuleEditState edit)
    {
        bool Contains(string id) => plugin.Configuration.Role == PluginRole.Owner
            ? plugin.Configuration.GestureMapping.ImportedPeerCatalog.ContainsKey(id)
            : plugin.Configuration.GestureMapping.LocalCatalog.ContainsKey(id);
        bool Valid(bool enabled, string? id) => !enabled || id is not null && Contains(id);
        return Valid(edit.ArmsCuffed, edit.ArmsCuffedAnimationId)
            && Valid(edit.LegsCuffed, edit.LegsCuffedAnimationId)
            && Valid(edit.FullBodyCuffed, edit.FullBodyCuffedAnimationId);
    }

    /// collar/ui-organization "Restraint rule checkboxes are laid out two per row": shared by the Sub's
    /// device-capture editor, the Owner's per-quick-command editor, and the Owner's ad-hoc device editor -
    /// one `ImGui.Columns(2)` block per row keeps each checkbox's own dependent controls (pose combo,
    /// bound-animation picker) attached underneath it within its own column, regardless of how tall the
    /// other column's content is.
    private void DrawRestraintRuleCheckboxes(RestraintRuleEditState edit, string idSuffix)
    {
        ImGui.Columns(2, $"restraintRules_{idSuffix}_row1", false);
        ImGui.Checkbox($"Forced pose##{idSuffix}", ref edit.ForcedPose);
        IconGlyph.HelpMarker("Places you into the chosen pose and fully blocks movement input until released.");
        if (edit.ForcedPose)
            ImGui.Combo($"Pose##{idSuffix}", ref edit.PoseIndex, PoseNames, PoseNames.Length);
        ImGui.NextColumn();
        ImGui.Checkbox($"Walk-only##{idSuffix}", ref edit.WalkOnly);
        IconGlyph.HelpMarker("Forces walking and blocks running, without blocking directional movement input.");
        ImGui.Columns(1);

        ImGui.Columns(2, $"restraintRules_{idSuffix}_row2", false);
        ImGui.Checkbox($"Action block##{idSuffix}", ref edit.ActionBlock);
        IconGlyph.HelpMarker("Blocks hotbar action/skill usage until released, without affecting movement.");
        ImGui.NextColumn();
        ImGui.Checkbox($"Gagged##{idSuffix}", ref edit.GagChat);
        IconGlyph.HelpMarker("Garbles your outgoing chat text - the actual transmitted message, not just your own display - until released. See the README's Automation risk section before enabling.");
        ImGui.Columns(1);

        ImGui.Columns(2, $"restraintRules_{idSuffix}_row3", false);
        DrawBoundAnimationPicker("Arms Cuffed", ref edit.ArmsCuffed, edit.ArmsCuffedAnimationId, id => edit.ArmsCuffedAnimationId = id, $"{idSuffix}Arms");
        IconGlyph.HelpMarker("Temporarily activates the chosen animation and holds you in it until released, without affecting movement or actions.");
        ImGui.NextColumn();
        DrawBoundAnimationPicker("Legs Cuffed", ref edit.LegsCuffed, edit.LegsCuffedAnimationId, id => edit.LegsCuffedAnimationId = id, $"{idSuffix}Legs");
        IconGlyph.HelpMarker("Temporarily activates the chosen animation and holds you in it until released, without affecting movement or actions.");
        ImGui.Columns(1);

        ImGui.Columns(2, $"restraintRules_{idSuffix}_row4", false);
        DrawBoundAnimationPicker("Full Body Cuffed", ref edit.FullBodyCuffed, edit.FullBodyCuffedAnimationId, id => edit.FullBodyCuffedAnimationId = id, $"{idSuffix}FullBody");
        IconGlyph.HelpMarker("Temporarily activates the chosen animation and holds you in it, and fully blocks movement input, until released - a fully custom-animation counterpart to forced pose.");
        ImGui.NextColumn();
        ImGui.Columns(1);
    }

    /// collar/restraints "Arms Cuffed and Legs Cuffed rules...": a checkbox plus the same searchable
    /// animation picker `collar/gesture`'s "Add animation..." button already opens (AnimationPickerWindow),
    /// reused here for the Sub's device-capture UI and the Owner's per-quick-command rule editor alike.
    /// `onChosen` writes back to whatever field/property backs `currentAnimationId` - a plain delegate
    /// rather than `ref string?`, since the picker's selection callback fires on a later frame and a ref
    /// parameter can't be captured by that closure.
    private void DrawBoundAnimationPicker(string label, ref bool enabled, string? currentAnimationId, Action<string> onChosen, string idSuffix)
    {
        ImGui.Checkbox($"{label}##{idSuffix}", ref enabled);
        if (!enabled)
            return;

        ImGui.Indent();
        var ownerMode = plugin.Configuration.Role == PluginRole.Owner;
        var localCatalog = plugin.Configuration.GestureMapping.LocalCatalog;
        var peerCatalog = plugin.Configuration.GestureMapping.ImportedPeerCatalog;
        var chosenLabel = "(none chosen)";
        var chosenMode = "";
        if (currentAnimationId is { } id)
        {
            if (ownerMode && peerCatalog.TryGetValue(id, out var peer))
            {
                chosenLabel = peer.AnimationName;
                chosenMode = peer.Trigger is null ? "Enable option only" : peer.Trigger.DisplayName;
            }
            else if (!ownerMode && localCatalog.TryGetValue(id, out var local))
            {
                chosenLabel = local.AnimationName;
                chosenMode = local.Trigger is null ? "Enable option only" : local.Trigger.DisplayName;
            }
            else
            {
                chosenLabel = "(missing or stale)";
            }
        }

        // Keep the action reachable at narrow widths: the full catalog label used to consume the row and
        // push Choose outside the column. The button gets its own line; readable detail is shortened and
        // wraps below it, with the complete option name available on hover.
        if (ImGui.SmallButton($"{(currentAnimationId is null ? "Choose animation..." : "Change animation...")}##{idSuffix}"))
        {
            if (ownerMode) plugin.AnimationPickerWindow.OpenImportedForRestraint(chosen => onChosen(chosen.Id));
            else plugin.AnimationPickerWindow.OpenForRestraint(chosen => onChosen(chosen.Id));
        }
        var shortLabel = CommandPresentation.CompactAnimation(chosenLabel);
        ImGui.TextUnformatted(shortLabel);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(chosenLabel);
        if (chosenMode.Length > 0)
            IconGlyph.WrappedDisabled(chosenMode);
        ImGui.Unindent();
    }


    private void DrawGestureModule()
    {
        var config = plugin.Configuration;
        IconGlyph.Text(FontAwesomeIcon.TheaterMasks, "Gesture");
        ImGui.Separator();
        IconGlyph.WrappedDisabled("Select animation mods and scan them in Settings, then define aliases from their named options here.");

        var gestures = config.Aliases.Gestures;
        var removeGestureIndex = -1;
        if (gestures.Count > 0 && ImGui.BeginTable("gestureAliases", 2,
                ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH))
        {
            ImGui.TableSetupColumn("Animation", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 72);
            for (var i = 0; i < gestures.Count; i++)
            {
                ImGui.PushID($"gesture_{i}");
                var g = gestures[i];
                var invalid = string.IsNullOrEmpty(g.GestureId) || !config.GestureMapping.LocalCatalog.ContainsKey(g.GestureId);

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.TextWrapped($"{g.Alias} → {(g.AnimationName.Length > 0 ? g.AnimationName : g.EmoteName)} ({g.ModName}){(invalid ? " — rescan/recreate required" : "")}");

                ImGui.TableSetColumnIndex(1);
                if (ImGui.SmallButton("Remove")) removeGestureIndex = i;
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        if (removeGestureIndex >= 0)
        {
            gestures.RemoveAt(removeGestureIndex);
            config.Save();
        }

        ImGui.Spacing();
        var allOptions = config.GestureMapping.LocalCatalog.Values.Where(e => e.Trigger != null).ToList();
        if (allOptions.Count == 0)
        {
            IconGlyph.WrappedDisabled("No scanned/resolved gestures yet - rescan in Settings (gear icon) first.");
            return;
        }

        ImGui.InputText("Alias##newGesture", ref newGestureAlias, 32);
        IconGlyph.HelpMarker("Short word the Owner types. With Gesture permission enabled, this immediately enables the chosen animation temporarily and plays its tied trigger.");
        DrawReservedWordWarning(newGestureAlias);

        if (ImGui.Button(selectedAliasGesture is null ? "Add animation..." : "Change animation..."))
            plugin.AnimationPickerWindow.Open(entry => selectedAliasGesture = entry);

        var canAdd = newGestureAlias.Length > 0 && !IsReserved(newGestureAlias) && selectedAliasGesture is not null;
        using (ImRaii.Disabled(!canAdd))
        {
            if (ImGui.Button("Add gesture alias") && selectedAliasGesture is { } chosen)
            {
                gestures.Add(new GestureAliasDefinition
                {
                    Alias = newGestureAlias,
                    GestureId = chosen.Id,
                    AnimationName = chosen.AnimationName,
                    ModDirectory = chosen.ModDirectory,
                    ModName = chosen.ModName,
                    EmoteName = chosen.Trigger!.DisplayName.TrimStart('/'),
                });
                config.Save();
                newGestureAlias = "";
                selectedAliasGesture = null;
            }
        }
        if (selectedAliasGesture is { } picked)
        {
            ImGui.SameLine();
            IconGlyph.WrappedDisabled($"Selected: {picked.Label}");
        }

        ImGui.Spacing();
        ImGui.Separator();
        using (ImRaii.Disabled(!plugin.GestureCommand.HasActiveTemporary))
        {
            if (ImGui.Button("Reset active gesture"))
                plugin.GestureCommand.ResetActiveTemporary();
        }
        IconGlyph.HelpMarker("Reverts the currently active temporary mod activation back to its saved settings right now, instead of waiting for the automatic ~30s idle-timeout. Only enabled while a gesture's temporary activation is active.");
    }

    /// collar/moodles "Sub can self-apply or self-clear a Moodle via alias": mirrors DrawGestureModule's
    /// exact shape (a list of defined aliases with Remove, a dedicated clear alias, an "add" form picking
    /// from the Sub's own scanned catalog) - the picking control here is a plain combo rather than a
    /// dedicated picker window since Moodles has no equivalent of AnimationPickerWindow/ItemPickerWindow.
    private void DrawMoodlesModule()
    {
        var config = plugin.Configuration;
        IconGlyph.Text(FontAwesomeIcon.Smile, "Moodles Aliases");
        ImGui.Separator();
        IconGlyph.WrappedDisabled("Scan your own registered Moodles statuses in Settings, then define aliases from them here.");

        DrawClearAliasField("Clear-moodle alias", () => config.Aliases.ClearMoodleAlias, v => config.Aliases.ClearMoodleAlias = v, config);
        IconGlyph.HelpMarker("The alias that removes your currently active Moodle when triggered - separate from the named aliases below, which each apply a specific status.");

        ImGui.Spacing();
        var moodleAliases = config.Aliases.Moodles;
        for (var i = 0; i < moodleAliases.Count; i++)
        {
            ImGui.PushID($"moodle_{i}");
            var m = moodleAliases[i];
            var invalid = string.IsNullOrEmpty(m.StatusId) || !config.MoodlesMapping.LocalCatalog.ContainsKey(m.StatusId);
            ImGui.BulletText($"{m.Alias} -> {MoodlesTextFormat.StripMarkup(m.StatusName)}{(invalid ? " — rescan/recreate required" : "")}");
            ImGui.SameLine();
            if (ImGui.SmallButton("Remove"))
            {
                moodleAliases.RemoveAt(i);
                config.Save();
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }

        ImGui.Spacing();
        var statuses = config.MoodlesMapping.LocalCatalog.Values.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
        if (statuses.Count == 0)
        {
            IconGlyph.WrappedDisabled("No scanned Moodles statuses yet - rescan in Settings (gear icon) first.");
            return;
        }

        ImGui.InputText("Alias##newMoodle", ref newMoodleAlias, 32);
        IconGlyph.HelpMarker("Short word the Owner types. With Moodles permission enabled, this immediately applies the chosen status.");
        DrawReservedWordWarning(newMoodleAlias);

        var statusNames = statuses.Select(s => MoodlesTextFormat.StripMarkup(s.Name)).ToArray();
        newMoodleStatusIndex = Math.Clamp(newMoodleStatusIndex, 0, statusNames.Length - 1);
        ImGui.Combo("Status##newMoodle", ref newMoodleStatusIndex, statusNames, statusNames.Length);

        if (ImGui.Button("Add Moodle alias") && newMoodleAlias.Length > 0 && !IsReserved(newMoodleAlias))
        {
            var chosen = statuses[newMoodleStatusIndex];
            moodleAliases.Add(new MoodlesAliasDefinition { Alias = newMoodleAlias, StatusId = chosen.StatusId, StatusName = chosen.Name });
            config.Save();
            newMoodleAlias = "";
        }
    }

    /// collar/custom-triggers "Sub can define a multi-action Custom Trigger": builds one CustomTriggerAction
    /// at a time into a draft list (reusing each category's own existing picker - AnimationPickerWindow for
    /// Gesture, WardrobeMapping/RestraintMapping-backed combos for Outfit/Restraint matching
    /// DrawWardrobeModule/DrawRestraintsModule, a plain combo for Moodle matching DrawMoodlesModule, and a
    /// raw text box for Chat since design.md rules out any content/channel validation beyond the permission
    /// gate itself), then commits alias+actions together on "Save trigger". Each bundled action still checks
    /// its own category's permission at apply time (CustomTriggerCommand.Apply) - this UI doesn't duplicate
    /// those checks, it only builds the definition.
    private void DrawCustomTriggersModule()
    {
        var config = plugin.Configuration;
        IconGlyph.Text(FontAwesomeIcon.BoltLightning, "Custom Triggers");
        ImGui.Separator();
        IconGlyph.WrappedDisabled("Bundle multiple actions - title, outfit, gesture, moodle, restraint, chat - behind one alias. Each action still needs its own category permission (Chat also needs the dedicated Custom chat messages acknowledgement in Settings) or it's skipped when the trigger fires.");

        var triggers = config.Aliases.CustomTriggers;
        for (var i = 0; i < triggers.Count; i++)
        {
            ImGui.PushID($"customTrigger_{i}");
            var t = triggers[i];
            ImGui.TextUnformatted(t.Alias);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(t.Alias);
            ImGui.Indent();
            foreach (var action in t.Actions)
                DrawActionSummary(action);
            ImGui.Unindent();
            ImGui.SameLine();
            if (ImGui.SmallButton("Edit"))
            {
                editingCustomTriggerIndex = i;
                ctNewAlias = t.Alias;
                ctDraftActions.Clear();
                ctDraftActions.AddRange(t.Actions.Select(CloneAction));
                ImGui.PopID();
                break;
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("Remove"))
            {
                triggers.RemoveAt(i);
                config.Save();
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted(editingCustomTriggerIndex is null ? "New trigger" : "Edit trigger");
        ImGui.InputText("Alias##newCustomTrigger", ref ctNewAlias, 32);
        IconGlyph.HelpMarker("Short word the Owner types. Applies every permitted action below in order when triggered.");
        DrawReservedWordWarning(ctNewAlias);

        if (ctDraftActions.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextUnformatted("Actions in this trigger");
            for (var i = 0; i < ctDraftActions.Count; i++)
            {
                ImGui.PushID($"ctDraftAction_{i}");
                DrawActionSummary(ctDraftActions[i]);
                ImGui.SameLine();
                using (ImRaii.Disabled(i == 0))
                    if (ImGui.SmallButton("↑"))
                        MoveDraftAction(ctDraftActions, i, i - 1, ref editingCustomTriggerActionIndex);
                ImGui.SameLine();
                using (ImRaii.Disabled(i == ctDraftActions.Count - 1))
                    if (ImGui.SmallButton("↓"))
                        MoveDraftAction(ctDraftActions, i, i + 1, ref editingCustomTriggerActionIndex);
                ImGui.SameLine();
                if (ImGui.SmallButton("Edit"))
                {
                    LoadSubActionDraft(ctDraftActions[i]);
                    editingCustomTriggerActionIndex = i;
                }
                ImGui.SameLine();
                if (ImGui.SmallButton("Remove"))
                {
                    RemoveDraftAction(ctDraftActions, i, ref editingCustomTriggerActionIndex);
                    ImGui.PopID();
                    break;
                }
                ImGui.PopID();
            }
        }

        ImGui.Spacing();
        var kindNames = Enum.GetNames<CustomTriggerActionKind>();
        ctNewActionKindIndex = Math.Clamp(ctNewActionKindIndex, 0, kindNames.Length - 1);
        ImGui.Combo("Add action##newCtKind", ref ctNewActionKindIndex, kindNames, kindNames.Length);
        var kind = Enum.Parse<CustomTriggerActionKind>(kindNames[ctNewActionKindIndex]);
        if (editingCustomTriggerActionIndex is not null)
            IconGlyph.WrappedColored(Theme.Accent, "Editing this action. Change its values below, then choose Save action.");

        switch (kind)
        {
            case CustomTriggerActionKind.Title:
                ImGui.InputText("Text##newCtTitle", ref ctTitleText, 64);
                ImGui.Checkbox("Prefix##newCtTitle", ref ctTitleIsPrefix);
                ImGui.ColorEdit3("Color##newCtTitle", ref ctTitleColor);
                using (ImRaii.Disabled(ctTitleText.Length == 0))
                {
                    if (ImGui.Button($"{(editingCustomTriggerActionIndex is null ? "Add action" : "Save action")}##newCtTitleBtn"))
                    {
                        CommitSubAction(new CustomTriggerAction { Kind = CustomTriggerActionKind.Title, TitleText = ctTitleText, TitleIsPrefix = ctTitleIsPrefix, TitleColor = ctTitleColor });
                        ctTitleText = "";
                        ctTitleIsPrefix = false;
                        ctTitleColor = new Vector3(1, 1, 1);
                    }
                }
                break;

            case CustomTriggerActionKind.Outfit:
                var designs = config.WardrobeMapping.LocalDesigns.Values.ToList();
                if (designs.Count == 0)
                {
                    IconGlyph.WrappedDisabled("No scanned designs yet - rescan in Settings (gear icon) first.");
                    break;
                }
                var designNames = designs.Select(d => d.Name).ToArray();
                ctOutfitDesignIndex = Math.Clamp(ctOutfitDesignIndex, 0, designNames.Length - 1);
                ImGui.Combo("Design##newCtOutfit", ref ctOutfitDesignIndex, designNames, designNames.Length);
                if (ImGui.Button($"{(editingCustomTriggerActionIndex is null ? "Add action" : "Save action")}##newCtOutfitBtn"))
                {
                    var design = designs[ctOutfitDesignIndex];
                    CommitSubAction(new CustomTriggerAction { Kind = CustomTriggerActionKind.Outfit, OutfitDesignId = design.DesignId, OutfitDesignName = design.Name });
                }
                break;

            case CustomTriggerActionKind.Gesture:
                if (ImGui.Button(ctSelectedGesture is null ? "Choose animation...##newCtGesture" : $"Change animation... ({ctSelectedGesture.Label})##newCtGesture"))
                    plugin.AnimationPickerWindow.Open(entry => ctSelectedGesture = entry);
                using (ImRaii.Disabled(ctSelectedGesture is null))
                {
                    if (ImGui.Button($"{(editingCustomTriggerActionIndex is null ? "Add action" : "Save action")}##newCtGestureBtn") && ctSelectedGesture is { } chosenGesture)
                    {
                        CommitSubAction(new CustomTriggerAction { Kind = CustomTriggerActionKind.Gesture, GestureId = chosenGesture.Id, GestureAnimationName = chosenGesture.AnimationName });
                        ctSelectedGesture = null;
                    }
                }
                break;

            case CustomTriggerActionKind.Moodle:
                var statuses = config.MoodlesMapping.LocalCatalog.Values.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
                if (statuses.Count == 0)
                {
                    IconGlyph.WrappedDisabled("No scanned Moodles statuses yet - rescan in Settings (gear icon) first.");
                    break;
                }
                var statusNames = statuses.Select(s => MoodlesTextFormat.StripMarkup(s.Name)).ToArray();
                ctMoodleStatusIndex = Math.Clamp(ctMoodleStatusIndex, 0, statusNames.Length - 1);
                ImGui.Combo("Status##newCtMoodle", ref ctMoodleStatusIndex, statusNames, statusNames.Length);
                if (ImGui.Button($"{(editingCustomTriggerActionIndex is null ? "Add action" : "Save action")}##newCtMoodleBtn"))
                {
                    var status = statuses[ctMoodleStatusIndex];
                    CommitSubAction(new CustomTriggerAction { Kind = CustomTriggerActionKind.Moodle, MoodleStatusId = status.StatusId, MoodleStatusName = status.Name });
                }
                break;

            case CustomTriggerActionKind.Restraint:
                var devices = config.RestraintMapping.Devices.Values.ToList();
                if (devices.Count == 0)
                {
                    IconGlyph.WrappedDisabled("No captured restraint devices yet - capture one in the Restraints tab first.");
                    break;
                }
                var deviceNames = devices.Select(d => d.Name).ToArray();
                ctRestraintDeviceIndex = Math.Clamp(ctRestraintDeviceIndex, 0, deviceNames.Length - 1);
                ImGui.Combo("Device##newCtRestraint", ref ctRestraintDeviceIndex, deviceNames, deviceNames.Length);
                IconGlyph.HelpMarker("Toggles this device when the trigger fires - applies if inactive, releases if active, same as a plain restraint alias.");
                if (ImGui.Button($"{(editingCustomTriggerActionIndex is null ? "Add action" : "Save action")}##newCtRestraintBtn"))
                {
                    var device = devices[ctRestraintDeviceIndex];
                    CommitSubAction(new CustomTriggerAction { Kind = CustomTriggerActionKind.Restraint, RestraintDeviceId = device.Id, RestraintDeviceName = device.Name });
                }
                break;

            case CustomTriggerActionKind.Chat:
                ImGui.InputText("Message##newCtChat", ref ctChatText, 400);
                IconGlyph.HelpMarker("Sent exactly as typed, unmodified - start it with a slash command (e.g. /sit) or a channel prefix (e.g. /p) to use those instead of your default chat channel. Needs the Custom chat messages permission and its own acknowledgement in Settings - see the README's Automation risk section.");
                using (ImRaii.Disabled(ctChatText.Trim().Length == 0))
                {
                    if (ImGui.Button($"{(editingCustomTriggerActionIndex is null ? "Add action" : "Save action")}##newCtChatBtn"))
                    {
                        CommitSubAction(new CustomTriggerAction { Kind = CustomTriggerActionKind.Chat, ChatText = ctChatText });
                        ctChatText = "";
                    }
                }
                break;
        }

        ImGui.Spacing();
        ImGui.Separator();
        var duplicateAlias = triggers.Where((_, i) => i != editingCustomTriggerIndex)
            .Any(t => string.Equals(t.Alias, ctNewAlias.Trim(), StringComparison.OrdinalIgnoreCase));
        if (duplicateAlias)
            IconGlyph.WrappedColored(Theme.Warning, "A custom trigger already uses this alias.");
        using (ImRaii.Disabled(ctNewAlias.Trim().Length == 0 || IsReserved(ctNewAlias) || ctDraftActions.Count == 0 || duplicateAlias))
        {
            if (ImGui.Button(editingCustomTriggerIndex is null ? "Save trigger" : "Save changes"))
            {
                var replacement = new CustomTriggerDefinition { Alias = ctNewAlias.Trim(), Actions = ctDraftActions.Select(CloneAction).ToList() };
                if (editingCustomTriggerIndex is { } editIndex && editIndex >= 0 && editIndex < triggers.Count)
                    triggers[editIndex] = replacement;
                else
                    triggers.Add(replacement);
                config.Save();
                ctNewAlias = "";
                ctDraftActions.Clear();
                editingCustomTriggerIndex = null;
                editingCustomTriggerActionIndex = null;
            }
        }
        if (editingCustomTriggerIndex is not null)
        {
            ImGui.SameLine();
            if (ImGui.Button("Cancel##editCustomTrigger"))
            {
                ctNewAlias = "";
                ctDraftActions.Clear();
                editingCustomTriggerIndex = null;
                editingCustomTriggerActionIndex = null;
            }
        }
    }

    private static string SummarizeCustomTriggerAction(CustomTriggerAction a) => CommandPresentation.Action(a);

    private static void DrawActionSummary(CustomTriggerAction action)
    {
        var summary = SummarizeCustomTriggerAction(action);
        ImGui.Bullet();
        ImGui.SameLine();
        ImGui.TextWrapped(summary);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(summary);
    }

    private static CustomTriggerAction CloneAction(CustomTriggerAction action) => new()
    {
        Kind = action.Kind,
        TitleText = action.TitleText,
        TitleIsPrefix = action.TitleIsPrefix,
        TitleColor = action.TitleColor,
        OutfitDesignId = action.OutfitDesignId,
        OutfitDesignName = action.OutfitDesignName,
        GestureId = action.GestureId,
        GestureAnimationName = action.GestureAnimationName,
        MoodleStatusId = action.MoodleStatusId,
        MoodleStatusName = action.MoodleStatusName,
        RestraintDeviceId = action.RestraintDeviceId,
        RestraintDeviceName = action.RestraintDeviceName,
        ChatText = action.ChatText,
    };

    private void CommitSubAction(CustomTriggerAction action)
    {
        if (editingCustomTriggerActionIndex is { } index && index >= 0 && index < ctDraftActions.Count)
            ctDraftActions[index] = action;
        else
            ctDraftActions.Add(action);
        editingCustomTriggerActionIndex = null;
    }

    private void CommitOwnerAction(CustomTriggerAction action)
    {
        if (editingOwnerActionIndex is { } index && index >= 0 && index < ctqDraftActions.Count)
            ctqDraftActions[index] = action;
        else
            ctqDraftActions.Add(action);
        editingOwnerActionIndex = null;
    }

    private static void MoveDraftAction(List<CustomTriggerAction> actions, int from, int to, ref int? editingIndex)
    {
        (actions[from], actions[to]) = (actions[to], actions[from]);
        if (editingIndex == from) editingIndex = to;
        else if (editingIndex == to) editingIndex = from;
    }

    private static void RemoveDraftAction(List<CustomTriggerAction> actions, int index, ref int? editingIndex)
    {
        actions.RemoveAt(index);
        if (editingIndex == index) editingIndex = null;
        else if (editingIndex > index) editingIndex--;
    }

    private void LoadSubActionDraft(CustomTriggerAction action)
    {
        ctNewActionKindIndex = (int)action.Kind;
        ctTitleText = action.TitleText;
        ctTitleIsPrefix = action.TitleIsPrefix;
        ctTitleColor = action.TitleColor;
        var designs = plugin.Configuration.WardrobeMapping.LocalDesigns.Values.ToList();
        ctOutfitDesignIndex = Math.Max(0, designs.FindIndex(d => d.DesignId == action.OutfitDesignId));
        ctSelectedGesture = plugin.Configuration.GestureMapping.LocalCatalog.Values
            .FirstOrDefault(g => string.Equals(g.Id, action.GestureId, StringComparison.OrdinalIgnoreCase));
        var statuses = plugin.Configuration.MoodlesMapping.LocalCatalog.Values.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
        ctMoodleStatusIndex = Math.Max(0, statuses.FindIndex(s => s.StatusId == action.MoodleStatusId));
        var devices = plugin.Configuration.RestraintMapping.Devices.Values.ToList();
        ctRestraintDeviceIndex = Math.Max(0, devices.FindIndex(d => d.Id == action.RestraintDeviceId));
        ctChatText = action.ChatText;
    }

    private void LoadOwnerActionDraft(CustomTriggerAction action)
    {
        ctqKindIndex = (int)action.Kind;
        ctqTitleText = action.TitleText;
        ctqTitleIsPrefix = action.TitleIsPrefix;
        ctqTitleColor = action.TitleColor;
        ctqOutfitName = action.OutfitDesignName;
        ctqGestureName = action.GestureAnimationName;
        ctqMoodleName = MoodlesTextFormat.StripMarkup(action.MoodleStatusName);
        ctqRestraintName = action.RestraintDeviceName;
        ctqChatText = action.ChatText;
    }

    private void LoadDeviceDraft(RestraintDeviceDefinition device)
    {
        editingDeviceId = device.Id;
        newDeviceName = device.Name;
        newDeviceSlotIndex = Math.Max(0, LockableEquipSlots.All.ToList().IndexOf(device.Slot));
        newDeviceItemId = device.ItemId;
        CopyRuleEdit(FromRules(device.Rules), newDeviceRuleEdit);
    }

    private bool SaveDeviceDraft(ApiEquipSlot slot, ulong itemId, List<RestraintRuleAssignment> rules)
    {
        if (editingDeviceId is not { } id || !plugin.Configuration.RestraintMapping.Devices.TryGetValue(id, out var device))
            return false;

        var oldName = device.Name;
        device.Name = newDeviceName.Trim();
        device.Slot = slot;
        device.ItemId = itemId;
        device.Rules = rules;
        foreach (var alias in plugin.Configuration.Aliases.Restraints.Where(a => a.DeviceId == id))
            alias.DeviceName = device.Name;
        plugin.Configuration.Save();
        Plugin.Log.Debug($"Edited restraint device '{oldName}' while preserving id {id}.");
        return true;
    }

    private void ResetDeviceDraft()
    {
        editingDeviceId = null;
        newDeviceName = "";
        newDeviceItemId = null;
        CopyRuleEdit(new RestraintRuleEditState(), newDeviceRuleEdit);
    }

    private static void CopyRuleEdit(RestraintRuleEditState source, RestraintRuleEditState target)
    {
        target.ForcedPose = source.ForcedPose;
        target.PoseIndex = source.PoseIndex;
        target.WalkOnly = source.WalkOnly;
        target.ActionBlock = source.ActionBlock;
        target.GagChat = source.GagChat;
        target.ArmsCuffed = source.ArmsCuffed;
        target.ArmsCuffedAnimationId = source.ArmsCuffedAnimationId;
        target.LegsCuffed = source.LegsCuffed;
        target.LegsCuffedAnimationId = source.LegsCuffedAnimationId;
        target.FullBodyCuffed = source.FullBodyCuffed;
        target.FullBodyCuffedAnimationId = source.FullBodyCuffedAnimationId;
    }

    /// collar/collaring: the Sub's own configured Neck-slot collar. Capture-only (see design.md's "Collar
    /// capture, not manual entry") - equip what you want first, then click Capture. Locked once applied at
    /// pairing acceptance (AcceptPending), not from this tab - editing/clearing is disabled while locked,
    /// matching "Sub configures their own collar item" and "resists casual removal" together.
    private void DrawCollarModule()
    {
        var config = plugin.Configuration;
        var locked = plugin.RuntimeState.CollarForceLocked;
        IconGlyph.Text(FontAwesomeIcon.Lock, "Collar");
        ImGui.Separator();
        IconGlyph.WrappedDisabled("Equip the item you want in your Neck slot (any way you like), then capture it here. Applied and locked automatically the moment you accept a pairing - not from this tab.");

        if (config.Collar.IsConfigured)
            ImGui.TextUnformatted($"Configured collar: {GetItemName(config.Collar.ItemId!.Value)}");
        else
            IconGlyph.WrappedDisabled("No collar configured yet.");

        if (locked)
        {
            IconGlyph.WrappedColored(Theme.Danger, "Locked - applied at pairing. Only /oathboundpanic (your safeword) or your Owner's \"collar unlock\" releases it.");
        }

        var collarChosenLabel = config.Collar.ItemId is { } collarItemId ? GetItemName(collarItemId) : "(none chosen)";
        using (ImRaii.Disabled(locked))
        {
            ImGui.TextUnformatted($"Item: {collarChosenLabel}");
            ImGui.SameLine();
            if (ImGui.Button("Choose item...##collar"))
                plugin.ItemPickerWindow.Open(ApiEquipSlot.Neck, (chosenId, _) => plugin.CollarCommand.ConfigureFromItem(chosenId));
            IconGlyph.HelpMarker("Pick any Neck-slot item to save as your collar - it does not need to be equipped or owned.");

            if (config.Collar.IsConfigured)
            {
                ImGui.SameLine();
                if (ImGui.Button("Clear"))
                    plugin.CollarCommand.ClearConfiguredCollar();
            }

            ImGui.Spacing();
            var collarMoodleLabel = config.Collar.MoodleStatusName is { } assignedMoodleName ? MoodlesTextFormat.StripMarkup(assignedMoodleName) : "(none assigned)";
            ImGui.TextUnformatted($"Moodle: {collarMoodleLabel}");
            IconGlyph.HelpMarker("Optional. Applied alongside your collar item when it locks, and periodically re-asserted for as long as the collar stays locked - removing it through Moodles' own UI won't make it stick. Cleared only by /oathboundpanic or your Owner's \"collar unlock\", the same as the collar item itself.");

            var collarMoodleStatuses = config.MoodlesMapping.LocalCatalog.Values.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
            if (collarMoodleStatuses.Count == 0)
            {
                IconGlyph.WrappedDisabled("No scanned Moodles statuses yet - rescan in Settings (gear icon) first.");
            }
            else
            {
                var collarMoodleStatusNames = collarMoodleStatuses.Select(s => MoodlesTextFormat.StripMarkup(s.Name)).ToArray();
                newCollarMoodleStatusIndex = Math.Clamp(newCollarMoodleStatusIndex, 0, collarMoodleStatusNames.Length - 1);
                ImGui.SetNextItemWidth(220);
                ImGui.Combo("##newCollarMoodle", ref newCollarMoodleStatusIndex, collarMoodleStatusNames, collarMoodleStatusNames.Length);
                ImGui.SameLine();
                if (ImGui.SmallButton("Assign##collarMoodle"))
                {
                    var chosen = collarMoodleStatuses[newCollarMoodleStatusIndex];
                    config.Collar.MoodleStatusId = chosen.StatusId;
                    config.Collar.MoodleStatusName = chosen.Name;
                    config.Save();
                }
            }

            if (config.Collar.HasMoodleAssigned)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton("Clear##collarMoodle"))
                {
                    config.Collar.MoodleStatusId = null;
                    config.Collar.MoodleStatusName = null;
                    config.Save();
                }
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        IconGlyph.Text(FontAwesomeIcon.Link, "Leash triggers");
        IconGlyph.WrappedDisabled("The words your Owner sends to engage or release the movement lock. Defaults: leash / unleash.");
        var follow = config.Aliases.Follow;
        var engage = follow.EngageAlias;
        if (ImGui.InputText("Leash", ref engage, 32))
        {
            follow.EngageAlias = engage.Trim();
            config.Save();
        }
        IconGlyph.HelpMarker("Engages follow and blocks your own movement while the Follow / Leash permission is enabled.");

        var release = follow.ReleaseAlias;
        if (ImGui.InputText("Unleash", ref release, 32))
        {
            follow.ReleaseAlias = release.Trim();
            config.Save();
        }
        IconGlyph.HelpMarker("Releases the movement lock and restores normal input.");
    }

    /// Best-effort display name for a raw Glamourer item id via Lumina's own Item sheet - falls back to
    /// the numeric id for sentinel/special values (e.g. "nothing equipped") that don't resolve to a real
    /// row, so a lookup miss never crashes the picker's chosen-item label.
    private static string GetItemName(ulong itemId)
    {
        var row = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault((uint)itemId);
        var name = row?.Name.ExtractText();
        return string.IsNullOrWhiteSpace(name) ? $"Item #{itemId}" : name;
    }

    /// The Owner-facing tab. Title/Outfit/Gesture/Moodles/Restraints each get a one-click QuickCommand
    /// list - Outfit/Gesture/Moodles/Restraints are populated in one action by the "Import commands" button
    /// at the top (collar/catalog-sync - a Sub-exported catalog file fills every one of them at once);
    /// Title is built one at a time since there's nothing to bulk-import for freeform text. Every button
    /// offers Send (ChatSender - one click, one /tell, disabled until pairing has captured a peer to
    /// address it to) alongside Copy (always available). The freeform box at the bottom covers a plain
    /// alias or a one-off not worth saving.
    private void DrawOwnerModule()
    {
        IconGlyph.Text(FontAwesomeIcon.Crown, "Owner - commands");
        ImGui.Separator();

        DrawImportCommandsButton();
        ImGui.Spacing();
        DrawCatalogRelaySection();
        ImGui.Spacing();

        var pairing = plugin.Configuration.Pairing;
        var canSend = pairing.IsPaired;
        if (!canSend)
            IconGlyph.WrappedColored(Theme.Warning, "No /tell target yet - Send is disabled until pairing captures your Sub's name (Settings' handshake), or is re-enabled after a panic/unpair. Copy still works any time.");

        var quick = plugin.Configuration.QuickCommands;
        DrawOwnerSection($"Title ({quick.Titles.Count} saved)##ownerTitle", () => DrawTitleQuickSection(canSend), defaultOpen: true);
        DrawOwnerSection($"Outfit ({quick.Outfits.Count} imported)##ownerOutfit", () => DrawOutfitQuickSection(canSend));
        DrawOwnerSection($"Gesture ({quick.Gestures.Count} imported)##ownerGesture", () => DrawGestureQuickSection(canSend));
        DrawOwnerSection($"Leash ({quick.Follow.Count} saved)##ownerLeash", () => DrawFollowQuickSection(canSend));
        DrawOwnerSection("Collar (2 actions)##ownerCollar", () => DrawCollarQuickSection(canSend));
        DrawOwnerSection($"Moodles ({quick.Moodles.Count} imported)##ownerMoodles", () => DrawMoodlesQuickSection(canSend));
        DrawOwnerSection($"Restraints ({quick.Restraints.Count} imported)##ownerRestraints", () => DrawRestraintQuickSection(canSend));
        DrawOwnerSection("Custom Trigger (ad-hoc)##ownerCustomTrigger", () => DrawCustomTriggerQuickSection(canSend), forceOpen: editingOwnerBundle is not null);
        DrawOwnerSection($"Custom Trigger Bundles / one-off ({quick.Aliases.Count} saved)##ownerAlias", () => DrawFreeformComposer(canSend));
    }

    /// collar/catalog-sync: the single Owner-side entry point that replaces the three former per-category
    /// "Add from clipboard" buttons - opens a native file picker for a Sub-exported catalog file and fills
    /// every category's quick-command list from it in one action (CatalogSyncService.ParseImport).
    private void DrawImportCommandsButton()
    {
        const string importLabel = "Import commands";
        const string resetLabel = "Reset imports";
        var importWidth = ImGui.CalcTextSize(importLabel).X + ImGui.GetStyle().FramePadding.X * 2f;
        var resetWidth = ImGui.CalcTextSize(resetLabel).X + ImGui.GetStyle().FramePadding.X * 2f;
        var totalWidth = importWidth + ImGui.GetStyle().ItemSpacing.X + resetWidth;
        var avail = ImGui.GetContentRegionAvail().X;
        if (avail > totalWidth)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (avail - totalWidth) / 2f);

        if (ImGui.Button(importLabel))
        {
            plugin.FileDialogManager.OpenFileDialog("Import Collar catalog", ".txt", (ok, path) =>
            {
                if (!ok)
                    return;
                try
                {
                    var text = System.IO.File.ReadAllText(path);
                    var result = plugin.CatalogSyncService.ParseImport(text);
                    var duplicateNote = result.Duplicates > 0 ? $" {result.Duplicates} duplicate(s) skipped." : "";
                    importResult = result.Error ?? (result.TotalAdded == 0
                        ? $"Nothing new - everything in that file was already imported or a duplicate.{duplicateNote}"
                        : $"Imported {result.TotalAdded} new command(s): {result.Title} title, {result.Wardrobe} outfit, {result.Gesture} gesture, {result.Moodles} moodles, {result.Restraints} restraint, {result.Bundles} bundle.{duplicateNote}");
                    resetImportsResult = null;
                }
                catch (Exception ex)
                {
                    importResult = $"Import failed: {ex.Message}";
                }
            });
        }

        ImGui.SameLine();

        /// collar/catalog-sync "Owner can reset every import to a blank slate": removes only the
        /// `Imported`-sourced entries from Title/Outfit/Gesture/Moodles/Restraints - those lists now mix
        /// import-sourced entries with the Owner's own manually-added/scanned ones (single-action aliases
        /// route into these same category lists now - see collar/catalog-sync), so a coarse `.Clear()`
        /// would also wipe out entries reset-imports was never meant to touch. The Custom Trigger Bundle
        /// list still shares one list between imported bundles and anything the Owner typed manually into
        /// the freeform composer, so it keeps the old coarse whole-list clear - the same reset already
        /// accepted for Restraints' manually-added entries (see collar/catalog-sync's spec).
        if (ImGui.Button(resetLabel))
        {
            var quick = plugin.Configuration.QuickCommands;
            RemoveImportedEntries(quick.Titles);
            RemoveImportedEntries(quick.Outfits);
            RemoveImportedEntries(quick.Gestures);
            RemoveImportedEntries(quick.Moodles);
            RemoveImportedEntries(quick.Restraints);
            quick.Aliases.Clear();
            plugin.Configuration.Save();
            expandedRestraintRuleEditors.Clear();
            restraintRuleEdits.Clear();
            resetImportsResult = "All imports reset to a blank slate.";
            importResult = null;
        }
        IconGlyph.HelpMarker("Clears every import-sourced quick command (Title, Outfit, Gesture, Moodles, Restraints) back out, leaving anything you added or scanned yourself in those same lists untouched, and clears the entire Custom Trigger Bundle list - including any one-off commands you typed by hand, since imported bundles share that same list.");

        if (importResult is not null)
        {
            var isError = importResult.StartsWith("Import failed", StringComparison.Ordinal) || importResult.Contains("doesn't look like", StringComparison.Ordinal) || importResult.Contains("is empty", StringComparison.Ordinal);
            IconGlyph.WrappedColored(isError ? Theme.Danger : Theme.Success, importResult);
        }
        if (resetImportsResult is not null)
            IconGlyph.WrappedColored(Theme.Success, resetImportsResult);
    }

    private static void RemoveImportedEntries(List<QuickCommand> list) =>
        list.RemoveAll(cmd => cmd.Source == ImportSource.Imported);

    /// collar/catalog-sync "Owner refresh controls": shows current phase, last successful snapshot/counts,
    /// next allowed time, and actionable failure text; the button itself is disabled during an active
    /// request or cooldown so it can never be double-clicked into a second one (task 7.3).
    private void DrawCatalogRelaySection()
    {
        var relayService = plugin.CatalogSyncRelayService;
        var pairing = plugin.Configuration.Pairing;
        IconGlyph.Text(FontAwesomeIcon.CloudDownloadAlt, "Automatic sync (relay)");
        if (!pairing.IsPaired)
        {
            IconGlyph.WrappedDisabled("Not paired - use manual Import above, or pair from Settings first.");
            return;
        }

        var cooldown = relayService.CooldownRemaining;
        using (ImRaii.Disabled(relayService.RequestInFlight || cooldown is not null))
        {
            if (ImGui.Button(relayService.RequestInFlight ? "Requesting..." : "Request refresh"))
                Plugin.FireAndForget(relayService.RequestRefreshAsync(System.Threading.CancellationToken.None));
        }
        IconGlyph.HelpMarker("Asks your paired Sub for a fresh, end-to-end encrypted catalog snapshot instead of a manually transferred file - at most once every four hours, enforced by both sides.");

        IconGlyph.WrappedDisabled($"Status: {relayService.Phase}.");

        if (cooldown is { } remaining)
            IconGlyph.WrappedDisabled($"Next refresh available in {(remaining.TotalHours >= 1 ? $"{(int)remaining.TotalHours}h {remaining.Minutes}m" : $"{remaining.Minutes}m")}.");
        else if (pairing.LastAcceptedCatalogSyncUnixSeconds > 0)
            IconGlyph.WrappedDisabled("Refresh is available now.");

        if (pairing.LastAcceptedCatalogSyncUnixSeconds > 0)
        {
            var lastSuccess = DateTimeOffset.FromUnixTimeSeconds(pairing.LastAcceptedCatalogSyncUnixSeconds).LocalDateTime;
            IconGlyph.WrappedDisabled($"Last successful sync: {lastSuccess:g} (snapshot #{pairing.LastImportedSnapshotId}).");
        }

        if (relayService.LastAttemptAt is { } attempted)
            IconGlyph.WrappedDisabled($"Last relay response: {attempted.LocalDateTime:g}.");

        if (relayService.LastError is { Length: > 0 } lastError)
            IconGlyph.WrappedColored(Theme.Danger, lastError);
        else if (relayService.LastImportResult is { Error: null } result && result.Added + result.Updated + result.Removed > 0)
            IconGlyph.WrappedColored(Theme.Success, $"Last sync: {result.Added} added, {result.Updated} updated, {result.Removed} removed.");
    }

    /// collar/ui-organization: draws a section's icon+title, then (if `showClearAll`) a "Clear all" button
    /// right-aligned on that same row - no prior "title ... [button]" row existed anywhere in this UI, so
    /// this is the one shared right-alignment routine every quick-command section now uses (design.md
    /// decision #3), based on the same GetContentRegionAvail math DrawImportCommandsButton already used to
    /// center its own button. Wraps to its own line instead of being clipped when the window is too narrow
    /// for the button to fit next to the title.
    private static void DrawSectionTitleRow(FontAwesomeIcon icon, string title, bool showClearAll, string idSuffix, Action onClearAll)
    {
        IconGlyph.Text(icon, title);
        if (!showClearAll)
            return;

        const string label = "Clear all";
        var buttonWidth = ButtonWidth(label);
        ImGui.SameLine();
        var avail = ImGui.GetContentRegionAvail().X;
        if (avail < buttonWidth)
            ImGui.NewLine();
        else if (avail > buttonWidth)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - buttonWidth);
        if (ImGui.SmallButton($"{label}##{idSuffix}"))
            onClearAll();
    }

    /// The rendered width of a button carrying this visible text, ignoring any "##id" suffix - used to
    /// decide whether the next control in a row still fits before wrapping (see ContinueRowOrWrap).
    private static float ButtonWidth(string visibleLabel) =>
        ImGui.CalcTextSize(visibleLabel).X + ImGui.GetStyle().FramePadding.X * 2f;

    /// collar/ui-organization: keeps a horizontal row of buttons from being clipped off-window when it's
    /// too narrow to fit them all - continues the row with SameLine() when the next control still fits,
    /// otherwise wraps it onto a fresh line (ImGui.NewLine() undoes the SameLine positioning it just did).
    private static void ContinueRowOrWrap(float nextControlWidth)
    {
        ImGui.SameLine();
        if (ImGui.GetContentRegionAvail().X < nextControlWidth)
            ImGui.NewLine();
    }

    private static void DrawOwnerSection(string label, Action draw, bool defaultOpen = false, bool forceOpen = false)
    {
        if (forceOpen)
            ImGui.SetNextItemOpen(true, ImGuiCond.Always);
        var flags = defaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
        if (ImGui.CollapsingHeader(label, flags))
        {
            ImGui.Indent();
            draw();
            ImGui.Unindent();
        }
        ImGui.Spacing();
    }

    /// Collar only ever has one override verb - `collar unlock` - since the collar itself only ever
    /// applies as a side effect of pairing acceptance, never through a chat command (see
    /// ChatCommandListener.HandleForceCollar). No "Add Command" builder needed, just the fixed release row.
    private void DrawCollarQuickSection(bool canSend)
    {
        IconGlyph.Text(FontAwesomeIcon.Lock, "Collar");
        DrawFixedQuickRow("Collar lock", "collar lock", canSend);
        IconGlyph.HelpMarker("(Re-)attaches your Sub's configured collar item and locks it - the same thing that happens automatically at pairing, triggered manually. Use this to re-lock after \"Collar unlock,\" or to apply it for the first time if it wasn't configured/enabled yet when pairing was accepted.");

        DrawFixedQuickRow("Collar unlock", "collar unlock", canSend);
        IconGlyph.HelpMarker("Releases your Sub's locked collar without them needing to panic - it stays equipped, just no longer locked.");
    }

    private void DrawMoodlesQuickSection(bool canSend)
    {
        var quick = plugin.Configuration.QuickCommands.Moodles;
        DrawSectionTitleRow(FontAwesomeIcon.TheaterMasks, "Moodles", quick.Count > 0, "moodlesQuick", () =>
        {
            quick.Clear();
            plugin.Configuration.Save();
        });

        DrawFixedQuickRow("Clear moodle", "moodle clear", canSend);

        if (quick.Count == 0)
        {
            IconGlyph.WrappedDisabled("No Moodles statuses imported yet - use \"Import commands\" above.");
            return;
        }

        using var _ = ImRaii.Child("moodlesQuickList", new Vector2(0, 120), true);
        foreach (var cmd in quick.ToArray())
            DrawSavedQuickRow(cmd, quick, canSend, MoodlesTextFormat.StripMarkup);
    }

    /// collar/restraints: Owner-tab quick commands - no per-category import button here (collar/catalog-
    /// sync's unified "Import commands" is the only way to populate this list, or the Owner can add one
    /// manually below (collar/restraints "Owner can add a restraint quick command by name") by typing a
    /// device name the Sub told them, the same freeform pattern DrawTitleQuickSection uses. Every entry
    /// starts with no rules assigned, so each row exposes a "Configure rules" editor - the same rule set
    /// DrawRestraintsModule's device-capture section uses - and Send/Copy stay disabled until at least one
    /// rule is assigned (collar/restraints "Owner quick command with no rules assigned yet").
    private void DrawRestraintQuickSection(bool canSend)
    {
        var quick = plugin.Configuration.QuickCommands.Restraints;
        DrawSectionTitleRow(FontAwesomeIcon.Handcuffs, "Restraints", quick.Count > 0, "restraintsQuick", () =>
        {
            quick.Clear();
            plugin.Configuration.Save();
        });

        ImGui.SetNextItemWidth(220);
        ImGui.InputText("##newQuickRestraint", ref newRestraintQuickText, 32);
        ImGui.SameLine();
        if (ImGui.SmallButton("Add Command##quickRestraint") && newRestraintQuickText.Trim().Length > 0)
        {
            var name = newRestraintQuickText.Trim();
            quick.Add(new QuickCommand { Label = name, Command = $"restraint lock \"{name}\"" });
            plugin.Configuration.Save();
            newRestraintQuickText = "";
        }
        IconGlyph.HelpMarker("Adds a restraint quick command by name - type the exact device name your Sub told you. Configure its rules below before it can be sent.");

        ImGui.Spacing();
        DrawAdHocRestraintSection(canSend);
        ImGui.Spacing();

        DrawFixedQuickRow("Restraint unlock", "restraint unlock", canSend);
        IconGlyph.HelpMarker("Force-releases every active restraint device and clears the force-lock, the same as your Sub's panic would for restraints specifically.");

        if (quick.Count == 0)
        {
            IconGlyph.WrappedDisabled("No restraint devices imported yet - use \"Import commands\" above.");
            return;
        }

        using var _ = ImRaii.Child("restraintsQuickList", new Vector2(0, 260), true);
        foreach (var cmd in quick.ToArray())
            DrawRestraintQuickRow(cmd, quick, canSend);
    }

    /// collar/restraints "Owner-authored ad-hoc restraint device": lets the Owner pick a slot and item
    /// directly via `ItemPickerWindow` and assign rules, with no Sub-side captured device to reference by
    /// name. Sends via the `restraint wear` grammar (RestraintCommand.BuildWearCommand) rather than being
    /// added to the name-based `quick` list, since its full definition already travels in the command text.
    private void DrawAdHocRestraintSection(bool canSend)
    {
        IconGlyph.WrappedDisabled("Or define a device's gear directly - no need to know what your Sub named anything.");

        var slotNames = LockableEquipSlots.All.Select(s => s.ToString()).ToArray();
        newAdHocSlotIndex = Math.Clamp(newAdHocSlotIndex, 0, slotNames.Length - 1);
        ImGui.SetNextItemWidth(160);
        ImGui.Combo("Slot##adHocRestraint", ref newAdHocSlotIndex, slotNames, slotNames.Length);

        var chosenLabel = newAdHocItemId is { } id ? GetItemName(id) : "(none chosen)";
        ImGui.TextUnformatted($"Item: {chosenLabel}");
        ImGui.SameLine();
        if (ImGui.SmallButton("Choose item...##adHocRestraint"))
        {
            var slot = LockableEquipSlots.All[newAdHocSlotIndex];
            plugin.ItemPickerWindow.Open(slot, (chosenId, _) => newAdHocItemId = chosenId);
        }

        ImGui.SetNextItemWidth(220);
        ImGui.InputText("Label##adHocRestraint", ref newAdHocLabel, 32);
        IconGlyph.HelpMarker("Your own reference name for this device - never matched against anything on your Sub's side.");

        DrawRestraintRuleCheckboxes(newAdHocRuleEdit, "adHocRestraint");

        var hasAnyRule = HasAnyRule(newAdHocRuleEdit);
        var boundAnimationsConfigured = BoundAnimationsConfigured(newAdHocRuleEdit);
        if (hasAnyRule && !boundAnimationsConfigured)
            IconGlyph.WrappedColored(Theme.Warning, "Choose an animation for every checked Arms/Legs/Full Body Cuffed rule before sending.");

        var ready = newAdHocItemId is not null && newAdHocLabel.Trim().Length > 0 && hasAnyRule && boundAnimationsConfigured;
        if (ready && newAdHocItemId is { } readyItemId)
        {
            var slot = LockableEquipSlots.All[newAdHocSlotIndex];
            var command = RestraintCommand.BuildWearCommand(slot, readyItemId, newAdHocLabel.Trim(), ToRules(newAdHocRuleEdit));
            ImGui.TextUnformatted("Send this ad-hoc device:");
            ContinueRowOrWrap(ButtonWidth("Send"));
            DrawSendCopyButtons(command, canSend, "adHocRestraint");
        }
        else
        {
            IconGlyph.WrappedColored(Theme.Warning, "Choose a slot, an item, a label, and at least one rule before this can be sent.");
        }
    }

    /// collar/custom-triggers "custom commands should also be creatable via the Owner commands menu":
    /// builds an ad-hoc, unnamed bundle one action at a time (mirroring DrawAdHocRestraintSection's
    /// draft-then-send shape) and sends it via `CustomTriggerCommand.BuildCastCommand`'s `customtrigger
    /// cast` wire grammar. Unlike the Sub-side DrawCustomTriggersModule, every non-Title/Chat action here
    /// is typed by name only, never picked from a local catalog - the Owner's own install has no access to
    /// the Sub's WardrobeMapping/GestureMapping/MoodlesMapping/RestraintMapping, only the Sub does, so this
    /// matches the existing "type the exact name your Sub told you" freeform pattern (e.g.
    /// DrawRestraintQuickSection's manual add) rather than the Sub-tab's picker-based one.
    private void DrawCustomTriggerQuickSection(bool canSend)
    {
        IconGlyph.WrappedDisabled("Bundle actions together by name - no dedicated alias needed on your Sub's side. Type each name exactly as your Sub told you; Title and Chat need no name at all.");

        ImGui.SetNextItemWidth(220);
        ImGui.InputText("Label##ctqLabel", ref ctqLabel, 32);
        IconGlyph.HelpMarker("Your own reference name for this bundle - never matched against anything on your Sub's side.");

        if (ctqDraftActions.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextUnformatted("Actions in this bundle");
            for (var i = 0; i < ctqDraftActions.Count; i++)
            {
                ImGui.PushID($"ctqDraftAction_{i}");
                DrawActionSummary(ctqDraftActions[i]);
                ImGui.SameLine();
                using (ImRaii.Disabled(i == 0))
                    if (ImGui.SmallButton("↑"))
                        MoveDraftAction(ctqDraftActions, i, i - 1, ref editingOwnerActionIndex);
                ImGui.SameLine();
                using (ImRaii.Disabled(i == ctqDraftActions.Count - 1))
                    if (ImGui.SmallButton("↓"))
                        MoveDraftAction(ctqDraftActions, i, i + 1, ref editingOwnerActionIndex);
                ImGui.SameLine();
                if (ImGui.SmallButton("Edit"))
                {
                    LoadOwnerActionDraft(ctqDraftActions[i]);
                    editingOwnerActionIndex = i;
                }
                ImGui.SameLine();
                if (ImGui.SmallButton("Remove"))
                {
                    RemoveDraftAction(ctqDraftActions, i, ref editingOwnerActionIndex);
                    ImGui.PopID();
                    break;
                }
                ImGui.PopID();
            }
        }

        ImGui.Spacing();
        var kindNames = Enum.GetNames<CustomTriggerActionKind>();
        ctqKindIndex = Math.Clamp(ctqKindIndex, 0, kindNames.Length - 1);
        ImGui.SetNextItemWidth(160);
        ImGui.Combo("Add action##ctqKind", ref ctqKindIndex, kindNames, kindNames.Length);
        var kind = Enum.Parse<CustomTriggerActionKind>(kindNames[ctqKindIndex]);
        if (editingOwnerActionIndex is not null)
            IconGlyph.WrappedColored(Theme.Accent, "Editing this action. Change its values below, then choose Save.");

        switch (kind)
        {
            case CustomTriggerActionKind.Title:
                ImGui.SetNextItemWidth(220);
                ImGui.InputText("Text##ctqTitle", ref ctqTitleText, 64);
                ImGui.Checkbox("Prefix##ctqTitle", ref ctqTitleIsPrefix);
                ImGui.ColorEdit3("Color##ctqTitle", ref ctqTitleColor);
                using (ImRaii.Disabled(ctqTitleText.Trim().Length == 0))
                {
                    if (ImGui.SmallButton($"{(editingOwnerActionIndex is null ? "Add" : "Save")}##ctqTitleBtn"))
                    {
                        CommitOwnerAction(new CustomTriggerAction { Kind = CustomTriggerActionKind.Title, TitleText = ctqTitleText.Trim(), TitleIsPrefix = ctqTitleIsPrefix, TitleColor = ctqTitleColor });
                        ctqTitleText = "";
                        ctqTitleIsPrefix = false;
                        ctqTitleColor = new Vector3(1, 1, 1);
                    }
                }
                break;

            case CustomTriggerActionKind.Outfit:
                ImGui.SetNextItemWidth(220);
                ImGui.InputText("Design name##ctqOutfit", ref ctqOutfitName, 32);
                IconGlyph.HelpMarker("Type the exact wardrobe design name your Sub told you.");
                using (ImRaii.Disabled(ctqOutfitName.Trim().Length == 0))
                {
                    if (ImGui.SmallButton($"{(editingOwnerActionIndex is null ? "Add" : "Save")}##ctqOutfitBtn"))
                    {
                        CommitOwnerAction(new CustomTriggerAction { Kind = CustomTriggerActionKind.Outfit, OutfitDesignName = ctqOutfitName.Trim() });
                        ctqOutfitName = "";
                    }
                }
                break;

            case CustomTriggerActionKind.Gesture:
                ImGui.SetNextItemWidth(220);
                ImGui.InputText("Gesture name##ctqGesture", ref ctqGestureName, 32);
                IconGlyph.HelpMarker("Type the exact gesture/animation name your Sub told you.");
                using (ImRaii.Disabled(ctqGestureName.Trim().Length == 0))
                {
                    if (ImGui.SmallButton($"{(editingOwnerActionIndex is null ? "Add" : "Save")}##ctqGestureBtn"))
                    {
                        CommitOwnerAction(new CustomTriggerAction { Kind = CustomTriggerActionKind.Gesture, GestureAnimationName = ctqGestureName.Trim() });
                        ctqGestureName = "";
                    }
                }
                break;

            case CustomTriggerActionKind.Moodle:
                ImGui.SetNextItemWidth(220);
                ImGui.InputText("Status name##ctqMoodle", ref ctqMoodleName, 32);
                IconGlyph.HelpMarker("Type the exact Moodles status name your Sub told you.");
                using (ImRaii.Disabled(ctqMoodleName.Trim().Length == 0))
                {
                    if (ImGui.SmallButton($"{(editingOwnerActionIndex is null ? "Add" : "Save")}##ctqMoodleBtn"))
                    {
                        CommitOwnerAction(new CustomTriggerAction { Kind = CustomTriggerActionKind.Moodle, MoodleStatusName = ctqMoodleName.Trim() });
                        ctqMoodleName = "";
                    }
                }
                break;

            case CustomTriggerActionKind.Restraint:
                ImGui.SetNextItemWidth(220);
                ImGui.InputText("Device name##ctqRestraint", ref ctqRestraintName, 32);
                IconGlyph.HelpMarker("Type the exact restraint device name your Sub told you. Always applies rather than toggling, since this bundle has no captured device to check the active state of - use the Restraints section above to release it.");
                using (ImRaii.Disabled(ctqRestraintName.Trim().Length == 0))
                {
                    if (ImGui.SmallButton($"{(editingOwnerActionIndex is null ? "Add" : "Save")}##ctqRestraintBtn"))
                    {
                        CommitOwnerAction(new CustomTriggerAction { Kind = CustomTriggerActionKind.Restraint, RestraintDeviceName = ctqRestraintName.Trim() });
                        ctqRestraintName = "";
                    }
                }
                break;

            case CustomTriggerActionKind.Chat:
                ImGui.SetNextItemWidth(320);
                ImGui.InputText("Message##ctqChat", ref ctqChatText, 400);
                IconGlyph.HelpMarker("Sent exactly as typed, unmodified - start it with a slash command (e.g. /sit) or a channel prefix (e.g. /p) to use those instead of the default chat channel. Needs your Sub's Custom chat messages permission and its own acknowledgement (see the README's Automation risk section).");
                using (ImRaii.Disabled(ctqChatText.Trim().Length == 0))
                {
                    if (ImGui.SmallButton($"{(editingOwnerActionIndex is null ? "Add" : "Save")}##ctqChatBtn"))
                    {
                        CommitOwnerAction(new CustomTriggerAction { Kind = CustomTriggerActionKind.Chat, ChatText = ctqChatText });
                        ctqChatText = "";
                    }
                }
                break;
        }

        ImGui.Spacing();
        if (ctqLabel.Trim().Length > 0 && ctqDraftActions.Count > 0)
        {
            var command = CustomTriggerCommand.BuildCastCommand(ctqLabel.Trim(), ctqDraftActions);
            ImGui.TextUnformatted(editingOwnerBundle is null ? "Send or save this bundle:" : "Update this saved bundle:");
            ContinueRowOrWrap(ButtonWidth("Send"));
            DrawSendCopyButtons(command, canSend, "ctqCustomTrigger");
            ContinueRowOrWrap(ButtonWidth("Save bundle"));
            var aliases = plugin.Configuration.QuickCommands.Aliases;
            var stale = editingOwnerBundle is not null && !aliases.Contains(editingOwnerBundle);
            var duplicate = aliases.Any(q => !ReferenceEquals(q, editingOwnerBundle) &&
                (string.Equals(q.Label, ctqLabel.Trim(), StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(q.Command, command, StringComparison.OrdinalIgnoreCase)));
            var safe = CommandSelector.Fits(plugin.ChatComposer.Compose(command));
            using (ImRaii.Disabled(stale || duplicate || !safe))
            {
                if (ImGui.SmallButton($"{(editingOwnerBundle is null ? "Save bundle" : "Save changes")}##ctqSave"))
                {
                    if (editingOwnerBundle is null)
                        aliases.Add(new QuickCommand { Label = ctqLabel.Trim(), Command = command });
                    else
                    {
                        editingOwnerBundle.Label = ctqLabel.Trim();
                        editingOwnerBundle.Command = command;
                    }
                    plugin.Configuration.Save();
                    ClearOwnerBundleDraft();
                }
            }
            if (stale) IconGlyph.WrappedColored(Theme.Warning, "This saved bundle was removed while it was being edited.");
            else if (duplicate) IconGlyph.WrappedColored(Theme.Warning, "Another saved bundle already uses this label or command.");
            else if (!safe) IconGlyph.WrappedColored(Theme.Warning, "This bundle is too long for a safe chat payload.");
            ContinueRowOrWrap(ButtonWidth("Cancel"));
            if (ImGui.SmallButton($"{(editingOwnerBundle is null ? "Clear bundle" : "Cancel")}##ctq"))
                ClearOwnerBundleDraft();
        }
        else
        {
            IconGlyph.WrappedColored(Theme.Warning, "Give this bundle a label and at least one action before it can be sent.");
        }
    }

    private void ClearOwnerBundleDraft()
    {
        ctqDraftActions.Clear();
        ctqLabel = "";
        editingOwnerActionIndex = null;
        editingOwnerBundle = null;
    }

    private void BeginOwnerBundleEdit(QuickCommand command)
    {
        const string prefix = "customtrigger cast ";
        if (!command.Command.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !CustomTriggerCommand.TryParseCastCommand(command.Command[prefix.Length..], out var label, out var actions))
            return;

        editingOwnerBundle = command;
        ctqLabel = label;
        ctqDraftActions.Clear();
        ctqDraftActions.AddRange(actions.Select(CloneAction));
        editingOwnerActionIndex = null;
    }

    private void DrawRestraintQuickRow(QuickCommand cmd, List<QuickCommand> list, bool canSend)
    {
        ImGui.PushID($"restraintQuick_{cmd.Label}");
        var hasRules = cmd.RestraintRules is { Count: > 0 };

        ImGui.TextUnformatted(cmd.Label);
        ContinueRowOrWrap(ButtonWidth("Favorited"));
        DrawFavoriteToggle(cmd, cmd.Label);
        ContinueRowOrWrap(ButtonWidth("Send"));
        using (ImRaii.Disabled(!hasRules))
            DrawSendCopyButtons(cmd.Command, canSend, $"{cmd.Label}_{cmd.Command}");
        ContinueRowOrWrap(ButtonWidth("Edit"));
        if (ImGui.SmallButton("Edit##restraintQuick"))
            BeginQuickCommandEdit(cmd, list);
        var configureLabel = hasRules ? "Edit rules" : "Configure rules";
        ContinueRowOrWrap(ButtonWidth(configureLabel));
        var expanded = expandedRestraintRuleEditors.Contains(cmd.Label);
        if (ImGui.SmallButton(configureLabel))
        {
            if (expanded)
            {
                expandedRestraintRuleEditors.Remove(cmd.Label);
            }
            else
            {
                expandedRestraintRuleEditors.Add(cmd.Label);
                restraintRuleEdits[cmd.Label] = FromRules(cmd.RestraintRules);
            }
            expanded = !expanded;
        }
        ContinueRowOrWrap(ButtonWidth("Remove"));
        if (ImGui.SmallButton("Remove"))
        {
            list.Remove(cmd);
            expandedRestraintRuleEditors.Remove(cmd.Label);
            restraintRuleEdits.Remove(cmd.Label);
            plugin.Configuration.Save();
            ImGui.PopID();
            return;
        }

        if (!hasRules)
        {
            IconGlyph.WrappedColored(Theme.Warning, "No rules assigned yet - configure rules before this can be sent.");
        }

        if (expanded && restraintRuleEdits.TryGetValue(cmd.Label, out var edit))
        {
            ImGui.Indent();
            DrawRestraintRuleCheckboxes(edit, $"restraintQuickRule_{cmd.Label}");

            var hasAnyRule = HasAnyRule(edit);
            var boundAnimationsConfigured = BoundAnimationsConfigured(edit);
            if (hasAnyRule && !boundAnimationsConfigured)
                IconGlyph.WrappedColored(Theme.Warning, "Choose an animation for every checked Arms/Legs/Full Body Cuffed rule before saving.");

            using (ImRaii.Disabled(!hasAnyRule || !boundAnimationsConfigured))
            {
                if (ImGui.SmallButton("Save rules##restraintQuickRule"))
                {
                    var rules = ToRules(edit);
                    cmd.RestraintRules = rules;
                    cmd.Command = RestraintCommand.BuildLockCommand(cmd.Label, rules);
                    plugin.Configuration.Save();
                    expandedRestraintRuleEditors.Remove(cmd.Label);
                    restraintRuleEdits.Remove(cmd.Label);
                }
            }
            ImGui.Unindent();
        }

        ImGui.PopID();
    }

    private static RestraintRuleEditState FromRules(List<RestraintRuleAssignment>? rules)
    {
        var edit = new RestraintRuleEditState();
        foreach (var rule in rules ?? [])
        {
            switch (rule.Kind)
            {
                case RestraintRuleKind.ForcedPose:
                    edit.ForcedPose = true;
                    edit.PoseIndex = Math.Clamp(rule.PoseModeId - 1, 0, PoseNames.Length - 1);
                    break;
                case RestraintRuleKind.WalkOnly: edit.WalkOnly = true; break;
                case RestraintRuleKind.ActionBlock: edit.ActionBlock = true; break;
                case RestraintRuleKind.GagChat: edit.GagChat = true; break;
                case RestraintRuleKind.ArmsCuffed: edit.ArmsCuffed = true; edit.ArmsCuffedAnimationId = rule.AnimationId; break;
                case RestraintRuleKind.LegsCuffed: edit.LegsCuffed = true; edit.LegsCuffedAnimationId = rule.AnimationId; break;
                case RestraintRuleKind.FullBodyCuffed: edit.FullBodyCuffed = true; edit.FullBodyCuffedAnimationId = rule.AnimationId; break;
            }
        }
        return edit;
    }

    private List<RestraintRuleAssignment> ToRules(RestraintRuleEditState edit)
    {
        string? LabelFor(string? id)
        {
            if (id is null) return null;
            if (plugin.Configuration.Role == PluginRole.Owner && plugin.Configuration.GestureMapping.ImportedPeerCatalog.TryGetValue(id, out var peer))
                return CommandSelector.GestureSelector(peer, plugin.Configuration.GestureMapping.ImportedPeerCatalog.Values);
            return plugin.Configuration.GestureMapping.LocalCatalog.TryGetValue(id, out var local)
                ? CommandSelector.GestureLabel(local.ModName, local.GroupName, local.AnimationName, local.Trigger) : null;
        }
        var rules = new List<RestraintRuleAssignment>();
        if (edit.ForcedPose)
            rules.Add(new RestraintRuleAssignment { Kind = RestraintRuleKind.ForcedPose, PoseModeId = edit.PoseIndex + 1 });
        if (edit.WalkOnly)
            rules.Add(new RestraintRuleAssignment { Kind = RestraintRuleKind.WalkOnly });
        if (edit.ActionBlock)
            rules.Add(new RestraintRuleAssignment { Kind = RestraintRuleKind.ActionBlock });
        if (edit.GagChat)
            rules.Add(new RestraintRuleAssignment { Kind = RestraintRuleKind.GagChat });
        if (edit.ArmsCuffed)
            rules.Add(new RestraintRuleAssignment { Kind = RestraintRuleKind.ArmsCuffed, AnimationId = edit.ArmsCuffedAnimationId, AnimationLabel = LabelFor(edit.ArmsCuffedAnimationId) });
        if (edit.LegsCuffed)
            rules.Add(new RestraintRuleAssignment { Kind = RestraintRuleKind.LegsCuffed, AnimationId = edit.LegsCuffedAnimationId, AnimationLabel = LabelFor(edit.LegsCuffedAnimationId) });
        if (edit.FullBodyCuffed)
            rules.Add(new RestraintRuleAssignment { Kind = RestraintRuleKind.FullBodyCuffed, AnimationId = edit.FullBodyCuffedAnimationId, AnimationLabel = LabelFor(edit.FullBodyCuffedAnimationId) });
        return rules;
    }

    private void DrawTitleQuickSection(bool canSend)
    {
        IconGlyph.Text(FontAwesomeIcon.Heading, "Title");
        var quick = plugin.Configuration.QuickCommands.Titles;

        ImGui.SetNextItemWidth(220);
        ImGui.InputText("##newQuickTitle", ref newTitleQuickText, 64);
        IconGlyph.HelpMarker("The exact title text applied via Honorific.");
        ImGui.Checkbox("Prefix (not suffix)##newQuickTitle", ref newTitleQuickIsPrefix);
        IconGlyph.HelpMarker("Show the title before your Sub's name instead of after it.");
        ImGui.ColorEdit3("Color##newQuickTitle", ref newTitleQuickColor);
        IconGlyph.HelpMarker("Honorific title color - matches the Sub's own Title alias color picker.");
        if (ImGui.SmallButton("Add Command##quickTitle") && newTitleQuickText.Trim().Length > 0)
        {
            var text = newTitleQuickText.Trim();
            quick.Add(new QuickCommand
            {
                Label = text,
                Command = TitleCommand.BuildStyleCommand(text, newTitleQuickIsPrefix, newTitleQuickColor),
                TitleIsPrefix = newTitleQuickIsPrefix,
                TitleColor = newTitleQuickColor,
            });
            plugin.Configuration.Save();
            newTitleQuickText = "";
            newTitleQuickIsPrefix = false;
            newTitleQuickColor = new Vector3(1, 1, 1);
        }
        IconGlyph.HelpMarker("Saves a one-click button that force-applies this exact title (with the chosen prefix/color) and locks it on - your Sub's own clear-title alias is refused while it's locked, only the \"Clear title\" button below (or their panic) releases it. Requires a Sub on this plugin version to recognize the styled command - see the README.");

        DrawFixedQuickRow("Clear title", "title clear", canSend);

        if (quick.Count == 0)
            return;

        using var _ = ImRaii.Child("titleQuickList", new Vector2(0, 90), true);
        foreach (var cmd in quick.ToArray())
            DrawSavedQuickRow(cmd, quick, canSend);
    }

    private void DrawOutfitQuickSection(bool canSend)
    {
        var quick = plugin.Configuration.QuickCommands.Outfits;
        DrawSectionTitleRow(FontAwesomeIcon.Tshirt, "Outfit", quick.Count > 0, "outfitQuick", () =>
        {
            quick.Clear();
            plugin.Configuration.Save();
        });

        DrawFixedQuickRow("Unlock outfit", "outfit unlock", canSend);

        if (quick.Count == 0)
        {
            IconGlyph.WrappedDisabled("No outfits imported yet - use \"Import commands\" above.");
            return;
        }

        using var _ = ImRaii.Child("outfitQuickList", new Vector2(0, 120), true);
        foreach (var cmd in quick.ToArray())
            DrawSavedQuickRow(cmd, quick, canSend);
    }

    /// collar/ui-organization: reworked to match the Sub's animation picker (AnimationPickerWindow) instead
    /// of one flat scrolling list - a Sub with 1000+ gestures made the old fixed-height flat child
    /// unusable. Grouped by GestureModName/GestureGroupName (carried through import - see
    /// CatalogSyncService.ImportGestureLines) with a search box filtering by mod/group/label.
    private void DrawGestureQuickSection(bool canSend)
    {
        var quick = plugin.Configuration.QuickCommands.Gestures;
        DrawSectionTitleRow(FontAwesomeIcon.TheaterMasks, "Gesture", quick.Count > 0, "gestureQuick", () =>
        {
            quick.Clear();
            plugin.Configuration.Save();
        });

        if (quick.Count == 0)
        {
            IconGlyph.WrappedDisabled("No gestures imported yet - use \"Import commands\" above.");
            return;
        }

        ImGui.SetNextItemWidth(Math.Max(180, ImGui.GetContentRegionAvail().X));
        ImGui.InputTextWithHint("##gestureQuickSearch", "Search mod, group, or animation...", ref gestureQuickSearch, 128);

        var filter = gestureQuickSearch.Trim();
        var visible = quick.Where(c => filter.Length == 0
            || c.Label.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || (c.GestureModName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
            || (c.GestureGroupName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();

        IconGlyph.WrappedDisabled($"{visible.Count} shown / {quick.Count} imported");

        using var _ = ImRaii.Child("gestureQuickList", new Vector2(0, 260), true);
        if (visible.Count == 0)
        {
            IconGlyph.WrappedDisabled("No gestures match this search.");
            return;
        }

        foreach (var modGroup in visible.GroupBy(c => c.GestureModName ?? "Ungrouped").OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var modFlags = filter.Length > 0 ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
            if (!ImGui.CollapsingHeader($"{modGroup.Key} ({modGroup.Count()})##gestureQuickMod_{modGroup.Key}", modFlags))
                continue;

            ImGui.Indent();
            // Ordered by GestureGroupOrder/GestureOptionOrder (the Sub's own Penumbra manifest order,
            // carried through import - see CatalogSyncService.ImportGestureLines), not alphabetically -
            // an alphabetic sort of option names like "1".."400" would put "10" before "2".
            foreach (var subGroup in modGroup.GroupBy(c => c.GestureGroupName ?? "").OrderBy(g => g.Min(c => c.GestureGroupOrder)))
            {
                var hasGroupLabel = subGroup.Key.Length > 0;
                var groupOpen = true;
                if (hasGroupLabel)
                {
                    var groupFlags = subGroup.Count() <= 4 || filter.Length > 0 ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
                    groupOpen = ImGui.TreeNodeEx($"{subGroup.Key}##gestureQuickGroup_{modGroup.Key}_{subGroup.Key}", groupFlags);
                }

                if (groupOpen)
                {
                    foreach (var cmd in subGroup.OrderBy(c => c.GestureOptionOrder))
                        DrawSavedQuickRow(cmd, quick, canSend);
                }

                if (hasGroupLabel && groupOpen)
                    ImGui.TreePop();
            }
            ImGui.Unindent();
        }
    }

    /// Follow has no reserved-keyword override the way Title/Outfit/Gesture do (ChatCommandListener never
    /// added a "force follow" - it's always a plain alias), so there's nothing to auto-populate from a
    /// scan. "leash"/"unleash" are AliasBook's own defaults, shown as ready-to-use fixed rows so
    /// there's a working Send/Copy immediately even before the Owner adds anything - if the Sub renamed
    /// their engage/release words, the Owner adds the real ones the same way as any other Quick Command.
    private void DrawFollowQuickSection(bool canSend)
    {
        IconGlyph.Text(FontAwesomeIcon.Link, "Follow / Leash");
        var quick = plugin.Configuration.QuickCommands.Follow;

        ImGui.SetNextItemWidth(220);
        ImGui.InputText("##newQuickFollow", ref newFollowQuickText, 32);
        ImGui.SameLine();
        if (ImGui.SmallButton("Add Command##quickFollow") && newFollowQuickText.Trim().Length > 0)
        {
            var text = newFollowQuickText.Trim();
            quick.Add(new QuickCommand { Label = text, Command = text });
            plugin.Configuration.Save();
            newFollowQuickText = "";
        }
        IconGlyph.HelpMarker("Follow has no direct-override syntax - this saves a plain alias word, exactly what your Sub set as their leash/unleash trigger in the Collar tab.");

        if (quick.Count == 0)
        {
            DrawFixedQuickRow("Leash (default)", "leash", canSend);
            DrawFixedQuickRow("Unleash (default)", "unleash", canSend);
            IconGlyph.WrappedDisabled("Defaults shown above - add your own if your Sub customized their alias words.");
            return;
        }

        using var _ = ImRaii.Child("followQuickList", new Vector2(0, 90), true);
        foreach (var cmd in quick.ToArray())
            DrawSavedQuickRow(cmd, quick, canSend);
    }

    private void DrawFreeformComposer(bool canSend)
    {
        IconGlyph.Text(FontAwesomeIcon.Comment, "Custom Trigger Bundles / one-off");
        ImGui.TextWrapped("Multi-action Custom Trigger bundles your Sub created land here on import - a single-action alias imports straight into its own category above instead. Type a bundle alias your Sub told you about, or any one-off override. Add Command saves it as a one-click button below for reuse.");

        ImGui.InputText("Command", ref commandInput, 96);
        IconGlyph.HelpMarker("Either a short alias name your Sub defined, or a direct override: \"title create <text>\" / \"title clear\", \"outfit lock <name>\" / \"outfit unlock\", \"gesture <name>\".");

        var composed = plugin.ChatComposer.Compose(commandInput.Trim());
        ImGui.TextUnformatted("Preview:");
        ImGui.TextWrapped(composed);

        var hasCommand = commandInput.Trim().Length > 0;
        using (ImRaii.Disabled(!canSend || !hasCommand))
        {
            if (ImGui.Button("Send"))
                plugin.ChatSender.Send(composed);
        }
        ImGui.SameLine();
        using (ImRaii.Disabled(!hasCommand))
        {
            if (ImGui.Button("Copy to clipboard"))
                ImGui.SetClipboardText(composed);
        }
        ImGui.SameLine();
        var aliasQuick = plugin.Configuration.QuickCommands.Aliases;
        using (ImRaii.Disabled(!hasCommand))
        {
            if (ImGui.Button("Add Command##alias"))
            {
                var text = commandInput.Trim();
                aliasQuick.Add(new QuickCommand { Label = text, Command = text });
                plugin.Configuration.Save();
                commandInput = "";
            }
        }
        IconGlyph.HelpMarker("Send fires this one /tell immediately - the same one-click, one-message shape as pressing an FFXIV macro, and the only thing in this plugin that ever sends chat for you. Copy never sends anything. Add Command saves the text above as a one-click button below.");

        if (aliasQuick.Count == 0)
            return;

        ImGui.Spacing();
        using var _ = ImRaii.Child("aliasQuickList", new Vector2(0, 100), true);
        foreach (var cmd in aliasQuick.ToArray())
            DrawSavedQuickRow(cmd, aliasQuick, canSend);
    }

    /// A built-in action (not user-saved, can't be removed) - "Clear title" and "Unlock outfit" always
    /// exist since every force-locked category needs a release valve regardless of what's been saved.
    private void DrawFixedQuickRow(string label, string command, bool canSend)
    {
        ImGui.TextUnformatted(label);
        ContinueRowOrWrap(ButtonWidth("Send"));
        DrawSendCopyButtons(command, canSend, $"fixed_{label}");
    }

    /// `displayLabel` lets a category-specific caller (e.g. Moodles, see collar/moodles' markup-stripping
    /// requirement) transform `cmd.Label` for display only - `cmd.Label` itself, the id-suffix strings
    /// below, and `cmd.Command` all keep using the raw stored value, since only display should ever change.
    private void DrawSavedQuickRow(QuickCommand cmd, List<QuickCommand> list, bool canSend, Func<string, string>? displayLabel = null)
    {
        ImGui.TextUnformatted(displayLabel?.Invoke(cmd.Label) ?? cmd.Label);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(displayLabel?.Invoke(cmd.Label) ?? cmd.Label);
        ContinueRowOrWrap(ButtonWidth("Favorited"));
        DrawFavoriteToggle(cmd, $"{cmd.Label}_{cmd.Command}");
        ContinueRowOrWrap(ButtonWidth("Send"));
        DrawSendCopyButtons(cmd.Command, canSend, $"{cmd.Label}_{cmd.Command}");
        ContinueRowOrWrap(ButtonWidth("Edit"));
        if (ImGui.SmallButton($"Edit##{cmd.Label}_{cmd.Command}"))
        {
            if (ReferenceEquals(list, plugin.Configuration.QuickCommands.Aliases) &&
                cmd.Command.StartsWith("customtrigger cast ", StringComparison.OrdinalIgnoreCase))
                BeginOwnerBundleEdit(cmd);
            else
                BeginQuickCommandEdit(cmd, list);
        }
        ContinueRowOrWrap(ButtonWidth("Remove"));
        if (ImGui.SmallButton($"Remove##{cmd.Label}_{cmd.Command}"))
        {
            list.Remove(cmd);
            plugin.Configuration.Save();
        }
    }

    private void BeginQuickCommandEdit(QuickCommand command, List<QuickCommand> list)
    {
        editingQuickCommand = command;
        editingQuickList = list;
        editingQuickLabel = command.Label;
        editingQuickPayload = command.Command;
        editingQuickCategory = CategoryFor(list);
        editingQuickTarget = command.Target ?? ExtractQuickTarget(command, editingQuickCategory);
        editingQuickOriginalTarget = editingQuickTarget;
        editingQuickTitleIsPrefix = command.TitleIsPrefix;
        editingQuickTitleColor = command.TitleColor ?? new Vector3(1, 1, 1);

        if (editingQuickCategory == QuickEditCategory.Title &&
            command.Command.StartsWith("title style ", StringComparison.OrdinalIgnoreCase) &&
            TitleCommand.TryParseStyleCommand(command.Command["title style ".Length..], out var title, out var prefix, out var color))
        {
            editingQuickTarget = title;
            editingQuickTitleIsPrefix = prefix;
            editingQuickTitleColor = color;
        }
        editingQuickOriginalTarget = editingQuickTarget;
        editingQuickOriginalTitleIsPrefix = editingQuickTitleIsPrefix;
        editingQuickOriginalTitleColor = editingQuickTitleColor;
    }

    private QuickEditCategory CategoryFor(List<QuickCommand> list)
    {
        var quick = plugin.Configuration.QuickCommands;
        if (ReferenceEquals(list, quick.Titles)) return QuickEditCategory.Title;
        if (ReferenceEquals(list, quick.Outfits)) return QuickEditCategory.Outfit;
        if (ReferenceEquals(list, quick.Gestures)) return QuickEditCategory.Gesture;
        if (ReferenceEquals(list, quick.Follow)) return QuickEditCategory.Follow;
        if (ReferenceEquals(list, quick.Moodles)) return QuickEditCategory.Moodle;
        return QuickEditCategory.Raw;
    }

    private static string ExtractQuickTarget(QuickCommand command, QuickEditCategory category)
    {
        var prefixes = category switch
        {
            QuickEditCategory.Outfit => new[] { "outfit lock " },
            QuickEditCategory.Gesture => new[] { "gesture " },
            QuickEditCategory.Moodle => new[] { "moodle apply " },
            _ => Array.Empty<string>(),
        };
        foreach (var prefix in prefixes)
            if (command.Command.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return command.Command[prefix.Length..].Trim().Trim('"');
        return category == QuickEditCategory.Follow ? command.Command : command.Label;
    }

    /// A shared focused draft editor for Owner entries. Nothing touches the stored object until Save;
    /// Cancel and validation failures therefore remain lossless, and reference identity detects a row
    /// removed while the editor was open.
    private void DrawQuickCommandEditor()
    {
        if (editingQuickCommand is not { } source || editingQuickList is not { } list)
            return;

        ImGui.Spacing();
        ImGui.Separator();
        IconGlyph.Text(FontAwesomeIcon.Pen, "Edit saved command");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("Label##quickEdit", ref editingQuickLabel, 80);

        switch (editingQuickCategory)
        {
            case QuickEditCategory.Title:
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("Title text##quickEdit", ref editingQuickTarget, 64);
                ImGui.Checkbox("Prefix (not suffix)##quickEdit", ref editingQuickTitleIsPrefix);
                ImGui.ColorEdit3("Color##quickEdit", ref editingQuickTitleColor);
                break;
            case QuickEditCategory.Outfit:
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("Outfit name##quickEdit", ref editingQuickTarget, 96);
                break;
            case QuickEditCategory.Gesture:
                DrawQuickGestureTargetPicker();
                break;
            case QuickEditCategory.Follow:
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("Sub alias##quickEdit", ref editingQuickTarget, 32);
                break;
            case QuickEditCategory.Moodle:
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("Moodle status##quickEdit", ref editingQuickTarget, 96);
                break;
            default:
                ImGui.SetNextItemWidth(-1);
                ImGui.InputText("Command##quickEdit", ref editingQuickPayload, 400);
                break;
        }

        var (draftCommand, draftTarget) = BuildQuickEditPayload(source);

        var stale = !list.Contains(source);
        var duplicate = list.Any(q => !ReferenceEquals(q, source) &&
            (string.Equals(q.Label, editingQuickLabel.Trim(), StringComparison.OrdinalIgnoreCase) ||
             string.Equals(q.Command, draftCommand, StringComparison.OrdinalIgnoreCase)));
        var aliasList = ReferenceEquals(list, plugin.Configuration.QuickCommands.Aliases) ||
            ReferenceEquals(list, plugin.Configuration.QuickCommands.Follow);
        var reserved = aliasList && IsReserved(draftCommand);
        var safe = CommandSelector.Fits(plugin.ChatComposer.Compose(draftCommand));
        var validTarget = draftCommand.Length > 0 && (editingQuickCategory != QuickEditCategory.Gesture || draftTarget is not null);
        if (stale) IconGlyph.WrappedColored(Theme.Warning, "This entry was removed while it was being edited.");
        else if (duplicate) IconGlyph.WrappedColored(Theme.Warning, "Another saved entry already uses this label or command.");
        else if (reserved) IconGlyph.WrappedColored(Theme.Warning, "This alias is reserved for a direct Owner command.");
        else if (!safe) IconGlyph.WrappedColored(Theme.Warning, "This command is too long for a safe chat payload.");

        using (ImRaii.Disabled(stale || duplicate || reserved || !safe || !validTarget || editingQuickLabel.Trim().Length == 0))
        {
            if (ImGui.Button("Save##quickEdit"))
            {
                source.Label = editingQuickLabel.Trim();
                source.Command = draftCommand;
                source.Target = draftTarget;
                if (editingQuickCategory == QuickEditCategory.Title)
                {
                    source.TitleIsPrefix = editingQuickTitleIsPrefix;
                    source.TitleColor = editingQuickTitleColor;
                }
                if (editingQuickCategory == QuickEditCategory.Gesture && draftTarget is not null &&
                    plugin.Configuration.GestureMapping.ImportedPeerCatalog.TryGetValue(draftTarget, out var gesture))
                {
                    source.GestureModName = gesture.ModName;
                    source.GestureGroupName = gesture.GroupName;
                    source.GestureGroupOrder = gesture.GroupOrder;
                    source.GestureOptionOrder = gesture.OptionOrder;
                }
                plugin.Configuration.Save();
                CancelQuickCommandEdit();
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel##quickEdit"))
            CancelQuickCommandEdit();
    }

    private void DrawQuickGestureTargetPicker()
    {
        var catalog = plugin.Configuration.GestureMapping.ImportedPeerCatalog.Values
            .OrderBy(g => g.ModName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.GroupOrder).ThenBy(g => g.OptionOrder).ToList();
        var preview = plugin.Configuration.GestureMapping.ImportedPeerCatalog.TryGetValue(editingQuickTarget, out var selected)
            ? CommandSelector.GestureSelector(selected, catalog)
            : "Choose an imported gesture...";
        ImGui.SetNextItemWidth(-1);
        if (!ImGui.BeginCombo("Gesture##quickEdit", preview)) return;
        foreach (var entry in catalog)
        {
            var label = CommandSelector.GestureSelector(entry, catalog);
            if (ImGui.Selectable($"{label}##{entry.Id}", string.Equals(entry.Id, editingQuickTarget, StringComparison.OrdinalIgnoreCase)))
                editingQuickTarget = entry.Id;
        }
        ImGui.EndCombo();
    }

    private (string Command, string? Target) BuildQuickEditPayload(QuickCommand source)
    {
        var target = editingQuickTarget.Trim();
        var targetChanged = !string.Equals(target, editingQuickOriginalTarget.Trim(), StringComparison.Ordinal) ||
            (editingQuickCategory == QuickEditCategory.Title &&
             (editingQuickTitleIsPrefix != editingQuickOriginalTitleIsPrefix || editingQuickTitleColor != editingQuickOriginalTitleColor));
        if (!targetChanged && editingQuickCategory != QuickEditCategory.Raw)
            return (source.Command, source.Target);
        return editingQuickCategory switch
        {
            QuickEditCategory.Title when target.Length > 0 =>
                (TitleCommand.BuildStyleCommand(target, editingQuickTitleIsPrefix, editingQuickTitleColor), null),
            QuickEditCategory.Outfit when target.Length > 0 => ($"outfit lock {target}", target),
            QuickEditCategory.Gesture when plugin.Configuration.GestureMapping.ImportedPeerCatalog.TryGetValue(target, out var entry) =>
                ($"gesture {CommandSelector.Quote(CommandSelector.GestureSelector(entry, plugin.Configuration.GestureMapping.ImportedPeerCatalog.Values))}", entry.Id),
            QuickEditCategory.Follow when target.Length > 0 => (target, null),
            QuickEditCategory.Moodle when target.Length > 0 =>
                ($"moodle apply {CommandSelector.Quote(target)}", target),
            QuickEditCategory.Raw => (editingQuickPayload.Trim(), source.Target),
            _ => ("", null),
        };
    }

    private void CancelQuickCommandEdit()
    {
        editingQuickCommand = null;
        editingQuickList = null;
        editingQuickLabel = "";
        editingQuickPayload = "";
        editingQuickTarget = "";
        editingQuickOriginalTarget = "";
        editingQuickCategory = QuickEditCategory.Raw;
    }

    /// collar/ui-organization "Owner can favorite quick commands for quick access": a plain toggle shared
    /// by every quick-command row (DrawSavedQuickRow covers Title/Outfit/Gesture/Follow/Moodles/Aliases;
    /// DrawRestraintQuickRow calls this too, since its row layout is its own bespoke one) - never affects
    /// Send/Copy/Remove or any other per-row state.
    private void DrawFavoriteToggle(QuickCommand cmd, string idSuffix)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.Warning, cmd.IsFavorite))
        {
            if (ImGui.SmallButton($"{(cmd.IsFavorite ? "Favorited" : "Favorite")}##fav_{idSuffix}"))
            {
                cmd.IsFavorite = !cmd.IsFavorite;
                plugin.Configuration.Save();
            }
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(cmd.IsFavorite ? "Remove from favorites" : "Add to favorites");
    }

    private void DrawSendCopyButtons(string command, bool canSend, string idSuffix)
    {
        var composed = plugin.ChatComposer.Compose(command);
        var fits = CommandSelector.Fits(composed);

        using (ImRaii.Disabled(!canSend || !fits))
        {
            if (ImGui.SmallButton($"Send##{idSuffix}"))
                plugin.ChatSender.Send(composed);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(!fits ? "Command is too long for a safe chat payload." : canSend ? composed : "No /tell target yet - pairing hasn't captured your Sub's name.");

        ContinueRowOrWrap(ButtonWidth("Copy"));
        using (ImRaii.Disabled(!fits))
        if (ImGui.SmallButton($"Copy##{idSuffix}"))
            ImGui.SetClipboardText(composed);
    }

    /// "title"/"outfit"/"gesture" are reserved for the Owner's direct override grammar (see
    /// ChatCommandListener.ReservedCategoryWords) - an alias with one of these exact names would be
    /// permanently unreachable, since the listener always routes to the override handler first.
    private static bool IsReserved(string alias) =>
        ChatCommandListener.ReservedCategoryWords.Contains(alias.Trim(), StringComparer.OrdinalIgnoreCase);

    private static void DrawReservedWordWarning(string alias)
    {
        if (IsReserved(alias))
            IconGlyph.WrappedColored(Theme.Warning, $"\"{alias.Trim()}\" is reserved for the Owner's direct override - pick a different alias.");
    }

    private void DrawClearAliasField(string label, Func<string> get, Action<string> set, PluginConfig config)
    {
        var value = get();
        if (ImGui.InputText(label, ref value, 32))
        {
            set(value);
            config.Save();
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
