using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace CollarSystem.Plugin.UI;

/// This build's default UI font does not have FontAwesome codepoints merged in - concatenating an icon
/// glyph directly into a widget's label string renders garbage (confirmed live: the panic button's
/// intended exclamation-triangle showed as a bare "="). Every icon+text combination in this plugin goes
/// through here instead, which explicitly pushes `UiBuilder.FontIcon` for the glyph only.
public static class IconGlyph
{
    /// Icon-only button (nav bar) - the whole label is the glyph, so one font push covers it.
    public static bool Button(FontAwesomeIcon icon, Vector2 size)
    {
        using var font = ImRaii.PushFont(Plugin.PluginInterface.UiBuilder.FontIcon);
        return ImGui.Button(icon.ToIconString(), size);
    }

    /// Icon + label as plain (non-clickable) text - two draw calls, SameLine'd, each in its own font.
    public static void Text(FontAwesomeIcon icon, string label)
    {
        using (ImRaii.PushFont(Plugin.PluginInterface.UiBuilder.FontIcon))
            ImGui.TextUnformatted(icon.ToIconString());
        ImGui.SameLine();
        ImGui.TextUnformatted(label);
    }

    /// Icon above label, both centered inside a clickable tile. ImGui can't mix fonts within one
    /// widget's text, so this draws an invisible hit-region button and overlays icon+text manually.
    public static bool TileButton(FontAwesomeIcon icon, string label, Vector2 size)
    {
        var clicked = ImGui.Button($"##tile_{label}", size);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var center = (min + max) / 2;

        string iconStr;
        Vector2 iconSize;
        using (ImRaii.PushFont(Plugin.PluginInterface.UiBuilder.FontIcon))
        {
            iconStr = icon.ToIconString();
            iconSize = ImGui.CalcTextSize(iconStr);
        }

        var labelSize = ImGui.CalcTextSize(label);
        var totalHeight = iconSize.Y + 4 + labelSize.Y;
        var iconPos = new Vector2(center.X - iconSize.X / 2, center.Y - totalHeight / 2);
        var labelPos = new Vector2(center.X - labelSize.X / 2, iconPos.Y + iconSize.Y + 4);

        var drawList = ImGui.GetWindowDrawList();
        var textColor = ImGui.GetColorU32(ImGuiCol.Text);
        using (ImRaii.PushFont(Plugin.PluginInterface.UiBuilder.FontIcon))
            drawList.AddText(iconPos, textColor, iconStr);
        drawList.AddText(labelPos, textColor, label);

        return clicked;
    }

    /// Icon to the left of the label, both centered as one unit inside a wide button - same overlay
    /// technique as TileButton, laid out horizontally instead of stacked. Used for the panic button.
    public static bool SideIconButton(FontAwesomeIcon icon, string label, Vector2 size)
    {
        var clicked = ImGui.Button($"##side_{label}", size);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var center = (min + max) / 2;

        string iconStr;
        Vector2 iconSize;
        using (ImRaii.PushFont(Plugin.PluginInterface.UiBuilder.FontIcon))
        {
            iconStr = icon.ToIconString();
            iconSize = ImGui.CalcTextSize(iconStr);
        }

        var labelSize = ImGui.CalcTextSize(label);
        const float spacing = 10f;
        var totalWidth = iconSize.X + spacing + labelSize.X;
        var startX = center.X - totalWidth / 2;

        var iconPos = new Vector2(startX, center.Y - iconSize.Y / 2);
        var labelPos = new Vector2(startX + iconSize.X + spacing, center.Y - labelSize.Y / 2);

        var drawList = ImGui.GetWindowDrawList();
        var textColor = ImGui.GetColorU32(ImGuiCol.Text);
        using (ImRaii.PushFont(Plugin.PluginInterface.UiBuilder.FontIcon))
            drawList.AddText(iconPos, textColor, iconStr);
        drawList.AddText(labelPos, textColor, label);

        return clicked;
    }
}
