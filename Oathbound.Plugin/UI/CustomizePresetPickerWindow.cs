using System;
using System.Linq;
using System.Numerics;
using Oathbound.Plugin.Ipc;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;

namespace Oathbound.Plugin.UI;

/// collar/restraints "Optional Customize+ preset on Gagged": a minimal sibling to AnimationPickerWindow -
/// a flat list of the Sub's own Customize+ profiles rather than a mod/group/option/trigger tree, since
/// Customize+ profiles have no such hierarchy and (per design.md's Non-Goals) are never shared via catalog
/// sync, so there is no Owner/imported-mode branch here at all.
public sealed class CustomizePresetPickerWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string search = "";
    private Action<CustomizePlusProfile>? onSelected;

    public CustomizePresetPickerWindow(Plugin plugin) : base("Choose Customize+ preset###CollarCustomizePresetPicker")
    {
        this.plugin = plugin;
        Flags = ImGuiWindowFlags.NoCollapse;
        Size = new Vector2(420, 420);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(320, 240), MaximumSize = new Vector2(700, 800) };
    }

    public void Open(Action<CustomizePlusProfile> selected)
    {
        onSelected = selected;
        IsOpen = true;
    }

    public void Dispose() { }

    public override void Draw()
    {
        IconGlyph.Text(FontAwesomeIcon.UserEdit, "Customize+ Profiles");
        ImGui.SameLine();
        IconGlyph.WrappedDisabled("Applied to your own character while this Gagged rule stays active.");
        ImGui.Separator();

        var result = plugin.CustomizePlusIpc.GetOwnProfiles();
        if (result.Status == CustomizePlusScanStatus.Unavailable)
        {
            IconGlyph.WrappedColored(Theme.Warning, "Customize+ is not installed or not ready.");
            return;
        }
        if (result.Status == CustomizePlusScanStatus.Failed)
        {
            IconGlyph.WrappedColored(Theme.Warning, $"Could not read Customize+ profiles: {result.Error}");
            return;
        }
        if (result.Profiles.Count == 0)
        {
            IconGlyph.WrappedDisabled("No Customize+ profiles found. Create one in Customize+ first.");
            return;
        }

        ImGui.SetNextItemWidth(Math.Max(180, ImGui.GetContentRegionAvail().X));
        ImGui.InputTextWithHint("##customizePresetPickerSearch", "Search profiles...", ref search, 128);

        var filter = search.Trim();
        var visible = result.Profiles
            .Where(p => filter.Length == 0 || p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        IconGlyph.WrappedDisabled($"{visible.Count} shown / {result.Profiles.Count} profiles");
        ImGui.Separator();
        if (visible.Count == 0) { IconGlyph.WrappedDisabled("No profiles match this search."); return; }

        foreach (var profile in visible)
        {
            if (ImGui.SmallButton($"Choose##customizePresetChoose_{profile.UniqueId}"))
            {
                onSelected?.Invoke(profile);
                IsOpen = false;
            }
            ImGui.SameLine();
            ImGui.TextUnformatted(profile.Name);
        }
    }
}
