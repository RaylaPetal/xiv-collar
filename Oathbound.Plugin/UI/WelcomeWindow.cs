using System;
using System.Numerics;
using Oathbound.Plugin.Config;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Oathbound.Plugin.UI;

/// collar/onboarding "Welcome window appears once on first plugin load": a deliberately minimal, distinct
/// window (not a mode of CollarWindow, per design.md's "Welcome window is a distinct Window" decision) shown
/// before CollarWindow's own nav bar/character header - both of which assume Role/pairing state already
/// exists - would otherwise render confusingly incomplete. Role and trigger phrase write straight to
/// `PluginConfig.Role`/`TriggerPhrase`, the same fields Settings' Identity & Pairing tab reads and writes -
/// there is no separate onboarding-only copy of either value.
public sealed class WelcomeWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string triggerPhraseInput = "";
    private static readonly string[] RoleNames = ["Sub", "Owner"];

    public WelcomeWindow(Plugin plugin) : base("Welcome to Oathbound###OathboundWelcome")
    {
        this.plugin = plugin;
        Flags = ImGuiWindowFlags.NoCollapse;
        Size = new Vector2(480, 320);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(420, 280), MaximumSize = new Vector2(700, 500) };
    }

    public void Dispose() { }

    public override void OnOpen() => triggerPhraseInput = plugin.Configuration.TriggerPhrase;

    public override void Draw()
    {
        var config = plugin.Configuration;

        ImGui.TextWrapped("Welcome! Before you get started, choose your role and the trigger phrase your commands will use - you can change either later in Settings.");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var roleIndex = config.Role == PluginRole.Owner ? 1 : 0;
        if (ImGui.Combo("Role", ref roleIndex, RoleNames, RoleNames.Length))
            config.Role = roleIndex == 1 ? PluginRole.Owner : PluginRole.Sub;
        IconGlyph.HelpMarker("Sub reacts to trigger tells and applies commands locally - only Sub actually gates anything. Owner is mostly informational. You can switch roles later in Settings.");

        ImGui.Spacing();
        ImGui.InputTextWithHint("Trigger phrase", "e.g. command", ref triggerPhraseInput, 32);
        IconGlyph.HelpMarker("The word that must start every ongoing command tell, e.g. \"command strip\".");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        IconGlyph.WrappedDisabled("After you continue, a short guided tour will walk you through each tab.");

        if (ImGui.Button("Continue"))
        {
            config.TriggerPhrase = triggerPhraseInput;
            config.HasCompletedWelcome = true;
            config.Save();
            IsOpen = false;
            plugin.TutorialDriver.StartIfUnseen(config.Role);
        }
    }
}
