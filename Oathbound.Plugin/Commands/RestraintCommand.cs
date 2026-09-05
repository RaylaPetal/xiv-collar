using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Oathbound.Plugin.Config;
using Oathbound.Plugin.Ipc;
using Oathbound.Plugin.Safety;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Glamourer.Api.Enums;

namespace Oathbound.Plugin.Commands;

/// collar/restraints: applies a restraint device's single captured gear piece (locking exactly its one
/// equipment slot, via SlotLockManager - same "Restraints" owner name collar/slot-locking's spec already
/// reserves) and activates every restriction rule the device carries (via RestrictionRuleManager). Follows
/// OutfitCommand's exact two-tier shape: Sub self-apply/release via alias, Owner force-apply/force-unlock
/// "joker" override that locks out the Sub's own controls while active.
public sealed class RestraintCommand
{
    private const string ExportPrefix = "OATHBOUND-RESTRAINT-V1|";
    private const string ConfiguredExportPrefix = "OATHBOUND-RESTRAINT-CONFIG-V1|";
    public string? LastFailureReason { get; private set; }
    private const string Owner = "Restraints";
    private const long PlayDelayMs = 500;

    private readonly PluginConfig config;
    private readonly GlamourerIpc glamourer;
    private readonly PenumbraIpc penumbra;
    private readonly SlotLockManager slotLocks;
    private readonly RestrictionRuleManager restrictionRules;
    private readonly SubRuntimeState runtimeState;
    private readonly GestureCatalogScanner catalogScanner;
    private readonly TemporaryModSettingsCoordinator temporarySettings;
    public int? LastScanTotalMods { get; private set; }
    public int LastScanMatchedMods { get; private set; }
    public string? LastScanError { get; private set; }

    public RestraintCommand(PluginConfig config, GlamourerIpc glamourer, PenumbraIpc penumbra, SlotLockManager slotLocks, RestrictionRuleManager restrictionRules, SubRuntimeState runtimeState, TemporaryModSettingsCoordinator temporarySettings)
    {
        this.config = config;
        this.glamourer = glamourer;
        this.penumbra = penumbra;
        this.slotLocks = slotLocks;
        this.restrictionRules = restrictionRules;
        this.runtimeState = runtimeState;
        this.temporarySettings = temporarySettings;
        catalogScanner = new GestureCatalogScanner(penumbra, config);
    }

    public void RescanCatalog()
    {
        var scope = catalogScanner.ResolveRestraintScope();
        var result = catalogScanner.ScanMods(scope);
        LastScanTotalMods = result.TotalMods;
        LastScanMatchedMods = scope.Count;
        LastScanError = result.Error;
        if (result.Error is not null) return;
        config.RestraintMapping.LocalCatalog = result.Entries.ToDictionary(e => e.Id, e => new RestraintCatalogEntry
        {
            Id = e.Id, ModDirectory = e.ModDirectory, ModName = e.ModName,
            GroupSelections = e.SavedSelections, ModEnabled = e.ModEnabled, ChangedItemIds = e.ChangedItemIds.ToList(),
        });
        config.Save();
    }

    public string ExportCatalog() => string.Join("\n", config.RestraintMapping.LocalCatalog.Values
        .OrderBy(e => e.ModName)
        .Select(e => EncodeExport(RestraintCatalogExportEntry.From(e))));

    public static bool TryParseExport(string line, out RestraintCatalogExportEntry? entry)
    {
        entry = null;
        if (!line.StartsWith(ExportPrefix, StringComparison.Ordinal)) return false;
        try
        {
            entry = JsonSerializer.Deserialize<RestraintCatalogExportEntry>(Encoding.UTF8.GetString(Convert.FromBase64String(line[ExportPrefix.Length..])));
            return entry is { Id.Length: 16, ModName.Length: > 0 and <= 160 }
                && entry.Id.All(char.IsAsciiLetterOrDigit);
        }
        catch { return false; }
    }

