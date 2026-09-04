using System;
using System.Linq;
using System.Numerics;
using Oathbound.Plugin.Config;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace Oathbound.Plugin.UI;

/// Dedicated PoseKit-style browser used by the alias form. Keeping this hierarchy in its own roomy
/// window lets mod/group/option names remain readable instead of compressing them into a combo.
public sealed class AnimationPickerWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string search = "";
    private Action<GestureCatalogEntry>? onSelected;

    public AnimationPickerWindow(Plugin plugin) : base("Add animation###CollarAnimationPicker")
    {
        this.plugin = plugin;
        Flags = ImGuiWindowFlags.NoCollapse;
        Size = new Vector2(720, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(520, 420), MaximumSize = new Vector2(1100, 1000) };
    }

    public void Open(Action<GestureCatalogEntry> selected)
    {
        onSelected = selected;
        IsOpen = true;
    }

    public void Dispose() { }

    public override void Draw()
    {
        IconGlyph.Text(FontAwesomeIcon.TheaterMasks, "Animation Library");
        ImGui.SameLine();
        IconGlyph.WrappedDisabled("Choose the exact named option and trigger for this alias.");
        ImGui.Separator();

        const float buttonWidth = 82;
        ImGui.SetNextItemWidth(Math.Max(180, ImGui.GetContentRegionAvail().X - buttonWidth - ImGui.GetStyle().ItemSpacing.X));
        ImGui.InputTextWithHint("##animationPickerSearch", "Search mod, group, animation, command, or pose...", ref search, 128);
        ImGui.SameLine();
        if (ImGui.Button("Rescan", new Vector2(buttonWidth, 0))) plugin.GestureCommand.Rescan();

        var all = plugin.Configuration.GestureMapping.LocalCatalog.Values.ToList();
        if (all.Count == 0)
        {
            IconGlyph.WrappedColored(Theme.Warning, "No animations scanned. Select Penumbra mods in Settings, rescan, then return here.");
            return;
        }

        var filter = search.Trim();
        var visible = all.Where(e => filter.Length == 0
            || e.ModName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || e.GroupName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || e.AnimationName.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || (e.Trigger?.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

        IconGlyph.WrappedDisabled($"{visible.Count} shown / {all.Count} discovered");
        ImGui.Separator();
        using var child = ImRaii.Child("animationPickerResults", Vector2.Zero, false);
        if (visible.Count == 0) { IconGlyph.WrappedDisabled("No animations match this search."); return; }

        foreach (var mod in visible.GroupBy(e => new { e.ModDirectory, e.ModName }).OrderBy(g => g.Key.ModName, StringComparer.OrdinalIgnoreCase))
        {
            var first = mod.First();
            var flags = filter.Length > 0 ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
            if (!ImGui.CollapsingHeader($"{mod.Key.ModName}##pickerMod_{mod.Key.ModDirectory}", flags)) continue;
            ImGui.Indent();
            IconGlyph.WrappedDisabled(first.ModEnabled ? "Enabled in Penumbra" : "Disabled in Penumbra — enabled temporarily when used");

            foreach (var group in mod.GroupBy(e => new { e.GroupName, e.GroupOrder }).OrderBy(g => g.Key.GroupOrder))
            {
                var groupOpen = group.Count() <= 4 || filter.Length > 0
                    ? ImGuiTreeNodeFlags.DefaultOpen
                    : ImGuiTreeNodeFlags.None;
                if (!ImGui.TreeNodeEx($"{group.Key.GroupName}##pickerGroup_{mod.Key.ModDirectory}_{group.Key.GroupOrder}", groupOpen)) continue;

                foreach (var option in group.GroupBy(e => new { e.AnimationName, e.OptionOrder }).OrderBy(g => g.Key.OptionOrder))
                {
                    ImGui.TextUnformatted(option.Key.AnimationName);
                    foreach (var entry in option.OrderBy(e => e.TriggerOrder))
                    {
                        ImGui.Indent();
                        if (entry.Trigger is null)
                        {
                            IconGlyph.WrappedDisabled("No playable gesture detected");
                        }
                        else
                        {
                            IconGlyph.WrappedColored(Theme.Accent, entry.Trigger.DisplayName);
                            ImGui.SameLine();
                            if (ImGui.SmallButton($"Choose##pickerChoose_{entry.Id}"))
                            {
                                onSelected?.Invoke(entry);
                                IsOpen = false;
                            }
                        }
                        ImGui.Unindent();
                    }
                }
                ImGui.TreePop();
            }
            ImGui.Unindent();
        }
    }
}
