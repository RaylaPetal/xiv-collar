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
using Glamourer.Api.Enums;

namespace CollarSystem.Plugin.UI;

/// One window for both roles - Role (Settings) only changes what a few tabs say and whether incoming
/// tells apply locally (ChatCommandListener), it no longer decides which window opens. Title/Wardrobe/
/// Gesture/Permissions are "what I've set up for someone who might command me" and stay available
/// regardless of Role (you might configure your own aliases before ever flipping to Sub); Owner is "what
/// I need to command someone else" and is the one tab that's actually role-specific in spirit, even though
/// nothing stops using it while set to Sub. Pairing status stays permanently above the nav bar. There is
/// deliberately no panic button here - panic is the /collarpanic safeword (Settings), typed rather than
/// clicked, so it can't be hit by accident or spotted by someone watching over a shoulder.
public class CollarWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string activeModule = "title";

    /// collar/ui-organization: lets the favorites window (or anything else outside this class) bring the
    /// main window forward already on the Owner tab, instead of leaving it wherever it last was.
    public void OpenOwnerTab()
    {
        activeModule = "owner";
        IsOpen = true;
    }

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

    private string newDeviceName = "";
    private int newDeviceSlotIndex;
    private ulong? newDeviceItemId;
    private readonly RestraintRuleEditState newDeviceRuleEdit = new();

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

    public CollarWindow(Plugin plugin) : base("Collar System###CollarWindow")
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

        if (NavBar.Draw(activeModule, "owner", NavItems) is { } clicked)
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
    }

    /// Both roles can receive a Pending handshake now (collarpair's role token - see
    /// ChatCommandListener), so this is one role-aware card instead of two windows each handling their own
    /// half. Sub's accepted pairing stays locked (only /collarpanic, the safeword command, undoes it);
    /// Owner's has a plain Release button, since nothing is actually applied to the Owner's own character
    /// for panic to revert.
    private void DrawCharacterHeader()
    {
        var pending = plugin.ChatCommandListener.Pending;
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
            IconGlyph.WrappedColored(Theme.Warning, $"Pending request: {request.Name}@{request.World} wants to pair as {roleLabel}.");
            if (sameRoleWarning)
                IconGlyph.WrappedColored(Theme.Danger, $"You're both set to {config.Role} in Settings - one of you should switch, or nothing will ever trigger.");

            if (ImGui.Button("Accept"))
                plugin.ChatCommandListener.AcceptPending();
            IconGlyph.HelpMarker("Trusts this sender as your paired peer from now on. Locks pairing on if you're set to Sub (only /collarpanic, your safeword command, undoes it) - if you're set to Owner, Release pairing (below, once accepted) undoes it any time.");
            ImGui.SameLine();
            if (ImGui.Button("Reject"))
                plugin.ChatCommandListener.DismissPending();
        }
        else if (peerUnpairedNotice is { } ownerNotice && config.Role == PluginRole.Owner)
        {
            var peerLabel = ownerNotice.PeerRole == PluginRole.Owner ? "Your Owner's" : "Your Sub's";
            IconGlyph.WrappedColored(Theme.Warning, $"{peerLabel} side ended pairing via panic - they will not receive any commands until you pair again.");
            if (ImGui.Button("Release pairing"))
            {
                plugin.PairingCommand.ReleasePeer();
                plugin.ChatCommandListener.DismissPeerUnpairedNotice();
            }
            IconGlyph.HelpMarker("Clears who you're paired with on your own client only - doesn't touch your former Sub's plugin at all.");
        }
        else if (!pairing.IsPaired)
        {
            IconGlyph.WrappedColored(Theme.TextMuted, "Not paired");
            IconGlyph.WrappedDisabled("Set your codes and send the pairing message from Settings when you're ready.");
        }
        else if (config.Role == PluginRole.Owner)
        {
            IconGlyph.WrappedColored(Theme.Success, $"Owns: {pairing.PeerName}@{pairing.PeerWorld}");
            if (ImGui.Button("Release pairing"))
                plugin.PairingCommand.ReleasePeer();
            IconGlyph.HelpMarker("Clears who you're paired with on your own client only - doesn't touch your Sub's plugin at all. Use this to fix a stale/wrong pairing or to free them up to pair with someone else.");
        }
        else
        {
            IconGlyph.WrappedColored(Theme.Success, $"Owned by: {pairing.PeerName}@{pairing.PeerWorld}");
            IconGlyph.WrappedDisabled("Locked until you use /collarpanic.");
            if (peerUnpairedNotice is { } subNotice)
            {
                var peerLabel = subNotice.PeerRole == PluginRole.Owner ? "your Owner's" : "your Sub's";
                IconGlyph.WrappedColored(Theme.Warning, $"Note: {peerLabel} side ended pairing via panic. You're still paired and locked until you use /collarpanic - this doesn't change that.");
                if (ImGui.SmallButton("Dismiss##peerUnpairedNotice"))
                    plugin.ChatCommandListener.DismissPeerUnpairedNotice();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        IconGlyph.Text(FontAwesomeIcon.ShieldAlt, "Safeword");
        SafewordEditor.Draw(config, "mainHeader", ref revealSafeword);
        IconGlyph.HelpMarker("This only configures the typed /collarpanic command; editing it never triggers panic or changes pairing.");
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

        DrawClearAliasField("Unlock alias", () => config.Aliases.UnlockOutfitAlias, v => config.Aliases.UnlockOutfitAlias = v, config);
        IconGlyph.HelpMarker("The alias that unlocks your current Glamourer design, using whichever lock key that design was last applied with.");

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

        var devices = config.RestraintMapping.Devices.Values.ToList();
        if (devices.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextUnformatted("Captured devices");
            foreach (var device in devices)
            {
                ImGui.PushID($"device_{device.Id}");
                var active = plugin.RestraintCommand.IsActive(device.Id);
                var ruleSummary = string.Join(", ", device.Rules.Select(r => r.Kind switch
                {
                    RestraintRuleKind.ForcedPose => $"forced pose ({PoseName(r.PoseModeId)})",
                    RestraintRuleKind.WalkOnly => "walk-only",
                    RestraintRuleKind.ActionBlock => "action-block",
                    RestraintRuleKind.GagChat => "gagged",
                    RestraintRuleKind.ArmsCuffed => "arms cuffed",
                    RestraintRuleKind.LegsCuffed => "legs cuffed",
                    RestraintRuleKind.FullBodyCuffed => "full body cuffed",
                    _ => r.Kind.ToString(),
                }));
                ImGui.BulletText($"{device.Name} ({device.Slot}){(active ? " (active)" : "")} - {ruleSummary}");
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
        ImGui.TextUnformatted("Capture a new device");
        var slotNames = LockableEquipSlots.All.Select(s => s.ToString()).ToArray();
        newDeviceSlotIndex = Math.Clamp(newDeviceSlotIndex, 0, slotNames.Length - 1);
        ImGui.InputText("Device name##newDevice", ref newDeviceName, 32);
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

        using (ImRaii.Disabled(newDeviceName.Length == 0 || newDeviceItemId is null || !hasAnyRule || !boundAnimationsConfigured))
        {
            if (ImGui.Button("Capture device") && newDeviceItemId is { } chosenItemId)
            {
                var slot = LockableEquipSlots.All[newDeviceSlotIndex];
                var rules = ToRules(newDeviceRuleEdit);

                if (plugin.RestraintCommand.CaptureDeviceFromItem(slot, chosenItemId, newDeviceName, rules))
                {
                    newDeviceName = "";
                    newDeviceItemId = null;
                    newDeviceRuleEdit.ForcedPose = newDeviceRuleEdit.WalkOnly = newDeviceRuleEdit.ActionBlock = newDeviceRuleEdit.GagChat = false;
                    newDeviceRuleEdit.ArmsCuffed = newDeviceRuleEdit.LegsCuffed = newDeviceRuleEdit.FullBodyCuffed = false;
                    newDeviceRuleEdit.ArmsCuffedAnimationId = newDeviceRuleEdit.LegsCuffedAnimationId = newDeviceRuleEdit.FullBodyCuffedAnimationId = null;
                }
            }
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

    private static bool BoundAnimationsConfigured(RestraintRuleEditState edit) =>
        (!edit.ArmsCuffed || edit.ArmsCuffedAnimationId is not null)
        && (!edit.LegsCuffed || edit.LegsCuffedAnimationId is not null)
        && (!edit.FullBodyCuffed || edit.FullBodyCuffedAnimationId is not null);

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
        var catalog = plugin.Configuration.GestureMapping.LocalCatalog;
        var chosenLabel = currentAnimationId is { } id && catalog.TryGetValue(id, out var entry) ? entry.Label : "(none chosen)";
        ImGui.TextUnformatted($"Animation: {chosenLabel}");
        ImGui.SameLine();
        if (ImGui.SmallButton($"Choose##{idSuffix}"))
            plugin.AnimationPickerWindow.Open(chosen => onChosen(chosen.Id));
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
            var summary = string.Join(", ", t.Actions.Select(SummarizeCustomTriggerAction));
            ImGui.BulletText($"{t.Alias} -> {summary}");
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
        ImGui.TextUnformatted("New trigger");
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
                ImGui.BulletText(SummarizeCustomTriggerAction(ctDraftActions[i]));
                ImGui.SameLine();
                if (ImGui.SmallButton("Remove"))
                {
                    ctDraftActions.RemoveAt(i);
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

        switch (kind)
        {
            case CustomTriggerActionKind.Title:
                ImGui.InputText("Text##newCtTitle", ref ctTitleText, 64);
                ImGui.Checkbox("Prefix##newCtTitle", ref ctTitleIsPrefix);
                ImGui.ColorEdit3("Color##newCtTitle", ref ctTitleColor);
                using (ImRaii.Disabled(ctTitleText.Length == 0))
                {
                    if (ImGui.Button("Add action##newCtTitleBtn"))
                    {
                        ctDraftActions.Add(new CustomTriggerAction { Kind = CustomTriggerActionKind.Title, TitleText = ctTitleText, TitleIsPrefix = ctTitleIsPrefix, TitleColor = ctTitleColor });
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
                if (ImGui.Button("Add action##newCtOutfitBtn"))
                {
                    var design = designs[ctOutfitDesignIndex];
                    ctDraftActions.Add(new CustomTriggerAction { Kind = CustomTriggerActionKind.Outfit, OutfitDesignId = design.DesignId, OutfitDesignName = design.Name });
                }
                break;

            case CustomTriggerActionKind.Gesture:
                if (ImGui.Button(ctSelectedGesture is null ? "Choose animation...##newCtGesture" : $"Change animation... ({ctSelectedGesture.Label})##newCtGesture"))
                    plugin.AnimationPickerWindow.Open(entry => ctSelectedGesture = entry);
                using (ImRaii.Disabled(ctSelectedGesture is null))
                {
                    if (ImGui.Button("Add action##newCtGestureBtn") && ctSelectedGesture is { } chosenGesture)
                    {
                        ctDraftActions.Add(new CustomTriggerAction { Kind = CustomTriggerActionKind.Gesture, GestureId = chosenGesture.Id, GestureAnimationName = chosenGesture.AnimationName });
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
                if (ImGui.Button("Add action##newCtMoodleBtn"))
                {
                    var status = statuses[ctMoodleStatusIndex];
                    ctDraftActions.Add(new CustomTriggerAction { Kind = CustomTriggerActionKind.Moodle, MoodleStatusId = status.StatusId, MoodleStatusName = status.Name });
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
                if (ImGui.Button("Add action##newCtRestraintBtn"))
                {
                    var device = devices[ctRestraintDeviceIndex];
                    ctDraftActions.Add(new CustomTriggerAction { Kind = CustomTriggerActionKind.Restraint, RestraintDeviceId = device.Id, RestraintDeviceName = device.Name });
                }
                break;

            case CustomTriggerActionKind.Chat:
                ImGui.InputText("Message##newCtChat", ref ctChatText, 400);
                IconGlyph.HelpMarker("Sent exactly as typed, unmodified - start it with a slash command (e.g. /sit) or a channel prefix (e.g. /p) to use those instead of your default chat channel. Needs the Custom chat messages permission and its own acknowledgement in Settings - see the README's Automation risk section.");
                using (ImRaii.Disabled(ctChatText.Trim().Length == 0))
                {
                    if (ImGui.Button("Add action##newCtChatBtn"))
                    {
                        ctDraftActions.Add(new CustomTriggerAction { Kind = CustomTriggerActionKind.Chat, ChatText = ctChatText });
                        ctChatText = "";
                    }
                }
                break;
        }

        ImGui.Spacing();
        ImGui.Separator();
        using (ImRaii.Disabled(ctNewAlias.Length == 0 || IsReserved(ctNewAlias) || ctDraftActions.Count == 0))
        {
            if (ImGui.Button("Save trigger"))
            {
                triggers.Add(new CustomTriggerDefinition { Alias = ctNewAlias, Actions = new List<CustomTriggerAction>(ctDraftActions) });
                config.Save();
                ctNewAlias = "";
                ctDraftActions.Clear();
            }
        }
    }

    private static string SummarizeCustomTriggerAction(CustomTriggerAction a) => CustomTriggerCommand.Summarize(a);

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
            IconGlyph.WrappedColored(Theme.Danger, "Locked - applied at pairing. Only /collarpanic (your safeword) or your Owner's \"collar unlock\" releases it.");
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
            IconGlyph.HelpMarker("Optional. Applied alongside your collar item when it locks, and periodically re-asserted for as long as the collar stays locked - removing it through Moodles' own UI won't make it stick. Cleared only by /collarpanic or your Owner's \"collar unlock\", the same as the collar item itself.");

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
        DrawOwnerSection("Custom Trigger (ad-hoc)##ownerCustomTrigger", () => DrawCustomTriggerQuickSection(canSend));
        DrawOwnerSection($"Alias / one-off ({quick.Aliases.Count} saved)##ownerAlias", () => DrawFreeformComposer(canSend));
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
                    importResult = result.Error ?? (result.TotalAdded == 0
                        ? "Nothing new - everything in that file was already imported."
                        : $"Imported {result.TotalAdded} new command(s): {result.Wardrobe} outfit, {result.Gesture} gesture, {result.Moodles} moodles, {result.Restraints} restraint, {result.Aliases} alias.");
                    resetImportsResult = null;
                }
                catch (Exception ex)
                {
                    importResult = $"Import failed: {ex.Message}";
                }
            });
        }

        ImGui.SameLine();

        /// collar/catalog-sync "Owner can reset every import to a blank slate": clears the five
        /// import-populated lists, leaving Title/Follow (hand-built, never import-populated) untouched -
        /// distinct from any single category's own "Clear all". Aliases shares one list between imported
        /// entries and anything the Owner typed manually into the freeform "Alias / one-off" control, so
        /// resetting clears that entire list too - the same coarse whole-list reset already accepted for
        /// Restraints' manually-added entries (see collar/catalog-sync's spec).
        if (ImGui.Button(resetLabel))
        {
            var quick = plugin.Configuration.QuickCommands;
            quick.Outfits.Clear();
            quick.Gestures.Clear();
            quick.Moodles.Clear();
            quick.Restraints.Clear();
            quick.Aliases.Clear();
            plugin.Configuration.Save();
            expandedRestraintRuleEditors.Clear();
            restraintRuleEdits.Clear();
            resetImportsResult = "All imports reset to a blank slate.";
            importResult = null;
        }
        IconGlyph.HelpMarker("Clears every import-populated quick-command list (Outfit, Gesture, Moodles, Restraints, Alias/one-off) back to empty - including any one-off commands you typed by hand, since imported aliases share that same list. Title and Leash commands you built by hand are untouched.");

        if (importResult is not null)
        {
            var isError = importResult.StartsWith("Import failed", StringComparison.Ordinal) || importResult.Contains("doesn't look like", StringComparison.Ordinal) || importResult.Contains("is empty", StringComparison.Ordinal);
            IconGlyph.WrappedColored(isError ? Theme.Danger : Theme.Success, importResult);
        }
        if (resetImportsResult is not null)
            IconGlyph.WrappedColored(Theme.Success, resetImportsResult);
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

    private static void DrawOwnerSection(string label, Action draw, bool defaultOpen = false)
    {
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
                ImGui.BulletText(SummarizeCustomTriggerAction(ctqDraftActions[i]));
                ImGui.SameLine();
                if (ImGui.SmallButton("Remove"))
                {
                    ctqDraftActions.RemoveAt(i);
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

        switch (kind)
        {
            case CustomTriggerActionKind.Title:
                ImGui.SetNextItemWidth(220);
                ImGui.InputText("Text##ctqTitle", ref ctqTitleText, 64);
                ImGui.Checkbox("Prefix##ctqTitle", ref ctqTitleIsPrefix);
                ImGui.ColorEdit3("Color##ctqTitle", ref ctqTitleColor);
                using (ImRaii.Disabled(ctqTitleText.Trim().Length == 0))
                {
                    if (ImGui.SmallButton("Add##ctqTitleBtn"))
                    {
                        ctqDraftActions.Add(new CustomTriggerAction { Kind = CustomTriggerActionKind.Title, TitleText = ctqTitleText.Trim(), TitleIsPrefix = ctqTitleIsPrefix, TitleColor = ctqTitleColor });
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
                    if (ImGui.SmallButton("Add##ctqOutfitBtn"))
                    {
                        ctqDraftActions.Add(new CustomTriggerAction { Kind = CustomTriggerActionKind.Outfit, OutfitDesignName = ctqOutfitName.Trim() });
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
                    if (ImGui.SmallButton("Add##ctqGestureBtn"))
                    {
                        ctqDraftActions.Add(new CustomTriggerAction { Kind = CustomTriggerActionKind.Gesture, GestureAnimationName = ctqGestureName.Trim() });
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
                    if (ImGui.SmallButton("Add##ctqMoodleBtn"))
                    {
                        ctqDraftActions.Add(new CustomTriggerAction { Kind = CustomTriggerActionKind.Moodle, MoodleStatusName = ctqMoodleName.Trim() });
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
                    if (ImGui.SmallButton("Add##ctqRestraintBtn"))
                    {
                        ctqDraftActions.Add(new CustomTriggerAction { Kind = CustomTriggerActionKind.Restraint, RestraintDeviceName = ctqRestraintName.Trim() });
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
                    if (ImGui.SmallButton("Add##ctqChatBtn"))
                    {
                        ctqDraftActions.Add(new CustomTriggerAction { Kind = CustomTriggerActionKind.Chat, ChatText = ctqChatText });
                        ctqChatText = "";
                    }
                }
                break;
        }

        ImGui.Spacing();
        if (ctqLabel.Trim().Length > 0 && ctqDraftActions.Count > 0)
        {
            var command = CustomTriggerCommand.BuildCastCommand(ctqLabel.Trim(), ctqDraftActions);
            ImGui.TextUnformatted("Send this bundle:");
            ContinueRowOrWrap(ButtonWidth("Send"));
            DrawSendCopyButtons(command, canSend, "ctqCustomTrigger");
            if (ImGui.SmallButton("Clear bundle##ctq"))
            {
                ctqDraftActions.Clear();
                ctqLabel = "";
            }
        }
        else
        {
            IconGlyph.WrappedColored(Theme.Warning, "Give this bundle a label and at least one action before it can be sent.");
        }
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

    private static List<RestraintRuleAssignment> ToRules(RestraintRuleEditState edit)
    {
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
            rules.Add(new RestraintRuleAssignment { Kind = RestraintRuleKind.ArmsCuffed, AnimationId = edit.ArmsCuffedAnimationId });
        if (edit.LegsCuffed)
            rules.Add(new RestraintRuleAssignment { Kind = RestraintRuleKind.LegsCuffed, AnimationId = edit.LegsCuffedAnimationId });
        if (edit.FullBodyCuffed)
            rules.Add(new RestraintRuleAssignment { Kind = RestraintRuleKind.FullBodyCuffed, AnimationId = edit.FullBodyCuffedAnimationId });
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
        IconGlyph.Text(FontAwesomeIcon.Comment, "Alias / one-off");
        ImGui.TextWrapped("Type an alias your Sub told you about, or a one-off override. Add Command saves it as a one-click button below for reuse.");

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
        ContinueRowOrWrap(ButtonWidth("Favorited"));
        DrawFavoriteToggle(cmd, $"{cmd.Label}_{cmd.Command}");
        ContinueRowOrWrap(ButtonWidth("Send"));
        DrawSendCopyButtons(cmd.Command, canSend, $"{cmd.Label}_{cmd.Command}");
        ContinueRowOrWrap(ButtonWidth("Remove"));
        if (ImGui.SmallButton($"Remove##{cmd.Label}_{cmd.Command}"))
        {
            list.Remove(cmd);
            plugin.Configuration.Save();
        }
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

        using (ImRaii.Disabled(!canSend))
        {
            if (ImGui.SmallButton($"Send##{idSuffix}"))
                plugin.ChatSender.Send(composed);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(canSend ? composed : "No /tell target yet - pairing hasn't captured your Sub's name.");

        ContinueRowOrWrap(ButtonWidth("Copy"));
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
