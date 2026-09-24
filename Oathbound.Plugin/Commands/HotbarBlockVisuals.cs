using System;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;

namespace Oathbound.Plugin.Commands;

/// collar/restraint-restrictions "Action Block is visible on the Sub's hotbars": the visual half of
/// ActionBlockService - enforcement stays in its UseAction detour, this only makes the block visible. Same
/// approach as GagSpeak's HotbarActionController/AddonHotbar: nothing is drawn, every blockable slot is
/// swapped in memory (`RaptureHotbarModule.Hotbars`) to a real action the player can never use, so the game
/// itself renders its greyed icon with the red slash. `Set` never touches `SavedHotbars`, so
/// `LoadSavedHotbar` restores the exact saved layout, and a crash/relog needs no cleanup at all. The hotbar is
/// locked (and its lock toggle hidden) while shown so the Sub can't drag a placeholder into another slot -
/// the one path where the game would write a placeholder into the saved layout.
///
/// Every entry point is best-effort and swallows its own failures: a broken visual must never affect the
/// block itself (fail-closed on the block, fail-open on the visuals).
public sealed unsafe class HotbarBlockVisuals
{
    /// One of GagSpeak's always-unusable override actions (its BoundArms override) - the game renders it
    /// greyed out with the red slash on every job.
    private const uint PlaceholderActionId = 68;

    /// `_ActionBar`'s lock toggle component node and its lock callback, as used by GagSpeak's AddonHotbar
    /// (itself from SimpleTweaks).
    private const uint LockToggleNodeId = 21;

    private bool shown;
    private bool? lockedBefore;

    public bool IsShown => shown;

    public void Show()
    {
        shown = true;
        SwapBlockableSlots();
        LockAndHideToggle();
    }

    public void Hide()
    {
        if (!shown)
            return;
        shown = false;
        RestoreSavedHotbars();
        RestoreLock();
    }

    /// collar/restraint-restrictions "Job change while blocked": job, gearset and PvP changes make the game
    /// reload its hotbars from the saved layout, undoing the swap. Rather than hooking every reload path,
    /// re-swap whatever blockable slot shows up while shown - cheap (a few hundred slot reads) and
    /// idempotent, since already-swapped slots hold the placeholder and are skipped.
    public void OnFrameworkUpdate()
    {
        if (shown)
            SwapBlockableSlots();
    }

    private static bool IsBlockable(RaptureHotbarModule.HotbarSlot* slot) =>
        slot->CommandType switch
        {
            RaptureHotbarModule.HotbarSlotType.Action => slot->CommandId != PlaceholderActionId,
            RaptureHotbarModule.HotbarSlotType.GeneralAction
                or RaptureHotbarModule.HotbarSlotType.Item
                or RaptureHotbarModule.HotbarSlotType.CraftAction
                or RaptureHotbarModule.HotbarSlotType.PetAction => true,
            _ => false,
        };

    private static RaptureHotbarModule* HotbarModule()
    {
        var framework = Framework.Instance();
        if (framework == null)
            return null;
        var uiModule = framework->GetUIModule();
        return uiModule == null ? null : uiModule->GetRaptureHotbarModule();
    }

    private static void SwapBlockableSlots()
    {
        try
        {
            var module = HotbarModule();
            if (module == null)
                return;

            var hotbars = module->Hotbars;
            for (var i = 0; i < hotbars.Length; i++)
            {
                var hotbar = hotbars.GetPointer(i);
                var slots = hotbar->Slots;
                for (var j = 0; j < slots.Length; j++)
                {
                    var slot = slots.GetPointer(j);
                    if (IsBlockable(slot))
                        slot->Set(module->UIModule, RaptureHotbarModule.HotbarSlotType.Action, PlaceholderActionId);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "HotbarBlockVisuals: failed to grey out hotbar slots.");
        }
    }

    private static void RestoreSavedHotbars()
    {
        try
        {
            var module = HotbarModule();
            var playerState = PlayerState.Instance();
            if (module == null || playerState == null)
                return;

            var jobId = (uint)playerState->CurrentClassJobId;
            for (var i = 0; i < module->Hotbars.Length; i++)
                module->LoadSavedHotbar(jobId, (uint)i);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "HotbarBlockVisuals: failed to restore saved hotbars - relogging restores them.");
        }
    }

    private static AddonActionBarBase* ActionBar()
    {
        var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("_ActionBar").Address;
        return addon == null || !addon->IsReady ? null : (AddonActionBarBase*)addon;
    }

    private void LockAndHideToggle()
    {
        try
        {
            var bar = ActionBar();
            if (bar == null)
            {
                Plugin.Log.Warning("HotbarBlockVisuals: _ActionBar not ready - hotbar left unlocked.");
                return;
            }

            lockedBefore ??= bar->IsLocked;
            if (!bar->IsLocked)
                SetLocked(bar, true);
            SetToggleVisible(bar, false);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "HotbarBlockVisuals: failed to lock the hotbar.");
        }
    }

    private void RestoreLock()
    {
        try
        {
            var wasLocked = lockedBefore;
            lockedBefore = null;
            var bar = ActionBar();
            if (bar == null)
                return;

            if (wasLocked == false && bar->IsLocked)
                SetLocked(bar, false);
            SetToggleVisible(bar, true);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "HotbarBlockVisuals: failed to restore the hotbar lock state.");
        }
    }

    private static void SetLocked(AddonActionBarBase* bar, bool locked) =>
        Callback.Fire((AtkUnitBase*)bar, true, 9, 3, 51u, 0u, locked);

    private static void SetToggleVisible(AddonActionBarBase* bar, bool visible)
    {
        var node = ((AtkUnitBase*)bar)->GetNodeById(LockToggleNodeId);
        if (node != null)
            node->ToggleVisibility(visible);
    }
}
