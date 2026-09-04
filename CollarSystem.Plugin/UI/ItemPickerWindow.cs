using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Glamourer.Api.Enums;

namespace CollarSystem.Plugin.UI;

/// collar/restraints: lets the Sub or Owner pick any equippable game item for a chosen slot, without it
/// needing to be currently equipped or owned - GlamourerIpc.SetItemOnce applies any valid item id
/// unconditionally, which is what makes this picker-first flow possible. Modeled on AnimationPickerWindow's
/// "search a large data set in a roomy dedicated window" shape, backed by Lumina's own Item sheet instead
/// of the mod/animation catalog.
public sealed class ItemPickerWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string search = "";
    private Action<uint, string>? onChosen;
    private ApiEquipSlot slot;
    private List<(uint ItemId, string Name)> slotItems = [];

    public ItemPickerWindow(Plugin plugin) : base("Choose item###CollarItemPicker")
    {
        this.plugin = plugin;
        Flags = ImGuiWindowFlags.NoCollapse;
        Size = new Vector2(560, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(420, 360), MaximumSize = new Vector2(900, 1000) };
    }

    /// Recomputes the slot-filtered item list once per Open rather than every frame - the full Item sheet
    /// has tens of thousands of rows, but only a few hundred are valid for any one slot, so caching this
    /// list keeps the per-frame search-filter cheap.
    public void Open(ApiEquipSlot slot, Action<uint, string> chosen)
    {
        this.slot = slot;
        onChosen = chosen;
        search = "";
        slotItems = EnumerateSlotItems(slot);
        IsOpen = true;
    }

    public void Dispose() { }

    private static List<(uint ItemId, string Name)> EnumerateSlotItems(ApiEquipSlot slot)
    {
        var sheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
        var results = new List<(uint, string)>();
        foreach (var item in sheet)
        {
            if (!MatchesSlot(item, slot))
                continue;
            var name = item.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name))
                continue;
            results.Add((item.RowId, name));
        }
        return results.OrderBy(i => i.Item2, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// The 10 lockable slots each map to one non-zero `EquipSlotCategory` field - confirmed field names via
    /// other installed Dalamud plugins' compiled Lumina structs (design.md's "Item enumeration source").
    private static bool MatchesSlot(Lumina.Excel.Sheets.Item item, ApiEquipSlot slot)
    {
        var category = item.EquipSlotCategory.ValueNullable;
        if (category is null)
            return false;

        return slot switch
        {
            ApiEquipSlot.Head => category.Value.Head != 0,
            ApiEquipSlot.Body => category.Value.Body != 0,
            ApiEquipSlot.Hands => category.Value.Gloves != 0,
            ApiEquipSlot.Legs => category.Value.Legs != 0,
            ApiEquipSlot.Feet => category.Value.Feet != 0,
            ApiEquipSlot.Ears => category.Value.Ears != 0,
            ApiEquipSlot.Neck => category.Value.Neck != 0,
            ApiEquipSlot.Wrists => category.Value.Wrists != 0,
            ApiEquipSlot.RFinger => category.Value.FingerR != 0,
            ApiEquipSlot.LFinger => category.Value.FingerL != 0,
            _ => false,
        };
    }

    public override void Draw()
    {
        IconGlyph.Text(FontAwesomeIcon.Tshirt, $"Item Library - {slot}");
        ImGui.SameLine();
        IconGlyph.WrappedDisabled("Choose any item valid for this slot - it does not need to be equipped or owned.");
        ImGui.Separator();

        ImGui.SetNextItemWidth(Math.Max(180, ImGui.GetContentRegionAvail().X));
        ImGui.InputTextWithHint("##itemPickerSearch", "Search item name...", ref search, 128);

        var filter = search.Trim();
        var visible = filter.Length == 0
            ? slotItems
            : slotItems.Where(i => i.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        IconGlyph.WrappedDisabled($"{visible.Count} shown / {slotItems.Count} valid for {slot}");
        ImGui.Separator();
        using var child = ImRaii.Child("itemPickerResults", Vector2.Zero, false);
        if (visible.Count == 0) { IconGlyph.WrappedDisabled("No items match this search."); return; }

        foreach (var (itemId, name) in visible)
        {
            ImGui.TextUnformatted(name);
            ImGui.SameLine();
            if (ImGui.SmallButton($"Choose##itemPicker_{itemId}"))
            {
                onChosen?.Invoke(itemId, name);
                IsOpen = false;
            }
        }
    }
}
