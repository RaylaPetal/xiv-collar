using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Oathbound.Plugin.Config;
using Oathbound.Plugin.Commands;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility.Raii;

namespace Oathbound.Plugin.UI;

/// collar/ui-organization "Compact favorites window lists only favorited commands" (reworked): replaces
/// the former `FavoritesWindow` with a two-level ImGui popup (design.md's "Quick-access menu is an ImGui
/// popup, not a window") - opened by both the DTR bar entry and the on-screen `FavoritesBarButton`, so it
/// lives as its own static-ish helper rather than a `Window` subclass.
///
/// `Toggle()` is called from two different contexts (a DTR bar click callback, which Dalamud can invoke
/// outside any ImGui frame entirely, and the on-screen button's own click inside its Draw()) - it MUST
/// NOT call any ImGui popup API (OpenPopup/BeginPopup/IsPopupOpen) directly. Every one of those is scoped
/// to Dear ImGui's "current window" at the time of the call (its ID is hashed together with whatever
/// window happens to be current), so calling them from mismatched contexts computes mismatched popup IDs
/// - which was the actual cause of the open flicker and the popup appearing in the wrong place - and
/// calling them when there is no current ImGui frame/window at all (as a DTR click callback can do)
/// dereferences invalid internal ImGui state, which is what was crashing the game on click. `Toggle()`
/// therefore only flips a plain flag; every real ImGui popup call happens inside `Draw()`, which is always
/// invoked from the exact same place every frame (`FavoritesBarButton.Draw()`), so Open/Begin are always
/// called from one consistent, always-valid context.
public static class QuickAccessMenu
{
    private const string PopupId = "CollarQuickAccessMenu";
    private static bool openRequested;
    private static bool closeRequested;

    public static void Toggle()
    {
        if (closeRequested || (!openRequested && IsLikelyOpen))
            closeRequested = true;
        else
            openRequested = true;
    }

    /// Best-effort only - real open/closed state is Dear ImGui's, only ever queried from inside `Draw()`
    /// where the ID-stack context is known-good; this just avoids re-requesting an open that's already
    /// pending/showing when Toggle() is called twice before a frame has run.
    private static bool IsLikelyOpen { get; set; }

    public static void Draw(Plugin plugin)
    {
        if (openRequested)
        {
            openRequested = false;
            ImGui.OpenPopup(PopupId);
        }

        // collar/ui-organization "Quick-access button and menu use the plugin's own theme": pushed for
        // the whole method (via `using` declarations, not blocks) so every return path - BeginPopup
        // failing, the closeRequested branch, and the normal fallthrough to EndPopup - pops them exactly
        // once, matching Card.cs's push/pop shape for the same Theme.CardBg/CardRounding pair.
        using var popupBg = ImRaii.PushColor(ImGuiCol.PopupBg, Theme.CardBg);
        using var popupRounding = ImRaii.PushStyle(ImGuiStyleVar.PopupRounding, Theme.CardRounding);
        using var headerHovered = ImRaii.PushColor(ImGuiCol.HeaderHovered, Theme.TileBgHover);

        // Anchors the popup to the on-screen button's own rect - explicit rather than relying on Dear
        // ImGui's default mouse-position popup placement, and pivoted so the menu grows away from
        // whichever screen edges the button sits against instead of potentially opening off-screen (the
        // bug report: menu appearing up near the top while the button sits at the bottom). Cheap to call
        // every frame - ImGuiCond.Appearing only actually applies it on the frame the popup opens.
        var buttonSettings = plugin.Configuration.FavoritesButton;
        var buttonPos = FavoritesBarButton.ComputePosition(buttonSettings);
        var isTop = buttonSettings.Corner is ScreenCorner.TopLeft or ScreenCorner.TopRight;
        var isLeft = buttonSettings.Corner is ScreenCorner.TopLeft or ScreenCorner.BottomLeft;
        var pivot = new Vector2(isLeft ? 0f : 1f, isTop ? 0f : 1f);
        var anchor = new Vector2(
            isLeft ? buttonPos.X : buttonPos.X + FavoritesBarButton.ButtonSize,
            isTop ? buttonPos.Y + FavoritesBarButton.ButtonSize : buttonPos.Y);
        ImGui.SetNextWindowPos(anchor, ImGuiCond.Appearing, pivot);

        if (!ImGui.BeginPopup(PopupId))
        {
            IsLikelyOpen = false;
            closeRequested = false;
            return;
        }

        IsLikelyOpen = true;

        if (closeRequested)
        {
            closeRequested = false;
            IsLikelyOpen = false;
            ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
            return;
        }

        // Owner quick commands are meant to be sent to a different, paired Sub - a character currently
        // configured as Sub has nothing to send them to (its own client never applies anything it sends
        // to itself, see ChatCommandListener.OnChatMessage's Role check), so the menu stays limited to the
        // plain open-window shortcuts below instead of exposing a Send list that would only do nothing.
        if (plugin.Configuration.Role == PluginRole.Owner)
        {
            var canSend = plugin.Configuration.Pairing.IsPaired;
            var favoritesByCategory = CategorizedFavorites(plugin.Configuration.QuickCommands);
            var teleportFavorited = plugin.Configuration.QuickCommands.FavoriteFixedActions.Contains(FixedActionIds.Teleport);

            if (favoritesByCategory.Count == 0 && !teleportFavorited)
            {
                ImGui.TextUnformatted("Nothing favorited yet");
            }
            else
            {
                foreach (var (label, favorites) in favoritesByCategory)
                {
                    if (!ImGui.BeginMenu($"{label} ({favorites.Count})"))
                        continue;

                    foreach (var cmd in favorites)
                        DrawFavoriteMenuItem(plugin, cmd, canSend);
                    ImGui.EndMenu();
                }

                // collar/ui-organization "Header includes a quick Teleport action": Teleport has no static
                // Command text to compose (it's resolved live at send time), so it can't join the synthetic
                // QuickCommand entries CategorizedFavorites builds for the other fixed actions - it gets its
                // own top-level entry instead of a category submenu.
                if (teleportFavorited)
                    DrawTeleportMenuItem(plugin, canSend);
            }
        }

        if (ImGui.MenuItem("Open main window"))
            plugin.OpenMainWindow();
        if (ImGui.MenuItem("Open settings"))
            plugin.ToggleSettingsUi();

        ImGui.EndPopup();
    }

