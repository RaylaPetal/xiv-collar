using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Oathbound.Plugin.UI;

/// The persistent icon row used to switch between modules (Home/Wardrobe/Gesture/Follow/...) - the
/// "scalable" navigation surface: a new module is one more entry in the caller's array, not a wider tab
/// bar or another accordion buried in the window body.
public static class NavBar
{
    public static string? Draw(string activeId, string trailingId, params (string Id, FontAwesomeIcon Icon, string Tooltip)[] items)
    {
        string? clicked = null;

        // 32px buttons + the card's own top/bottom padding need more than 44px - too tight caused a
        // few-pixel vertical overflow and a sliver of scrollbar inside the nav bar itself. noScroll is
        // the hard guarantee; the taller size just avoids clipping the buttons.
        using var card = Card.Begin("navBar", new Vector2(0, 56), noScroll: true);
        var leading = items.Where(item => item.Id != trailingId).ToArray();
        for (var i = 0; i < leading.Length; i++)
        {
            var itemClicked = DrawItem(activeId, leading[i]);
            if (clicked is null && itemClicked is not null)
                clicked = itemClicked;
            if (i < leading.Length - 1)
                ImGui.SameLine();
        }

        var trailing = items.FirstOrDefault(item => item.Id == trailingId);
        if (!string.IsNullOrEmpty(trailing.Id))
        {
            ImGui.SameLine();
            var rightX = ImGui.GetWindowContentRegionMax().X - 32f;
            if (rightX > ImGui.GetCursorPosX())
                ImGui.SetCursorPosX(rightX);
            var itemClicked = DrawItem(activeId, trailing);
            if (clicked is null && itemClicked is not null)
                clicked = itemClicked;
        }

        return clicked;
    }

    private static string? DrawItem(string activeId, (string Id, FontAwesomeIcon Icon, string Tooltip) item)
    {
        var active = item.Id == activeId;
        using (ImRaii.PushColor(ImGuiCol.Button, active ? Theme.Accent : Theme.TileBg))
        using (ImRaii.PushColor(ImGuiCol.ButtonHovered, active ? Theme.AccentHover : Theme.TileBgHover))
        using (ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, Theme.TileRounding))
        {
            if (IconGlyph.Button(item.Icon, new Vector2(32, 32)))
                return item.Id;
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(item.Tooltip);
        return null;
    }
}
