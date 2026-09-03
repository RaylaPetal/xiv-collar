using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ImGuiCol = Dalamud.Bindings.ImGui.ImGuiCol;
using ImGuiStyleVar = Dalamud.Bindings.ImGui.ImGuiStyleVar;

namespace CollarSystem.Plugin.UI;

/// A clickable icon+label tile for the module grid (DomWindow's home screen) - the "scalable" navigation
/// unit: a future command category is a new tile call, not a wider tab bar or another accordion.
public static class ModuleTile
{
    public static bool Draw(string label, FontAwesomeIcon icon, Vector2 size)
    {
        using var rounding = ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, Theme.TileRounding);
        using var bg = ImRaii.PushColor(ImGuiCol.Button, Theme.TileBg);
        using var bgHover = ImRaii.PushColor(ImGuiCol.ButtonHovered, Theme.TileBgHover);
        using var bgActive = ImRaii.PushColor(ImGuiCol.ButtonActive, Theme.Accent);

        return IconGlyph.TileButton(icon, label, size);
    }
}
