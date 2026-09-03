using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CollarSystem.Plugin.Commands;
using CollarSystem.Plugin.Config;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

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
    /// How long a local Test control's result stays visible before auto-clearing (collar/ui-organization).
    private const long TestResultDisplayMs = 4_000;

    private readonly Plugin plugin;
    private string activeModule = "title";

    private string newTitleAlias = "";
    private string newTitleText = "";
    private bool newTitleIsPrefix;
    private Vector3 newTitleColor = new(1, 1, 1);

    private string newOutfitAlias = "";
    private int newOutfitDesignIndex;
    private bool newOutfitLocked = true;

    private string newGestureAlias = "";
    private GestureCatalogEntry? selectedAliasGesture;

    private string newDeviceName = "";
    private int newDeviceDesignIndex;
    private bool newDeviceForcedPose;
    private int newDevicePoseIndex;
    private bool newDeviceWalkOnly;
    private bool newDeviceActionBlock;
    private bool newDeviceGagChat;

    private string newRestraintAlias = "";
    private int newRestraintDeviceIndex;

    private static readonly string[] PoseNames = ["Ground Sit", "Sit", "Doze"];

    private string commandInput = "";
    private string newTitleQuickText = "";
    private string newFollowQuickText = "";
    private string? importResult;
    private string? resetImportsResult;
    private bool revealSafeword;

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
    }

    /// Transient, session-only per-action local Test feedback (collar/ui-organization) - never saved,
    /// keyed by a stable per-row id so each row's last result shows independently of the others. Each
    /// entry auto-clears a short time after being shown (see DrawTestButton).
    private readonly Dictionary<string, (LocalTestResult Result, long ShownAtTicks)> testResults = new();

    private static readonly (string Id, FontAwesomeIcon Icon, string Tooltip)[] NavItems =
    [
        ("title", FontAwesomeIcon.Heading, "Title"),
        ("wardrobe", FontAwesomeIcon.Tshirt, "Wardrobe"),
        ("gesture", FontAwesomeIcon.TheaterMasks, "Gesture"),
        ("restraints", FontAwesomeIcon.Handcuffs, "Restraints"),
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
            case "restraints":
                DrawRestraintsModule();
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
            IconGlyph.HelpMarker("Lets a paired Owner apply or release a restraint device via a trigger tell. Restraint devices can suppress movement, force walking, block actions, or garble your outgoing chat while active - the gag chat rule rewrites content you actually typed, a heavier automation footprint than the other three - see the Restraints tab and the README's Automation risk section.");
        }

        ImGui.Spacing();
        if (ImGuiCheckbox("Collar", permissions.Collar, out var newCollar))
            SavePermission(() => permissions.Collar = newCollar);
        IconGlyph.HelpMarker("Lets your configured collar item apply and lock automatically when you accept a pairing (Collar tab). Configuring an item alone does nothing without this enabled too.");

        if (ImGuiCheckbox("Moodles", permissions.Moodles, out var newMoodles))
            SavePermission(() => permissions.Moodles = newMoodles);
        IconGlyph.HelpMarker("Lets a paired Owner apply or clear a Moodle (status effect) from your own registered statuses via a trigger tell - applies immediately, no confirmation queue.");
    }

    private void DrawTitleModule()
    {
        var config = plugin.Configuration;
        IconGlyph.Text(FontAwesomeIcon.Heading, "Title Aliases");
        ImGui.Separator();

        DrawClearAliasField("Clear-title alias", () => config.Aliases.ClearTitleAlias, v => config.Aliases.ClearTitleAlias = v, config);
        IconGlyph.HelpMarker("The alias that removes your current Honorific title when triggered - separate from the named aliases below, which each apply a specific title.");
        DrawTestButton("titleClear", "Test Clear", plugin.LocalTestCoordinator.TestTitleClear);
        IconGlyph.HelpMarker("Locally clears your title right now, the same way an accepted Owner's clear-title alias would - no pairing or chat involved.");

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
            ImGui.SameLine();
            DrawTestButton($"title_{t.Alias}", "Test Apply", () => plugin.LocalTestCoordinator.TestTitleApply(t));
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
        DrawTestButton("outfitUnlock", "Test Unlock", plugin.LocalTestCoordinator.TestOutfitUnlock);
        IconGlyph.HelpMarker("Locally unlocks your outfit right now, the same way an accepted Owner's unlock alias would - no pairing or chat involved.");

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
            ImGui.SameLine();
            DrawTestButton($"outfit_{o.Alias}", "Test Apply", () => plugin.LocalTestCoordinator.TestOutfitApply(o));
            ImGui.PopID();
        }

        ImGui.Spacing();
        var designs = config.WardrobeMapping.LocalDesigns.Values.ToList();
        if (designs.Count == 0)
        {
            ImGui.TextDisabled("No scanned designs yet - rescan in Settings (gear icon) first.");
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

    /// collar/restraints: tag designs from Restraints' own independent scan (Settings - separate folder
    /// allowlist from collar/outfit's Wardrobe scan, since bondage/restriction-themed designs and everyday
    /// outfits live in different Glamourer folders in practice) as restraint devices carrying restriction
    /// rules, then create Sub-alias entries that toggle them (RestraintCommand.Toggle). Untagged designs
    /// never appear here - only tagged devices and their aliases do, mirroring Outfit's design-then-alias
    /// flow.
    private void DrawRestraintsModule()
    {
        var config = plugin.Configuration;
        IconGlyph.Text(FontAwesomeIcon.Handcuffs, "Restraints");
        ImGui.Separator();
        IconGlyph.WrappedDisabled("Tag designs from Restraints' own scan (Settings gear icon - separate folder allowlist from Wardrobe) as restraint devices carrying restriction rules, then alias them below so an Owner - or your own alias - can apply/release them.");

        var devices = config.RestraintMapping.Devices.Values.ToList();
        if (devices.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextUnformatted("Tagged devices");
            foreach (var device in devices)
            {
                ImGui.PushID($"device_{device.Id}");
                var active = plugin.RestraintCommand.IsActive(device.Id);
                var ruleSummary = string.Join(", ", device.Rules.Select(r => r.Kind switch
                {
                    RestraintRuleKind.ForcedPose => $"forced pose ({PoseName(r.PoseModeId)})",
                    RestraintRuleKind.WalkOnly => "walk-only",
                    RestraintRuleKind.ActionBlock => "action-block",
                    RestraintRuleKind.GagChat => "gag chat",
                    _ => r.Kind.ToString(),
                }));
                ImGui.BulletText($"{device.Name}{(active ? " (active)" : "")} - {ruleSummary}");
                ImGui.SameLine();
                if (ImGui.SmallButton("Untag"))
                {
                    plugin.RestraintCommand.UntagDevice(device.Id);
                    ImGui.PopID();
                    break;
                }
                ImGui.PopID();
            }
        }

        ImGui.Spacing();
        var designs = plugin.RestraintCommand.ScannedDesigns();
        if (designs.Count == 0)
        {
            ImGui.TextDisabled("No scanned designs yet - rescan Restraints in Settings (gear icon) first.");
        }
        else
        {
            ImGui.TextUnformatted("Tag a new device");
            var designNames = designs.Select(d => d.Name).ToArray();
            newDeviceDesignIndex = Math.Clamp(newDeviceDesignIndex, 0, designNames.Length - 1);
            ImGui.InputText("Device name##newDevice", ref newDeviceName, 32);
            ImGui.Combo("Design##newDevice", ref newDeviceDesignIndex, designNames, designNames.Length);
            IconGlyph.HelpMarker("Which scanned Glamourer design this device applies when engaged - its equipment slots lock the same way a locked Outfit alias's do.");

            ImGui.Checkbox("Forced pose##newDevice", ref newDeviceForcedPose);
            IconGlyph.HelpMarker("Places you into the chosen pose and fully blocks movement input until released.");
            if (newDeviceForcedPose)
                ImGui.Combo("Pose##newDevice", ref newDevicePoseIndex, PoseNames, PoseNames.Length);

            ImGui.Checkbox("Walk-only##newDevice", ref newDeviceWalkOnly);
            IconGlyph.HelpMarker("Forces walking and blocks running, without blocking directional movement input.");

            ImGui.Checkbox("Action block##newDevice", ref newDeviceActionBlock);
            IconGlyph.HelpMarker("Blocks hotbar action/skill usage until released, without affecting movement.");

            ImGui.Checkbox("Gag chat##newDevice", ref newDeviceGagChat);
            IconGlyph.HelpMarker("Garbles your outgoing chat text - the actual transmitted message, not just your own display - until released. See the README's Automation risk section before enabling.");

            var hasAnyRule = newDeviceForcedPose || newDeviceWalkOnly || newDeviceActionBlock || newDeviceGagChat;
            if (ImGui.Button("Tag device") && newDeviceName.Length > 0 && hasAnyRule)
            {
                var design = designs[newDeviceDesignIndex];
                var rules = new List<RestraintRuleAssignment>();
                if (newDeviceForcedPose)
                    rules.Add(new RestraintRuleAssignment { Kind = RestraintRuleKind.ForcedPose, PoseModeId = newDevicePoseIndex + 1 });
                if (newDeviceWalkOnly)
                    rules.Add(new RestraintRuleAssignment { Kind = RestraintRuleKind.WalkOnly });
                if (newDeviceActionBlock)
                    rules.Add(new RestraintRuleAssignment { Kind = RestraintRuleKind.ActionBlock });
                if (newDeviceGagChat)
                    rules.Add(new RestraintRuleAssignment { Kind = RestraintRuleKind.GagChat });

                plugin.RestraintCommand.TagDevice(null, design.DesignId, newDeviceName, rules);
                newDeviceName = "";
                newDeviceForcedPose = newDeviceWalkOnly = newDeviceActionBlock = newDeviceGagChat = false;
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

    private void DrawGestureModule()
    {
        var config = plugin.Configuration;
        IconGlyph.Text(FontAwesomeIcon.TheaterMasks, "Gesture");
        ImGui.Separator();
        IconGlyph.WrappedDisabled("Select animation mods and scan them in Settings, then define aliases from their named options here.");

        var gestures = config.Aliases.Gestures;
        var removeGestureIndex = -1;
        if (gestures.Count > 0 && ImGui.BeginTable("gestureAliases", 3,
                ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH))
        {
            ImGui.TableSetupColumn("Animation", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 72);
            ImGui.TableSetupColumn("Test", ImGuiTableColumnFlags.WidthFixed, 220);
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

                ImGui.TableSetColumnIndex(2);
                using (ImRaii.Disabled(invalid))
                    DrawTestButton($"gesture_{g.Alias}", "Test Play", () => plugin.LocalTestCoordinator.TestGesturePlay(g));
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
            ImGui.TextDisabled("No scanned/resolved gestures yet - rescan in Settings (gear icon) first.");
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
            ImGui.TextDisabled($"Selected: {picked.Label}");
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
            ImGui.TextDisabled("No collar configured yet.");

        if (locked)
        {
            ImGui.TextColored(Theme.Danger, "Locked - applied at pairing. Only /collarpanic (your safeword) or your Owner's \"collar unlock\" releases it.");
        }

        using (ImRaii.Disabled(locked))
        {
            if (ImGui.Button("Save Collar"))
            {
                if (!plugin.CollarCommand.CaptureCurrentAsCollar())
                    Plugin.Log.Warning("Could not capture the current Neck item - is anything equipped there?");
            }
            IconGlyph.HelpMarker("Reads whatever's currently in your own Neck slot via Glamourer and saves it as your collar - never a manually typed item id.");

            if (config.Collar.IsConfigured)
            {
                ImGui.SameLine();
                if (ImGui.Button("Clear"))
                    plugin.CollarCommand.ClearConfiguredCollar();
            }
        }

        ImGui.Spacing();
        DrawTestButton("collarLock", "Test Lock", plugin.LocalTestCoordinator.TestCollarLock);
        IconGlyph.HelpMarker("Locally applies and locks your configured collar right now, the same way an accepted Owner's \"collar lock\" would - no pairing or chat involved.");
        ImGui.SameLine();
        DrawTestButton("collarUnlock", "Test Unlock", plugin.LocalTestCoordinator.TestCollarUnlock);
        IconGlyph.HelpMarker("Locally releases the collar lock, the same way an accepted Owner's \"collar unlock\" would.");

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

        ImGui.Spacing();
        DrawTestButton("leashEngage", "Test Engage", plugin.LocalTestCoordinator.TestLeashEngage);
        IconGlyph.HelpMarker("Locally engages the movement lock right now, the same way an accepted Owner's leash trigger would - blocks your own WASD input until released. Requires Follow / Leash permission and the automation-risk acknowledgement (Settings).");
        ImGui.SameLine();
        DrawTestButton("leashRelease", "Test Release", plugin.LocalTestCoordinator.TestLeashRelease);
        IconGlyph.HelpMarker("Locally releases the movement lock, the same way an accepted Owner's unleash trigger would.");
    }

    /// Best-effort display name for a raw Glamourer item id via Lumina's own Item sheet - falls back to
    /// the numeric id for sentinel/special values (e.g. "nothing equipped") that don't resolve to a real
    /// row, same "don't crash, just show something" spirit as GlamourerIpc.GetCurrentNeckItem.
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
        var canSend = !string.IsNullOrWhiteSpace(pairing.PeerName) && !string.IsNullOrWhiteSpace(pairing.PeerWorld);
        if (!canSend)
            IconGlyph.WrappedColored(Theme.Warning, "No /tell target yet - Send is disabled until pairing captures your Sub's name (Settings' handshake). Copy still works any time.");

        var quick = plugin.Configuration.QuickCommands;
        DrawOwnerSection($"Title ({quick.Titles.Count} saved)##ownerTitle", () => DrawTitleQuickSection(canSend), defaultOpen: true);
        DrawOwnerSection($"Outfit ({quick.Outfits.Count} imported)##ownerOutfit", () => DrawOutfitQuickSection(canSend));
        DrawOwnerSection($"Gesture ({quick.Gestures.Count} imported)##ownerGesture", () => DrawGestureQuickSection(canSend));
        DrawOwnerSection($"Leash ({quick.Follow.Count} saved)##ownerLeash", () => DrawFollowQuickSection(canSend));
        DrawOwnerSection("Collar (2 actions)##ownerCollar", () => DrawCollarQuickSection(canSend));
        DrawOwnerSection($"Moodles ({quick.Moodles.Count} imported)##ownerMoodles", () => DrawMoodlesQuickSection(canSend));
        DrawOwnerSection($"Restraints ({quick.Restraints.Count} imported)##ownerRestraints", () => DrawRestraintQuickSection(canSend));
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
                        : $"Imported {result.TotalAdded} new command(s): {result.Wardrobe} outfit, {result.Gesture} gesture, {result.Moodles} moodles, {result.Restraints} restraint.");
                    resetImportsResult = null;
                }
                catch (Exception ex)
                {
                    importResult = $"Import failed: {ex.Message}";
                }
            });
        }

        ImGui.SameLine();

        /// collar/catalog-sync "Owner can reset every import to a blank slate": clears only the four
        /// import-populated lists, leaving Titles/Follow/Aliases (hand-built, never import-populated)
        /// untouched - distinct from any single category's own "Clear all".
        if (ImGui.Button(resetLabel))
        {
            var quick = plugin.Configuration.QuickCommands;
            quick.Outfits.Clear();
            quick.Gestures.Clear();
            quick.Moodles.Clear();
            quick.Restraints.Clear();
            plugin.Configuration.Save();
            expandedRestraintRuleEditors.Clear();
            restraintRuleEdits.Clear();
            resetImportsResult = "All imports reset to a blank slate.";
            importResult = null;
        }
        IconGlyph.HelpMarker("Clears every import-populated quick-command list (Outfit, Gesture, Moodles, Restraints) back to empty. Title, Leash, and Alias commands you built by hand are untouched.");

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
            ImGui.TextDisabled("No Moodles statuses imported yet - use \"Import commands\" above.");
            return;
        }

        using var _ = ImRaii.Child("moodlesQuickList", new Vector2(0, 120), true);
        foreach (var cmd in quick.ToArray())
            DrawSavedQuickRow(cmd, quick, canSend);
    }

    /// collar/restraints: Owner-tab quick commands - no per-category import button here (collar/catalog-
    /// sync's unified "Import commands" is the only way to populate this list; see DrawOwnerModule). Every
    /// imported entry starts with no rules assigned (RestraintCommand.ExportNames now exports every
    /// scanned design, tagged or not), so each row exposes a "Configure rules" editor - the same rule set
    /// DrawRestraintsModule's "Tag a new device" section uses - and Send/Copy stay disabled until at least
    /// one rule is assigned (collar/restraints "Owner quick command with no rules assigned yet").
    private void DrawRestraintQuickSection(bool canSend)
    {
        var quick = plugin.Configuration.QuickCommands.Restraints;
        DrawSectionTitleRow(FontAwesomeIcon.Handcuffs, "Restraints", quick.Count > 0, "restraintsQuick", () =>
        {
            quick.Clear();
            plugin.Configuration.Save();
        });

        DrawFixedQuickRow("Restraint unlock", "restraint unlock", canSend);
        IconGlyph.HelpMarker("Force-releases every active restraint device and clears the force-lock, the same as your Sub's panic would for restraints specifically.");

        if (quick.Count == 0)
        {
            ImGui.TextDisabled("No restraint devices imported yet - use \"Import commands\" above.");
            return;
        }

        using var _ = ImRaii.Child("restraintsQuickList", new Vector2(0, 260), true);
        foreach (var cmd in quick.ToArray())
            DrawRestraintQuickRow(cmd, quick, canSend);
    }

    private void DrawRestraintQuickRow(QuickCommand cmd, List<QuickCommand> list, bool canSend)
    {
        ImGui.PushID($"restraintQuick_{cmd.Label}");
        var hasRules = cmd.RestraintRules is { Count: > 0 };

        ImGui.TextUnformatted(cmd.Label);
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
            ImGui.Checkbox("Forced pose##restraintQuickRule", ref edit.ForcedPose);
            if (edit.ForcedPose)
                ImGui.Combo("Pose##restraintQuickRule", ref edit.PoseIndex, PoseNames, PoseNames.Length);
            ImGui.Checkbox("Walk-only##restraintQuickRule", ref edit.WalkOnly);
            ImGui.Checkbox("Action block##restraintQuickRule", ref edit.ActionBlock);
            ImGui.Checkbox("Gag chat##restraintQuickRule", ref edit.GagChat);

            var hasAnyRule = edit.ForcedPose || edit.WalkOnly || edit.ActionBlock || edit.GagChat;
            using (ImRaii.Disabled(!hasAnyRule))
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
        return rules;
    }

    private void DrawTitleQuickSection(bool canSend)
    {
        IconGlyph.Text(FontAwesomeIcon.Heading, "Title");
        var quick = plugin.Configuration.QuickCommands.Titles;

        ImGui.SetNextItemWidth(220);
        ImGui.InputText("##newQuickTitle", ref newTitleQuickText, 64);
        ImGui.SameLine();
        if (ImGui.SmallButton("Add Command##quickTitle") && newTitleQuickText.Trim().Length > 0)
        {
            var text = newTitleQuickText.Trim();
            quick.Add(new QuickCommand { Label = text, Command = $"title create {text}" });
            plugin.Configuration.Save();
            newTitleQuickText = "";
        }
        IconGlyph.HelpMarker("Saves a one-click button that force-applies this exact title and locks it on - your Sub's own clear-title alias is refused while it's locked, only the \"Clear title\" button below (or their panic) releases it.");

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
            ImGui.TextDisabled("No outfits imported yet - use \"Import commands\" above.");
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
            ImGui.TextDisabled("No gestures imported yet - use \"Import commands\" above.");
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

        ImGui.TextDisabled($"{visible.Count} shown / {quick.Count} imported");

        using var _ = ImRaii.Child("gestureQuickList", new Vector2(0, 260), true);
        if (visible.Count == 0)
        {
            ImGui.TextDisabled("No gestures match this search.");
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
            ImGui.TextDisabled("Defaults shown above - add your own if your Sub customized their alias words.");
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

    private void DrawSavedQuickRow(QuickCommand cmd, List<QuickCommand> list, bool canSend)
    {
        ImGui.TextUnformatted(cmd.Label);
        ContinueRowOrWrap(ButtonWidth("Send"));
        DrawSendCopyButtons(cmd.Command, canSend, $"{cmd.Label}_{cmd.Command}");
        ContinueRowOrWrap(ButtonWidth("Remove"));
        if (ImGui.SmallButton($"Remove##{cmd.Label}_{cmd.Command}"))
        {
            list.Remove(cmd);
            plugin.Configuration.Save();
        }
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

    /// A local pre-pair Test control (collar/ui-organization) - action-specific label (e.g. "Test Lock")
    /// so it can never be mistaken for an Owner-send control and its effect is clear without hovering a
    /// tooltip, dispatches through LocalTestCoordinator (same local action path an accepted Owner command
    /// would use, no pairing/chat involved), and shows only its own last result, which clears itself
    /// automatically a few seconds after being shown. Hidden entirely when HideTestControls is enabled.
    private void DrawTestButton(string key, string label, Func<LocalTestResult> run)
    {
        if (plugin.Configuration.HideTestControls)
            return;

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
