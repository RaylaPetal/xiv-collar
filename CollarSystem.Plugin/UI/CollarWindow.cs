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
    private string gestureAliasSearch = "";
    private (string ModDirectory, string ModName, string EmoteName)? selectedAliasGesture;

    private string commandInput = "";
    private string newTitleQuickText = "";
    private string newFollowQuickText = "";
    private string? outfitImportError;
    private string? gestureImportError;

    private static readonly (string Id, FontAwesomeIcon Icon, string Tooltip)[] NavItems =
    [
        ("title", FontAwesomeIcon.Heading, "Title"),
        ("wardrobe", FontAwesomeIcon.Tshirt, "Wardrobe"),
        ("gesture", FontAwesomeIcon.TheaterMasks, "Gesture"),
        ("owner", FontAwesomeIcon.Crown, "Owner"),
        ("permissions", FontAwesomeIcon.ShieldAlt, "Permissions"),
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
        DrawPairingCard();
        ImGui.Spacing();

        if (NavBar.Draw(activeModule, NavItems) is { } clicked)
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
    private void DrawPairingCard()
    {
        var pending = plugin.ChatCommandListener.Pending;
        var pairing = plugin.Configuration.Pairing;
        var config = plugin.Configuration;
        var sameRoleWarning = pending is { } p && p.SenderRole == config.Role;
        var height = pending is not null
            ? (sameRoleWarning ? 130 : 100)
            : pairing.IsPaired ? (config.Role == PluginRole.Owner ? 90 : 70) : 90;
        using var card = Card.Begin("pairingCard", new Vector2(0, height));

        if (pending is { } request)
        {
            var roleLabel = request.SenderRole == PluginRole.Owner ? "your Owner" : "your Sub";
            IconGlyph.WrappedColored(Theme.Warning, $"Pairing request from {request.Name}@{request.World} (code matched) - they say they'll be {roleLabel}.");
            if (sameRoleWarning)
                IconGlyph.WrappedColored(Theme.Danger, $"You're both set to {config.Role} in Settings - one of you should switch, or nothing will ever trigger.");

            if (ImGui.Button("Accept"))
                plugin.ChatCommandListener.AcceptPending();
            IconGlyph.HelpMarker("Trusts this sender as your paired peer from now on. Locks pairing on if you're set to Sub (only /collarpanic, your safeword command, undoes it) - if you're set to Owner, Release pairing (below, once accepted) undoes it any time.");
            ImGui.SameLine();
            if (ImGui.Button("Reject"))
                plugin.ChatCommandListener.DismissPending();
            return;
        }

        if (!pairing.IsPaired)
        {
            IconGlyph.WrappedColored(Theme.Warning, "Not paired - set your codes and send the pairing message in Settings (gear icon).");
            return;
        }

        if (config.Role == PluginRole.Owner)
        {
            IconGlyph.WrappedColored(Theme.Success, $"Owns: {pairing.PeerName}@{pairing.PeerWorld}");
            if (ImGui.Button("Release pairing"))
                plugin.PairingCommand.ReleasePeer();
            IconGlyph.HelpMarker("Clears who you're paired with on your own client only - doesn't touch your Sub's plugin at all. Use this to fix a stale/wrong pairing or to free them up to pair with someone else.");
        }
        else
        {
            IconGlyph.WrappedColored(Theme.Success, $"Paired with: {pairing.PeerName}@{pairing.PeerWorld}");
            ImGui.TextDisabled("Locked - use /collarpanic (your safeword) to unpair.");
        }
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
            IconGlyph.WrappedColored(Theme.Warning, "Gesture/Follow require the ToS acknowledgement in Settings (gear icon) first.");

        using (ImRaii.Disabled(!config.TosAcknowledged))
        {
            if (ImGuiCheckbox("Gesture", permissions.Gesture, out var newGesture))
                SavePermission(() => permissions.Gesture = newGesture);
            IconGlyph.HelpMarker("Lets a paired Owner queue an emote via a trigger tell - it never plays automatically, you still confirm it in the Gesture tab.");

            if (ImGuiCheckbox("Follow / Leash (hardcore)", permissions.Follow, out var newFollow))
                SavePermission(() => permissions.Follow = newFollow);
            IconGlyph.HelpMarker("Lets a paired Owner lock your movement to follow them, blocking your own WASD input until released. Heavier automation footprint than the other three - see the README's Automation risk section.");
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
        IconGlyph.HelpMarker("Lock the design after applying so it can't be changed by other tools. The lock key itself is generated automatically - nothing to pick here.");
        DrawReservedWordWarning(newOutfitAlias);
        if (ImGui.Button("Add outfit alias") && newOutfitAlias.Length > 0 && !IsReserved(newOutfitAlias))
        {
            var design = designs[newOutfitDesignIndex];
            outfits.Add(new OutfitAliasDefinition
            {
                Alias = newOutfitAlias,
                DesignId = design.DesignId,
                DesignName = design.Name,
                Key = (uint)Random.Shared.Next(1, int.MaxValue),
                Locked = newOutfitLocked,
            });
            config.Save();
            newOutfitAlias = "";
        }
    }

    private void DrawGestureModule()
    {
        var config = plugin.Configuration;
        IconGlyph.Text(FontAwesomeIcon.TheaterMasks, "Gesture");
        ImGui.Separator();
        IconGlyph.WrappedDisabled("Mod folder allowlist and scanning live in Settings (gear icon). Define your gesture aliases below.");

        var gestures = config.Aliases.Gestures;
        for (var i = 0; i < gestures.Count; i++)
        {
            ImGui.PushID($"gesture_{i}");
            var g = gestures[i];
            ImGui.BulletText($"{g.Alias} -> {g.EmoteName} ({g.ModName})");
            ImGui.SameLine();
            if (ImGui.SmallButton("Remove"))
            {
                gestures.RemoveAt(i);
                config.Save();
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }

        ImGui.Spacing();
        var allOptions = config.GestureMapping.LocalCatalog.Values
            .SelectMany(e => e.EmoteNames.Select(emote => (e.ModDirectory, e.ModName, EmoteName: emote)))
            .ToList();
        if (allOptions.Count == 0)
        {
            ImGui.TextDisabled("No scanned/resolved gestures yet - rescan in Settings (gear icon) first.");
            return;
        }

        ImGui.InputText("Alias##newGesture", ref newGestureAlias, 32);
        IconGlyph.HelpMarker("Short word the Owner types after the trigger phrase to queue this gesture. It never fires automatically - you still confirm it below.");
        DrawReservedWordWarning(newGestureAlias);

        DrawGesturePicker(allOptions);

        var canAdd = newGestureAlias.Length > 0 && !IsReserved(newGestureAlias) && selectedAliasGesture is not null;
        using (ImRaii.Disabled(!canAdd))
        {
            if (ImGui.Button("Add gesture alias") && selectedAliasGesture is { } chosen)
            {
                gestures.Add(new GestureAliasDefinition
                {
                    Alias = newGestureAlias,
                    ModDirectory = chosen.ModDirectory,
                    ModName = chosen.ModName,
                    EmoteName = chosen.EmoteName,
                });
                config.Save();
                newGestureAlias = "";
                selectedAliasGesture = null;
            }
        }
        if (selectedAliasGesture is { } picked)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"Selected: {picked.EmoteName} ({picked.ModName})");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("Pending gesture prompts");
        IconGlyph.HelpMarker("A valid gesture trigger tell never plays automatically - it only queues here. Confirm plays the emote now (via ECommons chat automation); Dismiss discards it silently.");
        foreach (var prompt in plugin.GestureCommand.PendingPrompts.ToArray())
        {
            ImGui.PushID(prompt.Id);
            ImGui.TextUnformatted($"{prompt.EmoteName} ({prompt.ModName})");
            ImGui.SameLine();
            if (ImGui.Button("Confirm"))
                plugin.GestureCommand.ConfirmAndTrigger(prompt.Id);
            ImGui.SameLine();
            if (ImGui.Button("Dismiss"))
                plugin.GestureCommand.DismissPrompt(prompt.Id);
            ImGui.PopID();
        }
    }

    /// Grouped by mod with a search/filter box, instead of one flat "Emote (ModName)" combo - a folder
    /// with a lot of pose mods makes a flat list unusable to scan through. Filtering matches either the
    /// mod name or the emote name; each mod only renders (and only auto-expands) when it still has a
    /// match, so a search narrows the whole tree instead of just graying out non-matches.
    private void DrawGesturePicker(List<(string ModDirectory, string ModName, string EmoteName)> options)
    {
        ImGui.InputTextWithHint("##gestureSearch", "Search mods or emotes...", ref gestureAliasSearch, 64);

        var filter = gestureAliasSearch.Trim();
        var groups = options
            .Where(o => filter.Length == 0
                        || o.ModName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                        || o.EmoteName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .GroupBy(o => (o.ModDirectory, o.ModName))
            .OrderBy(g => g.Key.ModName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        using var _ = ImRaii.Child("gesturePicker", new Vector2(0, 160), true);
        if (groups.Count == 0)
        {
            ImGui.TextDisabled("No matches.");
            return;
        }

        foreach (var group in groups)
        {
            var headerFlags = filter.Length > 0 ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
            if (!ImGui.CollapsingHeader($"{group.Key.ModName}##{group.Key.ModDirectory}", headerFlags))
                continue;

            foreach (var entry in group.OrderBy(e => e.EmoteName, StringComparer.OrdinalIgnoreCase))
            {
                var isSelected = selectedAliasGesture is { } sel && sel.ModDirectory == entry.ModDirectory && sel.EmoteName == entry.EmoteName;
                if (ImGui.Selectable($"  {entry.EmoteName}##{entry.ModDirectory}_{entry.EmoteName}", isSelected))
                    selectedAliasGesture = entry;
            }
        }
    }

    /// The Owner-facing tab. Title/Outfit/Gesture each get a one-click QuickCommand list - Outfit/Gesture
    /// auto-populate one button per imported name ("Add from clipboard"), Title is built one at a time
    /// since there's nothing to bulk-import for freeform text. Every button offers Send (ChatSender - one
    /// click, one /tell, disabled until pairing has captured a peer to address it to) alongside Copy
    /// (always available). The freeform box at the bottom covers a plain alias or a one-off not worth
    /// saving.
    private void DrawOwnerModule()
    {
        IconGlyph.Text(FontAwesomeIcon.Crown, "Owner - commands");
        ImGui.Separator();

        var pairing = plugin.Configuration.Pairing;
        var canSend = !string.IsNullOrWhiteSpace(pairing.PeerName) && !string.IsNullOrWhiteSpace(pairing.PeerWorld);
        if (!canSend)
            IconGlyph.WrappedColored(Theme.Warning, "No /tell target yet - Send is disabled until pairing captures your Sub's name (Settings' handshake). Copy still works any time.");

        DrawTitleQuickSection(canSend);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawOutfitQuickSection(canSend);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawGestureQuickSection(canSend);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawFollowQuickSection(canSend);
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawFreeformComposer(canSend);
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
        IconGlyph.Text(FontAwesomeIcon.Tshirt, "Outfit");
        var quick = plugin.Configuration.QuickCommands.Outfits;

        if (ImGui.SmallButton("Add from clipboard##outfitQuick"))
            outfitImportError = ImportQuickCommands(quick, name => $"outfit lock {name}");
        IconGlyph.HelpMarker("Paste your Sub's \"Copy names\" output (their Settings' Wardrobe scan card) - one ready-to-use button per name, no extra save step.");
        if (quick.Count > 0)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Clear all##outfitQuick"))
            {
                quick.Clear();
                outfitImportError = null;
                plugin.Configuration.Save();
            }
        }
        if (outfitImportError is not null)
            IconGlyph.WrappedColored(Theme.Warning, outfitImportError);

        DrawFixedQuickRow("Unlock outfit", "outfit unlock", canSend);

        if (quick.Count == 0)
        {
            ImGui.TextDisabled("No outfits imported yet.");
            return;
        }

        using var _ = ImRaii.Child("outfitQuickList", new Vector2(0, 120), true);
        foreach (var cmd in quick.ToArray())
            DrawSavedQuickRow(cmd, quick, canSend);
    }

    private void DrawGestureQuickSection(bool canSend)
    {
        IconGlyph.Text(FontAwesomeIcon.TheaterMasks, "Gesture");
        var quick = plugin.Configuration.QuickCommands.Gestures;

        if (ImGui.SmallButton("Add from clipboard##gestureQuick"))
            gestureImportError = ImportQuickCommands(quick, name => $"gesture {name}");
        IconGlyph.HelpMarker("Paste your Sub's \"Copy names\" output (their Settings' Gesture scan card). Sending still only ever queues it on their end - your Sub confirms it themselves before it plays, same as always.");
        if (quick.Count > 0)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Clear all##gestureQuick"))
            {
                quick.Clear();
                gestureImportError = null;
                plugin.Configuration.Save();
            }
        }
        if (gestureImportError is not null)
            IconGlyph.WrappedColored(Theme.Warning, gestureImportError);

        if (quick.Count == 0)
        {
            ImGui.TextDisabled("No gestures imported yet.");
            return;
        }

        using var _ = ImRaii.Child("gestureQuickList", new Vector2(0, 120), true);
        foreach (var cmd in quick.ToArray())
            DrawSavedQuickRow(cmd, quick, canSend);
    }

    /// Follow has no reserved-keyword override the way Title/Outfit/Gesture do (ChatCommandListener never
    /// added a "force follow" - it's always a plain alias), so there's nothing to auto-populate from a
    /// scan. "leash-on"/"leash-off" are AliasBook's own defaults, shown as ready-to-use fixed rows so
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
        IconGlyph.HelpMarker("Follow has no direct-override syntax - this saves a plain alias word, exactly what your Sub set as their engage/release alias in their own Settings (default \"leash-on\"/\"leash-off\" unless they changed it).");

        if (quick.Count == 0)
        {
            DrawFixedQuickRow("Leash on (default)", "leash-on", canSend);
            DrawFixedQuickRow("Leash off (default)", "leash-off", canSend);
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
        ImGui.SameLine();
        DrawSendCopyButtons(command, canSend, $"fixed_{label}");
    }

    private void DrawSavedQuickRow(QuickCommand cmd, List<QuickCommand> list, bool canSend)
    {
        ImGui.TextUnformatted(cmd.Label);
        ImGui.SameLine();
        DrawSendCopyButtons(cmd.Command, canSend, $"{cmd.Label}_{cmd.Command}");
        ImGui.SameLine();
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

        ImGui.SameLine();
        if (ImGui.SmallButton($"Copy##{idSuffix}"))
            ImGui.SetClipboardText(composed);
    }

    /// Guards against pasting something that isn't actually a name list (a Discord message, a URL, code, a
    /// whole document pasted by accident) - this is a local convenience list only, but garbage entries
    /// would still clutter the picker with buttons that never match anything on the Sub's side. Rejects
    /// the whole paste rather than silently keeping only the "valid-looking" lines, since a rejection is
    /// far more noticeable than a list that's quietly missing half its entries. Returns an error message
    /// to show, or null on success. `toCommand` builds the actual override command from each imported
    /// name (e.g. "outfit lock <name>"), so every imported entry is immediately a ready one-click button.
    private string? ImportQuickCommands(List<QuickCommand> target, Func<string, string> toCommand)
    {
        var text = ImGui.GetClipboardText();
        if (string.IsNullOrWhiteSpace(text))
            return "Clipboard is empty - nothing to import.";

        var lines = text.Split('\n')
            .Select(line => line.Trim().TrimStart('-', '*', '•').Trim())
            .Where(line => line.Length > 0)
            .ToList();

        if (lines.Count == 0)
            return "Clipboard is empty - nothing to import.";
        if (lines.Count > 300)
            return "That's way more lines than a scan result would have - doesn't look like a name list. Nothing imported.";

        var badLine = lines.FirstOrDefault(line =>
            line.Length > 80 ||
            line.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("https://", StringComparison.OrdinalIgnoreCase) ||
            line.IndexOfAny(['{', '}', ';', '<', '>', '\t']) >= 0);
        if (badLine is not null)
            return $"\"{(badLine.Length > 40 ? badLine[..40] + "..." : badLine)}\" doesn't look like a design/emote name - doesn't look like a name list. Nothing imported.";

        var addedCount = 0;
        foreach (var line in lines)
        {
            var command = toCommand(line);
            if (!target.Any(existing => string.Equals(existing.Command, command, StringComparison.OrdinalIgnoreCase)))
            {
                target.Add(new QuickCommand { Label = line, Command = command });
                addedCount++;
            }
        }

        target.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));
        plugin.Configuration.Save();
        return addedCount == 0 ? "Nothing new - all of those were already imported." : null;
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
