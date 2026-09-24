using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Oathbound.Plugin.UI;

/// A labeled group inside a tab - the plugin's one "these controls belong together" primitive, used so every
/// module and settings page splits into the same kind of bordered, tinted blocks instead of one long run of
/// controls. `using (Section.Begin("id", "Heading")) { ... }` - early returns inside the `using` still close it.
///
/// A real child window (true padding on every side, its own draw list - safe for the rule editor's legacy
/// Columns - and the purple border Theme.PushWindowStyle sets). These ImGui bindings predate
/// ImGuiChildFlags.AutoResizeY, so the child is sized to the content height it measured the previous frame -
/// the same "measure at the end of Draw, apply next frame" approach CollarWindow uses for its own height. A
/// section's very first frame uses a small default, so it can look clipped for that one frame only.
public sealed class SectionScope : IDisposable
{
    private const float FirstFrameHeight = 40f;
    private static readonly Dictionary<uint, float> MeasuredHeights = new();

    private readonly uint key;
    private readonly bool contentsDrawn;
    private bool disposed;

    internal SectionScope(string id, string? heading)
    {
        var childId = $"##section_{id}";
        key = ImGui.GetID(childId);
        var height = MeasuredHeights.TryGetValue(key, out var measured) ? measured : FirstFrameHeight;

        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, Theme.TileRounding);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Theme.SectionBg);
        contentsDrawn = ImGui.BeginChild(childId, new Vector2(0, height), true,
            ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        if (heading is not null)
            Section.Heading(heading);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;

        // Only measure a frame whose contents were actually laid out. A section scrolled fully out of view
        // gets its items skipped by ImGui (BeginChild returns false), so the cursor never advances - measuring
        // then would record an empty box, shrink it next frame, shorten the whole tab, make ImGui clamp the
        // parent's scroll position, bring the section back into view, re-measure it at full size... which is
        // exactly the scroll-jumping/jitter this guard prevents. An off-screen section keeps its last height.
        if (contentsDrawn)
        {
            // Cursor Y after the last item already includes that item's trailing ItemSpacing - swap it for the
            // bottom WindowPadding so the space below the last control matches the space above the first.
            var style = ImGui.GetStyle();
            MeasuredHeights[key] = ImGui.GetCursorPosY() - style.ItemSpacing.Y + style.WindowPadding.Y;
        }

        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
        ImGui.Spacing();
    }
}

public static class Section
{
    public static SectionScope Begin(string id, string? heading = null) => new(id, heading);

    /// An accent-colored label with a rule under it - a section's title.
    public static void Heading(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.AccentHover);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
        ImGui.Separator();
    }

    /// A sub-part heading inside a section - same look as Heading, with a little space above it.
    public static void SubHeading(string text)
    {
        ImGui.Spacing();
        Heading(text);
    }
}
