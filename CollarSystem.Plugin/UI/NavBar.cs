using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace CollarSystem.Plugin.UI;

/// The persistent icon row used to switch between modules (Home/Wardrobe/Gesture/Follow/...) - the
/// "scalable" navigation surface: a new module is one more entry in the caller's array, not a wider tab
/// bar or another accordion buried in the window body.
public static class NavBar
{
    public static string? Draw(string activeId, params (string Id, FontAwesomeIcon Icon, string Tooltip)[] items)
    {
        string? clicked = null;

        // 32px buttons + the card's own top/bottom padding need more than 44px - too tight caused a
        // few-pixel vertical overflow and a sliver of scrollbar inside the nav bar itself. noScroll is
        // the hard guarantee; the taller size just avoids clipping the buttons.
        using var card = Card.Begin("navBar", new Vector2(0, 56), noScroll: true);
        for (var i = 0; i < items.Length; i++)
        {
            var (id, icon, tooltip) = items[i];
            var active = id == activeId;

            using (ImRaii.PushColor(ImGuiCol.Button, active ? Theme.Accent : Theme.TileBg))
            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, active ? Theme.AccentHover : Theme.TileBgHover))
            using (ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, Theme.TileRounding))
            {
                if (IconGlyph.Button(icon, new Vector2(32, 32)))
                    clicked = id;
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(tooltip);

            if (i < items.Length - 1)
                ImGui.SameLine();
        }

        return clicked;
    }
}
