using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CollarSystem.Plugin.Config;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace CollarSystem.Plugin.UI;

/// collar/ui-organization "Compact favorites window lists only favorited commands": opened by the DTR bar
/// entry (see Plugin.cs), lists every `QuickCommand` across all seven Owner categories with `IsFavorite`
/// set, flat rather than grouped by category. Deliberately a small toggleable Window like ItemPickerWindow/
/// AnimationPickerWindow, not a true auto-dismissing native dropdown - see design.md's recorded scope
/// decision. Self-contained (its own minimal Send/Copy/Un-favorite row) rather than reaching into
/// CollarWindow's private row-drawing helpers.
public sealed class FavoritesWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public FavoritesWindow(Plugin plugin) : base("Collar Favorites###CollarFavoritesWindow")
    {
        this.plugin = plugin;
        Flags = ImGuiWindowFlags.NoCollapse;
        Size = new Vector2(360, 320);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(280, 200), MaximumSize = new Vector2(600, 800) };
    }

    public void Dispose() { }

    private IEnumerable<QuickCommand> AllFavorites()
    {
        var quick = plugin.Configuration.QuickCommands;
        return quick.Titles.Concat(quick.Outfits).Concat(quick.Gestures).Concat(quick.Follow)
            .Concat(quick.Moodles).Concat(quick.Restraints).Concat(quick.Aliases)
            .Where(c => c.IsFavorite);
    }

    public override void Draw()
    {
        var canSend = plugin.Configuration.Pairing.IsPaired;
        IconGlyph.Text(FontAwesomeIcon.Heading, "Favorites");
        ImGui.Separator();

        var favorites = AllFavorites().OrderBy(c => c.Label, System.StringComparer.OrdinalIgnoreCase).ToList();
        if (favorites.Count == 0)
        {
            ImGui.TextWrapped("Nothing favorited yet - open the Owner tab, and click \"Favorite\" next to any saved quick command to have it show up here.");
        }
        else
        {
            foreach (var cmd in favorites)
                DrawFavoriteRow(cmd, canSend);
        }

        ImGui.Spacing();
        ImGui.Separator();
        if (ImGui.Button("Open Owner commands"))
            plugin.OpenOwnerCommands();
    }

    private void DrawFavoriteRow(QuickCommand cmd, bool canSend)
    {
        ImGui.PushID(cmd.Label + cmd.Command);
        ImGui.TextUnformatted(cmd.Label);

        var composed = plugin.ChatComposer.Compose(cmd.Command);
        using (Dalamud.Interface.Utility.Raii.ImRaii.Disabled(!canSend))
        {
            if (ImGui.SmallButton("Send"))
                plugin.ChatSender.Send(composed);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(canSend ? composed : "No /tell target yet - pairing hasn't captured your Sub's name.");
        ImGui.SameLine();
        if (ImGui.SmallButton("Copy"))
            ImGui.SetClipboardText(composed);
        ImGui.SameLine();
        if (ImGui.SmallButton("Unfavorite"))
        {
            cmd.IsFavorite = false;
            plugin.Configuration.Save();
        }
        ImGui.PopID();
    }
}
