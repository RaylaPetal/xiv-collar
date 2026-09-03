using System;
using System.Collections.Generic;
using System.Linq;
using CollarSystem.Plugin.Config;
using CollarSystem.Plugin.Ipc;
using CollarSystem.Plugin.Safety;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Glamourer.Api.Enums;

namespace CollarSystem.Plugin.Commands;

/// collar/restraints: applies a restraint device's Glamourer design (locking exactly the slots that
/// design changes, via SlotLockManager - same "Restraints" owner name collar/slot-locking's spec already
/// reserves) and activates every restriction rule the device carries (via RestrictionRuleManager). Follows
/// OutfitCommand's exact two-tier shape: Sub self-apply/release via alias, Owner force-apply/force-unlock
/// "joker" override that locks out the Sub's own controls while active.
public sealed class RestraintCommand
{
    private const string Owner = "Restraints";

    private readonly PluginConfig config;
    private readonly GlamourerIpc glamourer;
    private readonly SlotLockManager slotLocks;
    private readonly RestrictionRuleManager restrictionRules;
    private readonly SubRuntimeState runtimeState;

    public RestraintCommand(PluginConfig config, GlamourerIpc glamourer, SlotLockManager slotLocks, RestrictionRuleManager restrictionRules, SubRuntimeState runtimeState)
    {
        this.config = config;
        this.glamourer = glamourer;
        this.slotLocks = slotLocks;
        this.restrictionRules = restrictionRules;
        this.runtimeState = runtimeState;
    }

    /// Every currently-active device (Sub-applied or Owner-forced), for UI display.
    public IReadOnlySet<string> ActiveDeviceIds => activeDeviceIds;
    private readonly HashSet<string> activeDeviceIds = new();

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

