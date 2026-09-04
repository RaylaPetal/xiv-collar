using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Oathbound.Plugin.UI;

/// This build's default UI font does not have FontAwesome codepoints merged in - concatenating an icon
/// glyph directly into a widget's label string renders garbage (confirmed live: an exclamation-triangle
/// icon showed as a bare "="). Every icon+text combination in this plugin goes through here instead, which
/// explicitly pushes `UiBuilder.FontIcon` for the glyph only.
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

    /// A "(?)" marker placed right after a label/control, showing `tooltip` on hover. The plugin's one
    /// consistent way to explain a control in place, since most windows are too tight on vertical space
    /// for a permanent sentence next to every field.
    public static void HelpMarker(string tooltip)
    {
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        if (!ImGui.IsItemHovered())
            return;

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 24f);
        ImGui.TextUnformatted(tooltip);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    /// `TextColored` doesn't wrap on its own (no wrap position is set by default) - long status lines were
    /// getting clipped by their card's fixed height instead of flowing to a second line. This wraps at the
    /// current window's content width, same as `TextWrapped` does for uncolored text.
    public static void WrappedColored(Vector4 color, string text)
    {
        ImGui.PushTextWrapPos(0f);
        ImGui.TextColored(color, text);
        ImGui.PopTextWrapPos();
    }

    /// Same problem as WrappedColored, for `TextDisabled` - it doesn't wrap on its own either, and several
    /// of the plugin's longer hint/description lines use it.
    public static void WrappedDisabled(string text)
    {
        ImGui.PushTextWrapPos(0f);
        ImGui.TextDisabled(text);
        ImGui.PopTextWrapPos();
    }
}
