using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Glamourer.Api.Enums;

namespace CollarSystem.Plugin.UI;

/// Owner's command panel: a status card, a nav bar (Home/Title/Wardrobe/Gesture/Follow), and content per
/// selected module - design.md's tile-grid navigation decision, refined to a persistent icon nav bar so
/// switching modules doesn't cost a "back" click. Every send action here is purely "ask the Sub's client
/// to do something" - see design.md's Context. Nothing here ever touches game state directly;
/// CommandDispatcher on the Sub's side is the only place IPC calls happen.
public class DomWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    private string activeModule = "home";
    private string pairingCodeInput = "";

    private string titleText = "";
    private bool titleIsPrefix;
    private Vector3 titleColor = new(1, 1, 1);

    private int outfitSlot = (int)ApiEquipSlot.Body;
    private string outfitItemId = "0";
    private string outfitKey = "12345";
    private bool outfitLocked = true;

    private static readonly (string Id, FontAwesomeIcon Icon, string Tooltip)[] NavItems =
    [
        ("home", FontAwesomeIcon.Home, "Home"),
        ("title", FontAwesomeIcon.Heading, "Title"),
        ("wardrobe", FontAwesomeIcon.Tshirt, "Wardrobe"),
        ("gesture", FontAwesomeIcon.TheaterMasks, "Gesture"),
        ("follow", FontAwesomeIcon.Link, "Follow / Leash"),
    ];

    public DomWindow(Plugin plugin) : base("Collar - Owner###CollarDomWindow")
    {
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(460, 440), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
        this.plugin = plugin;

        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Cog,
            Click = _ => plugin.ToggleSettingsUi(),
            ShowTooltip = () => ImGui.SetTooltip("Settings"),
        });
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawStatusBar();
        ImGui.Spacing();

        if (!plugin.Configuration.Pairing.IsPaired)
        {
            DrawPairingCard();
            return;
        }

        if (NavBar.Draw(activeModule, NavItems) is { } clicked)
            activeModule = clicked;

        ImGui.Spacing();

        switch (activeModule)
        {
            case "title":
                using (var card = Card.Begin("moduleCard"))
                    DrawTitleModule();
                break;
            case "wardrobe":
                using (var card = Card.Begin("moduleCard"))
                    DrawWardrobeModule();
                break;
            case "gesture":
                using (var card = Card.Begin("moduleCard"))
                    DrawGestureModule();
                break;
            case "follow":
                using (var card = Card.Begin("moduleCard"))
                    DrawFollowModule();
                break;
            default:
                DrawPairingCard();
                break;
        }
    }

    private void DrawStatusBar()
    {
        using var card = Card.Begin("statusBar", new Vector2(0, 36), noScroll: true);
        ConnectionStatusView.Draw(plugin.Relay.ConnectionState);
        ImGui.SameLine();
        ImGui.TextColored(Theme.TextMuted, "|");
        ImGui.SameLine();
        ImGui.TextUnformatted("Role: Owner");

        ImGui.SameLine();
        ImGui.TextColored(Theme.TextMuted, "|");
        ImGui.SameLine();
        var pairing = plugin.Configuration.Pairing;
        if (pairing.IsPaired)
            ImGui.TextColored(Theme.Success, $"Owns {pairing.PeerName}");
        else
            ImGui.TextColored(Theme.TextMuted, "None owned");
    }

    private void DrawPairingCard()
    {
        using var card = Card.Begin("pairingCard", new Vector2(0, 90));
        ImGui.TextUnformatted("Pairing");
        ImGui.Separator();

        if (plugin.Configuration.Pairing.IsPaired)
        {
            if (ImGui.Button("Unpair"))
                plugin.PanicHandler.Panic(); // full local reset is also the correct behaviour for a deliberate unpair
            return;
        }

        ImGui.InputText("Sub's pairing code", ref pairingCodeInput, 32);
        if (ImGui.Button("Request pairing") && pairingCodeInput.Length > 0)
        {
            var code = pairingCodeInput;
            Plugin.FireAndForget(plugin.PairingCommand.RequestPairingAsync(code));
        }
    }

    private void DrawTitleModule()
    {
        IconGlyph.Text(FontAwesomeIcon.Heading, "Title");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.InputText("Title text", ref titleText, 64);
        ImGui.Checkbox("Prefix (not suffix)", ref titleIsPrefix);
        ImGui.ColorEdit3("Color", ref titleColor);

        if (ImGui.Button("Set title"))
            Plugin.FireAndForget(plugin.TitleCommand.SendSetAsync(titleText, titleIsPrefix, titleColor, null));
        ImGui.SameLine();
        if (ImGui.Button("Clear title"))
            Plugin.FireAndForget(plugin.TitleCommand.SendClearAsync());
    }

    private void DrawWardrobeModule()
    {
        IconGlyph.Text(FontAwesomeIcon.Tshirt, "Wardrobe");
        ImGui.Separator();
        ImGui.Spacing();

        var designs = plugin.Configuration.WardrobeMapping.CachedPeerDesigns;
        if (designs.Count == 0)
        {
            ImGui.TextDisabled("No wardrobe shared yet - ask the Sub to add designs to their allowlist in Settings and rescan.");
        }
        else
        {
            ImGui.TextUnformatted("Shared designs");
            using (var _ = ImRaii.Child("designList", new Vector2(0, 120), true))
            {
                foreach (var design in designs)
                {
                    ImGui.PushID(design.DesignId.ToString());
                    ImGui.TextUnformatted(design.Name);
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Apply"))
                    {
                        uint.TryParse(outfitKey, out var key); // key stays 0 (unlocked-default) if parsing fails
                        Plugin.FireAndForget(plugin.OutfitCommand.SendApplyDesignAsync(design.DesignId, key, outfitLocked));
                    }
                    ImGui.PopID();
                }
            }
        }

        ImGui.Spacing();
        ImGui.InputText("Lock key", ref outfitKey, 20);
        ImGui.Checkbox("Lock", ref outfitLocked);
        if (ImGui.Button("Unlock") && uint.TryParse(outfitKey, out var unlockKey))
            Plugin.FireAndForget(plugin.OutfitCommand.SendUnlockAsync(unlockKey));

        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Advanced (manual slot / raw state)"))
            DrawAdvancedOutfitControls();
    }

    private void DrawAdvancedOutfitControls()
    {
        ImGui.Combo("Slot", ref outfitSlot, EquipSlotNames, EquipSlotNames.Length);
        ImGui.InputText("Item ID", ref outfitItemId, 20);

        if (ImGui.Button("Send outfit slot") && ulong.TryParse(outfitItemId, out var itemId) && uint.TryParse(outfitKey, out var key))
            Plugin.FireAndForget(plugin.OutfitCommand.SendSetItemAsync(EquipSlotValues[outfitSlot], itemId, [0, 0], key, outfitLocked));
    }

    private void DrawGestureModule()
    {
        IconGlyph.Text(FontAwesomeIcon.TheaterMasks, "Gesture");
        ImGui.Separator();
        ImGui.Spacing();

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

    private void DrawFollowModule()
    {
        IconGlyph.Text(FontAwesomeIcon.Link, "Follow / Leash");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextWrapped("Requires the Sub to have separately opted into the movement-lock permission.");
        if (ImGui.Button("Engage leash"))
            Plugin.FireAndForget(plugin.FollowCommand.SendEngageAsync());
        ImGui.SameLine();
        if (ImGui.Button("Release leash"))
            Plugin.FireAndForget(plugin.FollowCommand.SendReleaseAsync());

        ImGui.Spacing();
        ImGui.TextDisabled("Leash distance/duration controls: placeholder - not implemented yet.");
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