    /// The Owner's direct override: matches `deviceName` against the Sub's own tagged device catalog
    /// (case-insensitive) - same lookup shape as OutfitCommand.ForceApply. Always force-locks.
    public bool ForceApply(string deviceName)
    {
        var entry = config.RestraintMapping.Devices.Values
            .FirstOrDefault(d => string.Equals(d.Name, deviceName, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
            return false;

        if (!ApplyDevice(entry.Id, entry))
            return false;

        runtimeState.RestraintsForceLocked = true;
        return true;
    }

    /// The Owner's rule-carrying override (collar/restraints "Owner force-apply and force-release
    /// override"): matches `deviceName` against every scanned design (tagged or not - see Rescan), and
    /// activates exactly the rules the Owner assigned to their quick command, ignoring whatever rules the
    /// Sub may have separately tagged that same design with. The ephemeral device's id is the design's own
    /// GUID, so re-applying the same design consistently maps to the same active-device/rule-conflict
    /// tracking entry across calls.
    public bool ForceApply(string deviceName, List<RestraintRuleAssignment> rules)
    {
        var scanned = config.RestraintMapping.ScannedDesigns.Values
            .FirstOrDefault(d => string.Equals(d.Name, deviceName, StringComparison.OrdinalIgnoreCase));
        if (scanned is null)
            return false;

        var device = new RestraintDeviceDefinition
        {
            Id = scanned.DesignId.ToString("N"),
            DesignId = scanned.DesignId,
            Name = scanned.Name,
            Rules = rules,
        };

        if (!ApplyDevice(device.Id, device))
            return false;

        runtimeState.RestraintsForceLocked = true;
        return true;
    }

    /// The only thing that can release every Owner-forced device besides panic.
    public bool ForceUnlock()
    {
        if (!runtimeState.RestraintsForceLocked)
            return false;

        foreach (var deviceId in activeDeviceIds.ToList())
            ReleaseDevice(deviceId);

        runtimeState.RestraintsForceLocked = false;
        return true;
    }

    /// Applies a device's Glamourer design (locking the slots it changes, via SlotLockManager - refused if
    /// any of those slots is already locked by a different owner) and activates every rule it carries (via
    /// RestrictionRuleManager - refused if a rule conflicts with an already-active one). Both checks run
    /// before anything is applied, and both must pass, so a refused apply never leaves a partial visual or
    /// rule change behind - same "refuse the whole action" guarantee OutfitCommand.ApplyDesign gives.
    private bool ApplyDevice(string deviceId, RestraintDeviceDefinition device)
    {
        var slots = glamourer.GetDesignEquipSlots(device.DesignId);
        if (slotLocks.WouldOverlap(slots, Owner))
        {
            Plugin.Log.Warning($"Restraint apply refused for \"{device.Name}\": a locked slot is already held by a different owner.");
            return false;
        }
        if (restrictionRules.WouldConflict(device.Rules, deviceId))
        {
            Plugin.Log.Warning($"Restraint apply refused for \"{device.Name}\": a restriction rule conflicts with a different device already active.");
            return false;
        }

        var ec = glamourer.ApplyDesign(device.DesignId);
        if (ec != GlamourerApiEc.Success)
        {
            Plugin.Log.Warning($"Restraint apply failed for \"{device.Name}\": {ec}.");
            return false;
        }

        if (slots.Count > 0)
        {
            var toLock = new Dictionary<ApiEquipSlot, SlotLockValue>();
            foreach (var slot in slots)
            {
                if (glamourer.GetEquipSlotValue(slot) is { } value)
                    toLock[slot] = new SlotLockValue(value.ItemId, value.Stain, value.Stain2);
            }

            slotLocks.TryRegisterAlreadyApplied(Owner, toLock);
        }

        if (device.Rules.Count > 0)
            restrictionRules.TryActivate(deviceId, device.Rules);

        var pose = device.Rules.FirstOrDefault(r => r.Kind == RestraintRuleKind.ForcedPose);
        if (pose is not null)
            ApplyPose(pose.PoseModeId);

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

    private void ReleaseDevice(string deviceId)
    {
        restrictionRules.Release(deviceId);
        activeDeviceIds.Remove(deviceId);

        // Only release the shared "Restraints" slot lock once no other active device still needs it -
        // SlotLockManager.Release tears down every slot the owner holds, so this must wait until the last
        // active device releases, mirroring RestrictionRuleManager's own refcounting.
        if (activeDeviceIds.Count == 0)
            slotLocks.Release(Owner);
    }

    /// How many designs the last Restraints scan found in total, before the allowlist filter - mirrors
    /// OutfitCommand.LastScanTotalDesigns.
    public int? LastScanTotalDesigns { get; private set; }

    /// Sub-side: rescan for Restraints, independent of collar/outfit's wardrobe scan - bondage/restriction
    /// designs and everyday outfits live in different Glamourer folders in practice, so this uses its own
    /// folder allowlist (PluginConfig.RestraintFolderAllowlist) rather than WardrobeFolderAllowlist, same
    /// "empty means all" semantics as OutfitCommand.Rescan.
    public void Rescan()
    {
        var allDesigns = glamourer.GetDesigns();
        LastScanTotalDesigns = allDesigns.Count;

        var allowlist = config.RestraintFolderAllowlist;
        var matched = allowlist.Count == 0
            ? allDesigns
            : allDesigns.Where(d => allowlist.Any(folder => IsUnderFolder(d.FullPath, folder))).ToList();

        var entries = matched.Select(d => new WardrobeDesignEntry { DesignId = d.Id, Name = d.DisplayName });
        config.RestraintMapping.ScannedDesigns = entries.ToDictionary(e => e.DesignId);
        config.Save();
    }

    private static bool IsUnderFolder(string fullPath, string folder) =>
        fullPath.StartsWith(folder.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);

    /// The Restraints tab tags devices from this independent scan, not collar/outfit's WardrobeMapping.
    public IReadOnlyList<WardrobeDesignEntry> ScannedDesigns() => config.RestraintMapping.ScannedDesigns.Values.ToList();

    /// Tags a scanned design as a device, or updates an already-tagged one's rules if `id` matches an
    /// existing entry.
    public void TagDevice(string? id, Guid designId, string name, List<RestraintRuleAssignment> rules)
    {
        var device = id is not null && config.RestraintMapping.Devices.TryGetValue(id, out var existing)
            ? existing
            : new RestraintDeviceDefinition();

        device.DesignId = designId;
        device.Name = name;
        device.Rules = rules;
        config.RestraintMapping.Devices[device.Id] = device;
        config.Save();
    }

    public void UntagDevice(string id)
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
            _ => "",
        }).Where(t => t.Length > 0);

        return $"restraint lock \"{deviceName}\" {RulesToken}{string.Join(',', tokens)}";
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
        }
        return rules;
    }

    /// collar/catalog-sync: every scanned design's display name, tagged or not, deduplicated with any
    /// tagged device names - same plain-name export shape OutfitCommand/MoodlesCommand provide, since a
    /// restraint device is identified by name alone for Owner purposes (ChatCommandListener's
    /// `restraint lock <name>` grammar). Exporting every scanned name (not only tagged ones) is what lets
    /// an Owner import and configure a device the Sub never got around to tagging.
    public IReadOnlyList<string> ExportNames() =>
        config.RestraintMapping.ScannedDesigns.Values.Select(d => d.Name)
            .Concat(config.RestraintMapping.Devices.Values.Select(d => d.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
}