    /// Built-in fixed-action rows that have static command text (everything except Teleport, which is
    /// resolved live - see DrawTeleportMenuItem) - collar/ui-organization "Owner can favorite ... built-in
    /// fixed-action row[s]". Grouped under the same category label its `DrawFixedQuickRow` call site lives
    /// under in CollarWindow, so a favorited "Collar lock" appears in the same submenu as any favorited
    /// Collar QuickCommand.
    private static readonly (string Id, string Label, string Category, string Command)[] FixedActions =
    [
        (FixedActionIds.CollarLock, "Collar lock", "Collar", "collar lock"),
        (FixedActionIds.CollarUnlock, "Collar unlock", "Collar", "collar unlock"),
        (FixedActionIds.ClearMoodle, "Clear moodle", "Moodles", "moodle clear"),
        (FixedActionIds.RestraintUnlock, "Restraint unlock", "Restraints", "restraint unlock"),
        (FixedActionIds.ClearTitle, "Clear title", "Title", "title clear"),
        (FixedActionIds.UnlockOutfit, "Unlock outfit", "Outfit", "outfit unlock"),
        (FixedActionIds.LeashDefault, "Leash (default)", "Follow", "leash"),
        (FixedActionIds.UnleashDefault, "Unleash (default)", "Follow", "unleash"),
    ];

    private static List<(string Label, List<QuickCommand> Favorites)> CategorizedFavorites(OwnerQuickCommands quick)
    {
        (string Label, List<QuickCommand> List)[] categories =
        [
            ("Title", quick.Titles),
            ("Outfit", quick.Outfits),
            ("Animation", quick.Gestures),
            ("Follow", quick.Follow),
            ("Moodles", quick.Moodles),
            ("Restraints", quick.Restraints),
            ("Custom Trigger Bundles", quick.Aliases),
        ];

        return categories
            .Select(c => (c.Label, Favorites: c.List.Where(cmd => cmd.IsFavorite)
                .Concat(FavoritedFixedActionsFor(c.Label, quick.FavoriteFixedActions))
                .OrderBy(cmd => cmd.Label, StringComparer.OrdinalIgnoreCase)
                .ToList()))
            .Where(c => c.Favorites.Count > 0)
            .ToList();
    }

    /// Synthesizes a plain `QuickCommand` (Label + Command only) for each favorited fixed action in
    /// `category`, so `DrawFavoriteMenuItem` below can send it exactly like any saved quick command - these
    /// are never written back to `quick.*` lists, just built fresh each frame for display.
    private static IEnumerable<QuickCommand> FavoritedFixedActionsFor(string category, HashSet<string> favoriteIds) =>
        FixedActions.Where(a => a.Category == category && favoriteIds.Contains(a.Id))
            .Select(a => new QuickCommand { Label = a.Label, Command = a.Command });

    private static void DrawFavoriteMenuItem(Plugin plugin, QuickCommand cmd, bool canSend)
    {
        var composed = plugin.ChatComposer.Compose(cmd.Command);
        var fits = CommandSelector.Fits(composed);
        using (ImRaii.Disabled(!canSend || !fits))
        {
            if (ImGui.MenuItem(cmd.Label))
                plugin.ChatSender.Send(composed);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(!fits ? "Command is too long for a safe chat payload." : canSend ? composed : "No /tell target yet - pairing hasn't captured your Sub's name.");
    }

    /// Teleport can't join `FixedActions` above - it has no static `Command` text, resolved live via
    /// `TeleportSendAction` instead. The popup closes on click (design.md), so a resolution failure is
    /// reported via a transient notification (matching `CatalogSyncRelayService`'s existing pattern) rather
    /// than an inline message.
    private static void DrawTeleportMenuItem(Plugin plugin, bool canSend)
    {
        using (ImRaii.Disabled(!canSend))
        {
            if (ImGui.MenuItem("Teleport"))
            {
                var (success, error) = TeleportSendAction.TryResolveAndSend(plugin);
                if (!success)
                    Plugin.NotificationManager.AddNotification(new Notification
                    {
                        Title = "Oathbound",
                        Content = error ?? "Teleport failed.",
                        Type = NotificationType.Warning,
                        InitialDuration = TimeSpan.FromSeconds(5),
                    });
            }
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(canSend ? "Teleport your paired Sub to your current position." : "No /tell target yet - pairing hasn't captured your Sub's name.");
    }
}
