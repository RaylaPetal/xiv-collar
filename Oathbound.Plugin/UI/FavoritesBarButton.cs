using System;
using System.Numerics;
using Oathbound.Plugin.Config;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace Oathbound.Plugin.UI;

/// collar/ui-organization "A movable on-screen button opens the quick-access favorites menu": a small,
/// always-visible overlay button - styled to sit unobtrusively over the game UI rather than as a native
/// ImGui window titlebar/frame (no titlebar/resize/scrollbar/background chrome), pinned every frame to a
/// corner + margin from Settings (design.md's "Alternative considered": a Settings-driven position is
/// simpler to persist/validate than free drag state). Always `IsOpen` - unlike every other window in this
/// plugin, there's no toggle for the button itself, only for the popup menu it opens (QuickAccessMenu).
public sealed class FavoritesBarButton : Window, IDisposable
{
    public const float ButtonSize = 32f;

    private readonly Plugin plugin;

    public FavoritesBarButton(Plugin plugin) : base("###CollarFavoritesBarButton")
    {
        this.plugin = plugin;
        IsOpen = true;
        ShowCloseButton = false;
        RespectCloseHotkey = false;
        Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings |
                ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.AlwaysAutoResize;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        ImGui.SetNextWindowPos(ComputePosition(plugin.Configuration.FavoritesButton), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.75f);
    }

    public override void Draw()
    {
        if (IconGlyph.Button(FontAwesomeIcon.Star, new Vector2(ButtonSize, ButtonSize)))
            QuickAccessMenu.Toggle();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Favorited Collar commands");

        QuickAccessMenu.Draw(plugin);
    }

    /// Shared with QuickAccessMenu, so the popup it opens can anchor itself to the button's actual
    /// on-screen position instead of relying on Dear ImGui's own mouse-position popup heuristics.
    public static Vector2 ComputePosition(FavoritesButtonSettings settings)
    {
        var viewport = ImGui.GetMainViewport();
        var min = viewport.Pos;
        var size = viewport.Size;
        var margin = settings.Margin;

        return settings.Corner switch
        {
            ScreenCorner.TopLeft => min + margin,
            ScreenCorner.TopRight => new Vector2(min.X + size.X - ButtonSize - margin.X, min.Y + margin.Y),
            ScreenCorner.BottomLeft => new Vector2(min.X + margin.X, min.Y + size.Y - ButtonSize - margin.Y),
            ScreenCorner.BottomRight => new Vector2(min.X + size.X - ButtonSize - margin.X, min.Y + size.Y - ButtonSize - margin.Y),
            _ => min + margin,
        };
    }
}
