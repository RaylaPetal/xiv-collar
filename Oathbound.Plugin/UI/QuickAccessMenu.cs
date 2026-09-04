using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Oathbound.Plugin.Config;
using Dalamud.Bindings.ImGui;
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

            if (favoritesByCategory.Count == 0)
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
            }

            ImGui.Separator();
            if (ImGui.MenuItem("Open Owner commands"))
                plugin.OpenOwnerCommands();
        }

        if (ImGui.MenuItem("Open main window"))
            plugin.OpenMainWindow();
        if (ImGui.MenuItem("Open settings"))
            plugin.ToggleSettingsUi();

        ImGui.EndPopup();
    }

    private static List<(string Label, List<QuickCommand> Favorites)> CategorizedFavorites(OwnerQuickCommands quick)
    {
        (string Label, List<QuickCommand> List)[] categories =
        [
            ("Title", quick.Titles),
            ("Outfit", quick.Outfits),
            ("Gesture", quick.Gestures),
            ("Follow", quick.Follow),
            ("Moodles", quick.Moodles),
            ("Restraints", quick.Restraints),
            ("Custom Trigger Bundles", quick.Aliases),
        ];

        return categories
            .Select(c => (c.Label, Favorites: c.List.Where(cmd => cmd.IsFavorite).OrderBy(cmd => cmd.Label, StringComparer.OrdinalIgnoreCase).ToList()))
            .Where(c => c.Favorites.Count > 0)
            .ToList();
    }

    private static void DrawFavoriteMenuItem(Plugin plugin, QuickCommand cmd, bool canSend)
    {
        var composed = plugin.ChatComposer.Compose(cmd.Command);
        using (ImRaii.Disabled(!canSend))
        {
            if (ImGui.MenuItem(cmd.Label))
                plugin.ChatSender.Send(composed);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(canSend ? composed : "No /tell target yet - pairing hasn't captured your Sub's name.");
    }
}