    public static string EncodeExport(RestraintCatalogExportEntry entry) =>
        ExportPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry)));

    public static string EncodeConfiguredExport(ConfiguredModRestraintExportEntry entry) =>
        ConfiguredExportPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry)));

    public static bool TryParseConfiguredExport(string line, out ConfiguredModRestraintExportEntry? entry)
    {
        entry = null;
        if (!line.StartsWith(ConfiguredExportPrefix, StringComparison.Ordinal)) return false;
        try
        {
            entry = JsonSerializer.Deserialize<ConfiguredModRestraintExportEntry>(Encoding.UTF8.GetString(Convert.FromBase64String(line[ConfiguredExportPrefix.Length..])));
            return entry is { Id.Length: > 0 and <= 64, CatalogId.Length: 16, Name.Length: > 0 and <= 80,
                ItemId: > 0, Rules.Count: > 0 }
                && entry.CatalogId.All(char.IsAsciiLetterOrDigit);
        }
        catch { return false; }
    }

    /// Every currently-active device (Sub-applied or Owner-forced), for UI display.
    public IReadOnlySet<string> ActiveDeviceIds => activeDeviceIds;
    private readonly HashSet<string> activeDeviceIds = new();

    /// Arms Cuffed/Legs Cuffed/Full Body Cuffed rules each temporarily activate their own chosen animation
    /// mod - keyed by (device, rule kind) rather than just device, since one device can carry more than one
    /// bound-animation rule at once (collar/restraints "Arms Cuffed and Legs Cuffed can be active
    /// together"), each needing its own independent Penumbra temporary-activation to revert. Deliberately
    /// separate from GestureCommand's own `activeTemporary` tracking - a restraint's held animation must
    /// never be subject to Gesture's 30-second idle-timeout revert, and vice versa.
    private readonly Dictionary<(string DeviceId, RestraintRuleKind Kind), (Guid Collection, string ModDirectory)> boundAnimations = new();
    private readonly Dictionary<(string DeviceId, RestraintRuleKind Kind), (GestureTrigger Trigger, long ReadyAtTicks)> pendingBoundPlays = new();
    private readonly Dictionary<string, (Guid Collection, string ModDirectory)> activeCatalogOverrides = new();

    public bool IsActive(string deviceId) => activeDeviceIds.Contains(deviceId);

    /// Sub self-service: applying an already-active device's alias releases it instead (toggle) - see
    /// AliasBook.Restraints. Refused outright while an Owner force-lock is in effect, same as
    /// OutfitCommand.Apply/Unlock's OutfitForceLocked check.
    public bool Toggle(RestraintAliasDefinition alias)
    {
        if (runtimeState.RestraintsForceLocked)
            return false;

        return activeDeviceIds.Contains(alias.DeviceId) ? Release(alias.DeviceId) : Apply(alias.DeviceId);
    }

    private bool Apply(string deviceId)
    {
        if (!config.RestraintMapping.Devices.TryGetValue(deviceId, out var device))
            return false;

        return ApplyDevice(deviceId, device);
    }

    private bool Release(string deviceId)
    {
        if (!activeDeviceIds.Contains(deviceId))
            return false;

        ReleaseDevice(deviceId);
        return true;
    }

    /// The Owner's direct override: matches `deviceName` against the Sub's own captured device catalog
    /// (case-insensitive) - same lookup shape as OutfitCommand.ForceApply. Applies the device using its own
    /// stored rules. Always force-locks.
    public bool ForceApply(string deviceName)
    {
        LastFailureReason = null;
        var entry = config.RestraintMapping.Devices.Values
            .FirstOrDefault(d => string.Equals(d.Name, deviceName, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            LastFailureReason = $"device \"{deviceName}\" was not found";
            return false;
        }

        if (!ApplyDevice(entry.Id, entry))
            return false;

        runtimeState.RestraintsForceLocked = true;
        return true;
    }

    /// Applies a Sub-authored Custom Trigger's restraint action by its stable captured-device identity.
    /// A Custom Trigger is still an Owner command, so this deliberately does not use Toggle (which is
    /// Sub self-service and is refused after an Owner force-lock). Multiple calls from the same bundle may
    /// therefore add multiple compatible devices before leaving the category force-locked.
    public bool ForceApplyById(string deviceId)
    {
        LastFailureReason = null;
        if (!config.RestraintMapping.Devices.TryGetValue(deviceId, out var device))
        {
            LastFailureReason = $"saved device id {deviceId} is stale";
            return false;
        }

        // Re-running an apply-only bundle is idempotent: do not toggle an active device back off or try to
        // acquire its already-held restriction claims again.
        if (activeDeviceIds.Contains(deviceId))
        {
            runtimeState.RestraintsForceLocked = true;
            return true;
        }

        if (!ApplyDevice(device.Id, device))
            return false;

        runtimeState.RestraintsForceLocked = true;
        return true;
    }

    /// The Owner's rule-carrying override (collar/restraints "Owner force-apply and force-release
    /// override"): matches `deviceName` against every captured device, and activates exactly the rules the
    /// Owner assigned to their quick command, ignoring whatever rules the Sub may have separately assigned
    /// to that same device.
    public bool ForceApply(string deviceName, List<RestraintRuleAssignment> rules)
    {
        var captured = config.RestraintMapping.Devices.Values
            .FirstOrDefault(d => string.Equals(d.Name, deviceName, StringComparison.OrdinalIgnoreCase));
        if (captured is null)
            return false;

        var device = new RestraintDeviceDefinition
        {
            Id = captured.Id,
            Slot = captured.Slot,
            ItemId = captured.ItemId,
            Stain = captured.Stain,
            Stain2 = captured.Stain2,
            Name = captured.Name,
            Rules = rules,
        };

        if (!ApplyDevice(device.Id, device))
            return false;

        runtimeState.RestraintsForceLocked = true;
        return true;
    }

    /// The Owner's ad-hoc override (collar/restraints "Owner-authored ad-hoc restraint device"): the Owner
    /// picked `slot`/`itemId` directly, with no Sub-side captured device to look up by name. The runtime
    /// device id is derived deterministically from slot+item (design.md's "Ad-hoc device identity") rather
    /// than a stored `RestraintDeviceDefinition.Id`, so conflict tracking and release work exactly like a
    /// name-referenced device without needing one to exist in the Sub's own catalog.
    public bool ForceApplyAdHoc(ApiEquipSlot slot, ulong itemId, string label, List<RestraintRuleAssignment> rules)
    {
        var device = new RestraintDeviceDefinition
        {
            Id = $"adhoc:{slot}:{itemId}",
            Slot = slot,
            ItemId = itemId,
            Stain = 0,
            Stain2 = 0,
            Name = label,
            Rules = rules,
        };

        if (!ApplyDevice(device.Id, device))
            return false;

        runtimeState.RestraintsForceLocked = true;
        return true;
    }

    public unsafe bool ForceApplyCatalog(string catalogId, ulong itemId, List<RestraintRuleAssignment> rules)
    {
        LastFailureReason = null;
        if (!config.RestraintMapping.LocalCatalog.TryGetValue(catalogId, out var entry))
        {
            LastFailureReason = "that shared restraint is stale or no longer allowed";
            return false;
        }
        var runtimeId = $"catalog:{catalogId}";
        if (activeCatalogOverrides.ContainsKey(runtimeId))
            return true;
        var slot = itemId == 0 ? null : GlamourerIpc.GetItemSlot((uint)itemId);
        if (itemId == 0 || slot is null || slotLocks.WouldOverlap([slot.Value], Owner))
        {
            LastFailureReason = itemId == 0 || slot is null ? "the restraint item is not valid for an equipment slot" : $"equipment slot {slot} is locked by another feature";
            return false;
        }
        if (restrictionRules.WouldConflict(rules, runtimeId))
        {
            LastFailureReason = "a restriction rule conflicts with an active restraint";
            return false;
        }
        if (!restrictionRules.CanActivate(rules, out var unavailable))
        {
            LastFailureReason = $"{unavailable} enforcement is unavailable";
            return false;
        }
        var boundRules = rules.Where(r => r.Kind is RestraintRuleKind.ArmsCuffed or RestraintRuleKind.LegsCuffed or RestraintRuleKind.FullBodyCuffed).ToList();
        if (boundRules.Any(r => ResolveAnimation(r.AnimationId) is null))
        {
            LastFailureReason = "a selected cuff animation is missing, stale, or ambiguous";
            return false;
        }
        if (rules.Any(r => r.Kind == RestraintRuleKind.ForcedPose) && PlayerState.Instance() == null)
        {
            LastFailureReason = "the local pose state is unavailable";
            return false;
        }
        var collection = penumbra.TryGetLocalPlayerCollectionId();
        if (collection is null) { LastFailureReason = "the local Penumbra collection is unavailable"; return false; }
        var selections = entry.GroupSelections.ToDictionary(x => x.Key, x => (IReadOnlyList<string>)x.Value);
        if (!temporarySettings.Acquire(runtimeId, collection.Value, entry.ModDirectory, selections))
        { LastFailureReason = "Penumbra could not apply the restraint option"; return false; }
        if (!penumbra.TryRedrawLocalPlayer())
        {
            temporarySettings.Release(runtimeId, collection.Value, entry.ModDirectory);
            LastFailureReason = "Penumbra could not redraw after applying the restraint";
            return false;
        }
        if (!slotLocks.TryLock(Owner, new Dictionary<ApiEquipSlot, SlotLockValue> { [slot.Value] = new(itemId, 0, 0) }))
        {
            temporarySettings.Release(runtimeId, collection.Value, entry.ModDirectory);
            penumbra.TryRedrawLocalPlayer();
            LastFailureReason = $"Glamourer could not equip or lock {slot}";
            return false;
        }
        if (rules.Count > 0 && !restrictionRules.TryActivate(runtimeId, rules))
        {
            slotLocks.Release(Owner);
            temporarySettings.Release(runtimeId, collection.Value, entry.ModDirectory);
            penumbra.TryRedrawLocalPlayer();
            LastFailureReason = "restriction enforcement could not be activated";
            return false;
        }
        activeCatalogOverrides[runtimeId] = (collection.Value, entry.ModDirectory);
        var pose = rules.FirstOrDefault(r => r.Kind == RestraintRuleKind.ForcedPose);
        if (pose is not null) ApplyPose(pose.PoseModeId);
        foreach (var rule in boundRules)
        {
            if (EngageBoundAnimation(runtimeId, rule)) continue;
            ReleaseBoundAnimations(runtimeId);
            restrictionRules.Release(runtimeId);
            slotLocks.Release(Owner);
            temporarySettings.Release(runtimeId, collection.Value, entry.ModDirectory);
            activeCatalogOverrides.Remove(runtimeId);
            penumbra.TryRedrawLocalPlayer();
            LastFailureReason = "the selected cuff animation could not be activated; the restraint was rolled back";
            return false;
        }
        activeDeviceIds.Add(runtimeId);
        runtimeState.RestraintsForceLocked = true;
        return true;
    }

    /// The only thing that can release every Owner-forced device besides panic.
    public bool ForceUnlock()
    {
        // This is a safety teardown, not an ordinary per-device toggle. Slot locks survive reloads in
        // config, while activeDeviceIds and restriction claims deliberately do not; relying only on the
        // latter made an Owner unlock falsely report success while leaving persisted Glamourer gear in
        // place. Release each layer independently and unconditionally so partial/stale state heals too.
        var hadRestraints = activeDeviceIds.Count > 0 || boundAnimations.Count > 0 || activeCatalogOverrides.Count > 0 || runtimeState.RestraintsForceLocked;
        restrictionRules.ReleaseAllForPanic();
        ReleaseAllBoundAnimationsForPanic();
        ReleaseAllCatalogOverrides();
        var gearReleased = slotLocks.Release(Owner);
        runtimeState.RestraintsForceLocked = false;
        return gearReleased || hadRestraints;
    }

    /// Advances delayed bound-animation triggers after Penumbra's redraw has settled. Playing the emote
    /// immediately after requesting redraw races the character rebuild and commonly results in no visible
    /// animation; GestureCommand uses the same framework-thread delay for this reason.
    public void OnFrameworkUpdate()
    {
        var now = Environment.TickCount64;
        foreach (var (key, pending) in pendingBoundPlays.Where(x => now >= x.Value.ReadyAtTicks).ToList())
        {
            pendingBoundPlays.Remove(key);
            if (boundAnimations.ContainsKey(key))
                GestureCommand.Play(pending.Trigger);
        }
    }

    /// Applies a device's single captured gear piece (locking its one equipment slot, via SlotLockManager -
    /// refused if that slot is already locked by a different owner) and activates every rule it carries
    /// (via RestrictionRuleManager - refused if a rule conflicts with an already-active one). Both checks
    /// run before anything is applied, and both must pass, so a refused apply never leaves a partial visual
    /// or rule change behind - same "refuse the whole action" guarantee OutfitCommand.ApplyDesign gives.
    private unsafe bool ApplyDevice(string deviceId, RestraintDeviceDefinition device)
    {
        if (slotLocks.WouldOverlap([device.Slot], Owner))
        {
            var conflicts = slotLocks.ConflictingLocks([device.Slot], Owner);
            LastFailureReason = conflicts.Count > 0
                ? $"equipment slot {device.Slot} is locked by {conflicts[0].Owner}"
                : $"equipment slot {device.Slot} is already locked";
            Plugin.Log.Warning($"Restraint apply refused for \"{device.Name}\": a locked slot is already held by a different owner.");
            return false;
        }
        if (restrictionRules.WouldConflict(device.Rules, deviceId))
        {
            LastFailureReason = "a pose or cuff rule conflicts with a different active restraint; unlock existing restraints first";
            Plugin.Log.Warning($"Restraint apply refused for \"{device.Name}\": a restriction rule conflicts with a different device already active.");
            return false;
        }
        if (!restrictionRules.CanActivate(device.Rules, out var unavailable))
        {
            LastFailureReason = $"{unavailable} enforcement is unavailable";
            Plugin.Log.Warning($"Restraint apply refused for '{device.Name}': {unavailable} enforcement is unavailable.");
            return false;
        }
        if (device.Rules.Any(r => r.Kind == RestraintRuleKind.ForcedPose) && PlayerState.Instance() == null)
        {
            LastFailureReason = "the local pose state is unavailable";
            Plugin.Log.Warning($"Restraint apply refused for '{device.Name}': pose state is unavailable.");
            return false;
        }
        var boundRules = device.Rules.Where(r => r.Kind is RestraintRuleKind.ArmsCuffed or RestraintRuleKind.LegsCuffed or RestraintRuleKind.FullBodyCuffed).ToList();
        if (boundRules.Any(r => ResolveAnimation(r.AnimationId) is null))
        {
            LastFailureReason = "a selected cuff animation is missing, stale, or ambiguous; edit the restraint and select it again";
            Plugin.Log.Warning($"Restraint apply refused for '{device.Name}': a bound animation is missing, stale, or ambiguous.");
            return false;
        }

        var value = new SlotLockValue(device.ItemId, device.Stain, device.Stain2);
        if (!slotLocks.TryLock(Owner, new Dictionary<ApiEquipSlot, SlotLockValue> { [device.Slot] = value }))
        {
            LastFailureReason = $"Glamourer could not apply or lock equipment slot {device.Slot}";
            Plugin.Log.Warning($"Restraint apply failed for \"{device.Name}\": could not apply/lock its slot.");
            return false;
        }

        if (device.Rules.Count > 0 && !restrictionRules.TryActivate(deviceId, device.Rules))
        {
            LastFailureReason = "restriction enforcement could not be activated";
            slotLocks.Release(Owner);
            return false;
        }

        var pose = device.Rules.FirstOrDefault(r => r.Kind == RestraintRuleKind.ForcedPose);
        if (pose is not null)
            ApplyPose(pose.PoseModeId);

        foreach (var rule in boundRules)
        {
            if (EngageBoundAnimation(deviceId, rule)) continue;
            ReleaseBoundAnimations(deviceId);
            restrictionRules.Release(deviceId);
            slotLocks.Release(Owner);
            Plugin.Log.Warning($"Restraint apply rolled back for '{device.Name}': bound animation activation failed.");
            LastFailureReason = "Penumbra could not activate the selected cuff animation; the restraint was rolled back";
            return false;
        }

        activeDeviceIds.Add(deviceId);
        return true;
    }

    /// One-shot: places the character into the configured pose via the game's own pose-set + emote command,
    /// the same mechanism GestureCommand.Play uses for a gesture's tied trigger. Distinct from the ongoing
    /// movement suppression MovementLockEnforcer provides - this only fires once, at apply time.
    private static unsafe void ApplyPose(int poseModeId)
    {
        var playerState = PlayerState.Instance();
        if (playerState == null || poseModeId is < 1 or > 3)
            return;

        var poseType = poseModeId switch
        {
            1 => EmoteController.PoseType.GroundSit,
            2 => EmoteController.PoseType.Sit,
            3 => EmoteController.PoseType.Doze,
            _ => throw new ArgumentOutOfRangeException(nameof(poseModeId)),
        };
        playerState->SelectedPoses[(int)poseType] = 0;
        Chat.SendMessage(poseModeId switch { 1 => "/groundsit", 2 => "/sit", 3 => "/doze", _ => "" });
    }

    /// collar/restraints "Arms Cuffed and Legs Cuffed rules lock the Sub into a chosen bound animation":
    /// temporarily activates the rule's chosen animation's mod/options (the same Penumbra call
    /// GestureCommand.Execute uses) and plays its tied trigger once - a pose trigger then holds naturally
    /// (the game's own idle-pose persists until changed), a slash-emote trigger plays once. Tracked
    /// separately per (device, rule kind) in `boundAnimations` so it is never subject to Gesture's own
    /// idle-timeout revert, and reverted explicitly in ReleaseDevice/ReleaseAllBoundAnimationsForPanic
    /// instead. Silently does nothing if the animation is missing, stale, or Penumbra is unavailable - the
    /// device still applies its other rules/slot lock even if the bound animation can't engage.
    private GestureCatalogEntry? ResolveAnimation(string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector)) return null;
        var resolved = CommandSelector.ResolveGestureDetailed(config.GestureMapping.LocalCatalog.Values, selector, requireTrigger: false).Entry;
        if (resolved is not null)
            return resolved;

        // The current restraint wire format uses commas between rules, so BuildLockCommand escaped
        // commas inside readable animation labels as a middle dot. Restore that presentation escape
        // before local catalog resolution. Without this, real options such as
        // "Get Cuffed (Gsit2,idle,walk)" arrive as "Gsit2·idle·walk" and always fail preflight.
        return selector.Contains('·')
            ? CommandSelector.ResolveGestureDetailed(config.GestureMapping.LocalCatalog.Values, selector.Replace('·', ','), requireTrigger: false).Entry
            : null;
    }

    private bool EngageBoundAnimation(string deviceId, RestraintRuleAssignment rule)
    {
        if (ResolveAnimation(rule.AnimationId) is not { } entry
            || string.IsNullOrWhiteSpace(entry.ModDirectory)
            || entry.GroupSelections.Count == 0)
        {
            Plugin.Log.Warning($"Restraint {rule.Kind} rule refused to engage: animation '{rule.AnimationId}' is unavailable.");
            return false;
        }

        var collection = penumbra.TryGetLocalPlayerCollectionId();
        if (collection is null)
            return false;

        var selections = entry.GroupSelections.ToDictionary(x => x.Key, x => (IReadOnlyList<string>)x.Value);
        var claimOwner = $"bound:{deviceId}:{rule.Kind}";
        if (!temporarySettings.Acquire(claimOwner, collection.Value, entry.ModDirectory, selections))
            return false;
        if (!penumbra.TryRedrawLocalPlayer())
        {
            temporarySettings.Release(claimOwner, collection.Value, entry.ModDirectory);
            return false;
        }

        boundAnimations[(deviceId, rule.Kind)] = (collection.Value, entry.ModDirectory);
        if (entry.Trigger is not null)
            pendingBoundPlays[(deviceId, rule.Kind)] = (entry.Trigger, Environment.TickCount64 + PlayDelayMs);
        return true;
    }

    private void ReleaseBoundAnimations(string deviceId)
    {
        var removedAny = false;
        foreach (var key in boundAnimations.Keys.Where(k => k.DeviceId == deviceId).ToList())
        {
            pendingBoundPlays.Remove(key);
            var (collection, modDirectory) = boundAnimations[key];
            removedAny |= temporarySettings.Release($"bound:{deviceId}:{key.Kind}", collection, modDirectory);
            boundAnimations.Remove(key);
        }
        if (removedAny && !penumbra.TryRedrawLocalPlayer())
            Plugin.Log.Warning($"Restraint release for '{deviceId}' removed temporary animation settings, but the Penumbra redraw failed; a manual redraw may be required.");
    }

    /// collar/restraints "Panic releases every active restriction rule": reverts every currently-held bound
    /// animation regardless of which device engaged it, and drops the (by then already-stale) active-device
    /// bookkeeping - mirrors RestrictionRuleManager.ReleaseAllForPanic's "drop bookkeeping unconditionally"
    /// shape, since Panic's own SlotLockManager/RestrictionRuleManager steps already tear down everything
    /// else this class doesn't own.
    public void ReleaseAllBoundAnimationsForPanic()
    {
        var removedAny = false;
        foreach (var (key, value) in boundAnimations)
            removedAny |= temporarySettings.Release($"bound:{key.DeviceId}:{key.Kind}", value.Collection, value.ModDirectory);
        boundAnimations.Clear();
        pendingBoundPlays.Clear();
        activeDeviceIds.Clear();
        ReleaseAllCatalogOverrides();
        if (removedAny && !penumbra.TryRedrawLocalPlayer())
            Plugin.Log.Warning("Restraint teardown removed temporary animation settings, but the Penumbra redraw failed; a manual redraw may be required.");
    }

    private void ReleaseAllCatalogOverrides()
    {
        var removed = false;
        foreach (var (owner, value) in activeCatalogOverrides)
            removed |= temporarySettings.Release(owner, value.Collection, value.ModDirectory);
        foreach (var id in activeCatalogOverrides.Keys)
            restrictionRules.Release(id);
        activeCatalogOverrides.Clear();
        if (removed) penumbra.TryRedrawLocalPlayer();
    }

    private void ReleaseDevice(string deviceId)
    {
        restrictionRules.Release(deviceId);
        activeDeviceIds.Remove(deviceId);
        ReleaseBoundAnimations(deviceId);

        // Only release the shared "Restraints" slot lock once no other active device still needs it -
        // SlotLockManager.Release tears down every slot the owner holds, so this must wait until the last
        // active device releases, mirroring RestrictionRuleManager's own refcounting.
        if (activeDeviceIds.Count == 0)
            slotLocks.Release(Owner);
    }

    /// Sub-side: captures a new restraint device from a slot+item picked in `ItemPickerWindow` - collar/
    /// restraints "Restraint device captured from a single equipped gear piece." Undyed (stain 0/0) - no
    /// dye picker in this flow yet (design.md's Non-Goals). Never touches live Glamourer state; the item
    /// does not need to be currently equipped or owned.
    public bool CaptureDeviceFromItem(ApiEquipSlot slot, ulong itemId, string name, List<RestraintRuleAssignment> rules)
    {
        var device = new RestraintDeviceDefinition
        {
            Slot = slot,
            ItemId = itemId,
            Stain = 0,
            Stain2 = 0,
            Name = name,
            Rules = rules,
        };
        config.RestraintMapping.Devices[device.Id] = device;
        config.Save();
        return true;
    }

    public void RemoveDevice(string id)
    {
        if (activeDeviceIds.Contains(id))
            ReleaseDevice(id);
        config.RestraintMapping.Devices.Remove(id);
        config.Save();
    }

    private const string RulesToken = "rules:";

    /// Builds the chat text for an Owner's rule-carrying restraint quick command: the device name always
    /// quoted (so TryParseLockCommand can find where it ends) followed by a `rules:` token listing every
    /// assigned rule. An older paired Sub client that doesn't understand this suffix still sees a quoted
    /// name it won't match against its own unquoted device names, so it fails closed (no action) rather
    /// than applying the wrong rules - see design.md's "additive, gracefully-degrading payload" decision.
    public static string BuildLockCommand(string deviceName, List<RestraintRuleAssignment> rules)
    {
        var tokens = rules.Select(r => r.Kind switch
        {
            RestraintRuleKind.ForcedPose => $"pose={r.PoseModeId}",
            RestraintRuleKind.WalkOnly => "walkonly",
            RestraintRuleKind.ActionBlock => "actionblock",
            RestraintRuleKind.GagChat => "gag",
            RestraintRuleKind.ArmsCuffed => $"armscuffed={ReadableAnimation(r)}",
            RestraintRuleKind.LegsCuffed => $"legscuffed={ReadableAnimation(r)}",
            RestraintRuleKind.FullBodyCuffed => $"fullbodycuffed={ReadableAnimation(r)}",
            _ => "",
        }).Where(t => t.Length > 0);

        return $"restraint lock \"{deviceName}\" {RulesToken}{string.Join(',', tokens)}";
    }

    public static string BuildCatalogLockCommand(string catalogId, string label, ulong itemId, List<RestraintRuleAssignment> rules)
    {
        if (catalogId.Length is < 8 or > 64 || catalogId.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new ArgumentException("Invalid restraint catalog identity.", nameof(catalogId));
        var ruleText = BuildLockCommand(label.Replace('"', '\''), rules);
        var suffix = ruleText[(ruleText.IndexOf(RulesToken, StringComparison.Ordinal) + RulesToken.Length)..];
        return $"restraint catalog {catalogId} \"{label.Replace('"', '\'')}\" item:{itemId} {RulesToken}{suffix}";
    }

    public static bool TryParseCatalogCommand(string remainder, out string catalogId, out ulong itemId, out List<RestraintRuleAssignment> rules)
    {
        catalogId = ""; itemId = 0; rules = [];
        if (remainder.Length > 400) return false;
        var (id, tail) = SplitFirstToken(remainder);
        if (id.Length is < 8 or > 64 || id.Any(c => !char.IsAsciiLetterOrDigit(c))) return false;
        if (!tail.StartsWith('"')) return false;
        var closing = tail.IndexOf('"', 1);
        if (closing < 0) return false;
        var after = tail[(closing + 1)..].Trim();
        var (itemToken, afterItem) = SplitFirstToken(after);
        if (!itemToken.StartsWith("item:", StringComparison.OrdinalIgnoreCase) ||
            !ulong.TryParse(itemToken[5..], out itemId) || itemId == 0)
            return false;
        after = afterItem.Trim();
        if (!after.StartsWith(RulesToken, StringComparison.OrdinalIgnoreCase)) return false;
        rules = ParseRuleTokens(after[RulesToken.Length..]);
        catalogId = id;
        return rules.Count > 0;
    }

    /// Parses the remainder of a `restraint lock ...` command (after the "lock " prefix) into a device
    /// name and, if present, the Owner-assigned rules carried in a `rules:` suffix. A legacy plain/unquoted
    /// name (no quotes, no rule suffix) parses as before - the whole remainder is the name, rules null -
    /// preserving the pre-existing Sub-tag lookup path for hand-typed overrides and stale saved commands.
    public static bool TryParseLockCommand(string remainder, out string deviceName, out List<RestraintRuleAssignment>? rules)
    {
        rules = null;
        var trimmed = remainder.Trim();

        if (trimmed.StartsWith('"'))
        {
            var closing = trimmed.IndexOf('"', 1);
            if (closing < 0)
            {
                deviceName = trimmed.Trim('"');
                return deviceName.Length > 0;
            }

            deviceName = trimmed[1..closing];
            var tail = trimmed[(closing + 1)..].Trim();
            if (tail.StartsWith(RulesToken, StringComparison.OrdinalIgnoreCase))
                rules = ParseRuleTokens(tail[RulesToken.Length..]);

            return deviceName.Length > 0;
        }

        deviceName = trimmed;
        return deviceName.Length > 0;
    }

    /// Builds the chat text for an Owner-authored ad-hoc restraint device (collar/restraints "Owner-
    /// authored ad-hoc restraint device"): carries the full slot/item/label/rules definition inline, since
    /// there is no Sub-side name to look up - see design.md's "Wire grammar" decision for why this is a
    /// separate sub-verb (`wear`) rather than an extension of `lock`'s name-lookup shape.
    public static string BuildWearCommand(ApiEquipSlot slot, ulong itemId, string label, List<RestraintRuleAssignment> rules)
    {
        var tokens = rules.Select(r => r.Kind switch
        {
            RestraintRuleKind.ForcedPose => $"pose={r.PoseModeId}",
            RestraintRuleKind.WalkOnly => "walkonly",
            RestraintRuleKind.ActionBlock => "actionblock",
            RestraintRuleKind.GagChat => "gag",
            RestraintRuleKind.ArmsCuffed => $"armscuffed={ReadableAnimation(r)}",
            RestraintRuleKind.LegsCuffed => $"legscuffed={ReadableAnimation(r)}",
            RestraintRuleKind.FullBodyCuffed => $"fullbodycuffed={ReadableAnimation(r)}",
            _ => "",
        }).Where(t => t.Length > 0);

        return $"restraint wear {slot} {itemId} \"{label}\" {RulesToken}{string.Join(',', tokens)}";
    }

    /// Parses the remainder of a `restraint wear ...` command (after the "wear " prefix) into a slot, item
    /// id, label, and Owner-assigned rules. Fails closed (returns false) on any malformed segment - an
    /// ad-hoc device with no rules is meaningless (nothing would activate), so this never silently applies
    /// a bare gear swap.
    public static bool TryParseWearCommand(string remainder, out ApiEquipSlot slot, out ulong itemId, out string label, out List<RestraintRuleAssignment> rules)
    {
        slot = default;
        itemId = 0;
        label = "";
        rules = [];

        var (slotToken, afterSlot) = SplitFirstToken(remainder);
        if (!Enum.TryParse(slotToken, true, out slot))
            return false;

        var (itemToken, afterItem) = SplitFirstToken(afterSlot);
        if (!ulong.TryParse(itemToken, out itemId))
            return false;

        var trimmed = afterItem.Trim();
        if (!trimmed.StartsWith('"'))
            return false;

        var closing = trimmed.IndexOf('"', 1);
        if (closing < 0)
            return false;

        label = trimmed[1..closing];
        var tail = trimmed[(closing + 1)..].Trim();
        if (tail.StartsWith(RulesToken, StringComparison.OrdinalIgnoreCase))
            rules = ParseRuleTokens(tail[RulesToken.Length..]);

        return label.Length > 0 && rules.Count > 0;
    }

    private static (string First, string Remainder) SplitFirstToken(string text)
    {
        var trimmed = text.Trim();
        var spaceIndex = trimmed.IndexOf(' ');
        return spaceIndex < 0 ? (trimmed, "") : (trimmed[..spaceIndex], trimmed[(spaceIndex + 1)..].Trim());
    }

    private static List<RestraintRuleAssignment> ParseRuleTokens(string tokens)
    {
        var rules = new List<RestraintRuleAssignment>();
        foreach (var token in tokens.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.StartsWith("pose=", StringComparison.OrdinalIgnoreCase) && int.TryParse(token.AsSpan(5), out var poseId))
                rules.Add(new RestraintRuleAssignment { Kind = RestraintRuleKind.ForcedPose, PoseModeId = poseId });
            else if (token.Equals("walkonly", StringComparison.OrdinalIgnoreCase))
                rules.Add(new RestraintRuleAssignment { Kind = RestraintRuleKind.WalkOnly });
            else if (token.Equals("actionblock", StringComparison.OrdinalIgnoreCase))
                rules.Add(new RestraintRuleAssignment { Kind = RestraintRuleKind.ActionBlock });
            else if (token.Equals("gag", StringComparison.OrdinalIgnoreCase))
                rules.Add(new RestraintRuleAssignment { Kind = RestraintRuleKind.GagChat });
            else if (token.StartsWith("armscuffed=", StringComparison.OrdinalIgnoreCase))
                rules.Add(new RestraintRuleAssignment { Kind = RestraintRuleKind.ArmsCuffed, AnimationId = token["armscuffed=".Length..] });
            else if (token.StartsWith("legscuffed=", StringComparison.OrdinalIgnoreCase))
                rules.Add(new RestraintRuleAssignment { Kind = RestraintRuleKind.LegsCuffed, AnimationId = token["legscuffed=".Length..] });
            else if (token.StartsWith("fullbodycuffed=", StringComparison.OrdinalIgnoreCase))
                rules.Add(new RestraintRuleAssignment { Kind = RestraintRuleKind.FullBodyCuffed, AnimationId = token["fullbodycuffed=".Length..] });
        }
        return rules;
    }

    private static string ReadableAnimation(RestraintRuleAssignment rule) =>
        (string.IsNullOrWhiteSpace(rule.AnimationLabel) ? rule.AnimationId : rule.AnimationLabel)!.Replace(',', '·');

    /// collar/catalog-sync: every captured device's display name, deduplicated - same plain-name export
    /// shape OutfitCommand/MoodlesCommand provide, since a restraint device is identified by name alone
    /// for Owner purposes (ChatCommandListener's `restraint lock <name>` grammar).
    public IReadOnlyList<string> ExportNames() =>
        config.RestraintMapping.Devices.Values.Select(d => d.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

    public IEnumerable<string> ExportEntries() =>
        ExportCatalog().Split('\n', StringSplitOptions.RemoveEmptyEntries).Concat(
            config.RestraintMapping.ConfiguredMods
                .Where(x => config.RestraintMapping.LocalCatalog.ContainsKey(x.CatalogId) && x.Rules.Count > 0 && x.ItemId > 0)
                .OrderBy(x => x.Name)
                .Select(x => EncodeConfiguredExport(ConfiguredModRestraintExportEntry.From(x))));
}
