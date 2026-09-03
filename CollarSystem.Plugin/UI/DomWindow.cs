using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Glamourer.Api.Enums;

namespace CollarSystem.Plugin.UI;

/// Owner's command panel: pairing, and one section per control category. Every send action here is
/// purely "ask the Sub's client to do something" - see design.md's Context. Nothing here ever touches
/// game state directly; CommandDispatcher on the Sub's side is the only place IPC calls happen.
public class DomWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    private string ownerName = "Owner";
    private string pairingCodeInput = "";

    private string titleText = "";
    private bool titleIsPrefix;
    private Vector3 titleColor = new(1, 1, 1);

    private int outfitSlot = (int)ApiEquipSlot.Body;
    private string outfitItemId = "0";
    private string outfitKey = "12345";
    private bool outfitLocked = true;

    public DomWindow(Plugin plugin) : base("Collar - Owner###CollarDomWindow")
    {
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(420, 360), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (ImGui.Button("Settings (role, relay URL)"))
            plugin.ToggleSettingsUi();
        ImGui.Separator();

        DrawPairingSection();

        if (!plugin.Configuration.Pairing.IsPaired)
        {
            ImGui.TextDisabled("Pair with a Sub to send commands.");
            return;
        }

        ImGui.Spacing();
        DrawTitleSection();
        ImGui.Spacing();
        DrawOutfitSection();
        ImGui.Spacing();
        DrawGestureSection();
        ImGui.Spacing();
        DrawFollowSection();
    }

    private void DrawPairingSection()
    {
        using var _ = Dalamud.Interface.Utility.Raii.ImRaii.Child("pairing", new Vector2(0, 90), true);
        ImGui.TextUnformatted("Pairing");
        ImGui.Separator();

        if (plugin.Configuration.Pairing.IsPaired)
        {
            ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), $"Paired with {plugin.Configuration.Pairing.PeerName}");
            if (ImGui.Button("Unpair"))
                plugin.PanicHandler.Panic(); // full local reset is also the correct behaviour for a deliberate unpair
            return;
        }

        ImGui.InputText("Your name", ref ownerName, 64);
        ImGui.InputText("Sub's pairing code", ref pairingCodeInput, 32);
        if (ImGui.Button("Request pairing") && pairingCodeInput.Length > 0)
        {
            var code = pairingCodeInput;
            var name = ownerName;
            Plugin.FireAndForget(plugin.PairingCommand.RequestPairingAsync(code, name));
        }
    }

    private void DrawTitleSection()
    {
        if (!ImGui.CollapsingHeader("Title"))
            return;

        ImGui.InputText("Title text", ref titleText, 64);
        ImGui.Checkbox("Prefix (not suffix)", ref titleIsPrefix);
        ImGui.ColorEdit3("Color", ref titleColor);

        if (ImGui.Button("Set title"))
            Plugin.FireAndForget(plugin.TitleCommand.SendSetAsync(titleText, titleIsPrefix, titleColor, null));
        ImGui.SameLine();
        if (ImGui.Button("Clear title"))
            Plugin.FireAndForget(plugin.TitleCommand.SendClearAsync());
    }

    private void DrawOutfitSection()
    {
        if (!ImGui.CollapsingHeader("Outfit"))
            return;

        ImGui.Combo("Slot", ref outfitSlot, EquipSlotNames, EquipSlotNames.Length);
        ImGui.InputText("Item ID", ref outfitItemId, 20);
        ImGui.InputText("Lock key", ref outfitKey, 20);
        ImGui.Checkbox("Lock", ref outfitLocked);

        if (ImGui.Button("Send outfit") && ulong.TryParse(outfitItemId, out var itemId) && uint.TryParse(outfitKey, out var key))
        {
            Plugin.FireAndForget(plugin.OutfitCommand.SendSetItemAsync(EquipSlotValues[outfitSlot], itemId, [0, 0], key, outfitLocked));
        }

        ImGui.SameLine();
        if (ImGui.Button("Unlock") && uint.TryParse(outfitKey, out var unlockKey))
            Plugin.FireAndForget(plugin.OutfitCommand.SendUnlockAsync(unlockKey));
    }

    private void DrawGestureSection()
    {
        if (!ImGui.CollapsingHeader("Gesture"))
            return;

        var catalog = plugin.Configuration.GestureMapping.CachedPeerCatalog;
        if (catalog.Count == 0)
        {
            ImGui.TextDisabled("No gesture catalog received yet - ask the Sub to rescan and share.");
            return;
        }

        foreach (var entry in catalog)
        {
            if (entry.EmoteNames.Count == 0)
            {
                ImGui.TextDisabled($"{entry.ModName} (unresolved)");
                continue;
            }

            foreach (var emote in entry.EmoteNames)
            {
                ImGui.PushID($"{entry.ModDirectory}:{emote}");
                if (ImGui.Button($"Prompt \"{emote}\" ({entry.ModName})"))
                    Plugin.FireAndForget(plugin.GestureCommand.SendPromptAsync(entry.ModDirectory, entry.ModName, emote));
                ImGui.PopID();
            }
        }
    }

    private void DrawFollowSection()
    {
        if (!ImGui.CollapsingHeader("Follow / Leash"))
            return;

        ImGui.TextWrapped("Requires the Sub to have separately opted into the movement-lock permission.");
        if (ImGui.Button("Engage leash"))
            Plugin.FireAndForget(plugin.FollowCommand.SendEngageAsync());
        ImGui.SameLine();
        if (ImGui.Button("Release leash"))
            Plugin.FireAndForget(plugin.FollowCommand.SendReleaseAsync());
    }

    private static readonly string[] EquipSlotNames =
    [
        "MainHand", "OffHand", "Head", "Body", "Hands", "Legs", "Feet", "Ears", "Neck", "Wrists", "RFinger", "LFinger",
    ];

    private static readonly ApiEquipSlot[] EquipSlotValues =
    [
        ApiEquipSlot.MainHand, ApiEquipSlot.OffHand, ApiEquipSlot.Head, ApiEquipSlot.Body, ApiEquipSlot.Hands,
        ApiEquipSlot.Legs, ApiEquipSlot.Feet, ApiEquipSlot.Ears, ApiEquipSlot.Neck, ApiEquipSlot.Wrists,
        ApiEquipSlot.RFinger, ApiEquipSlot.LFinger,
    ];
}
