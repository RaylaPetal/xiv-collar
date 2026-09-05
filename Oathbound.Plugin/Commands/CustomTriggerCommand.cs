using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Oathbound.Plugin.Config;
using Oathbound.Plugin.UI;
using ECommons.Automation;

namespace Oathbound.Plugin.Commands;

/// collar/custom-triggers: applies a Custom Trigger's bundled actions in sequence, dispatching each to its
/// own category's existing apply method and checking that category's own permission (and, for Restraints/
/// Gesture, the existing ToS acknowledgement; for Chat, the new dedicated permission/acknowledgement pair)
/// immediately before calling it - never bypassing a check that action would already require if triggered
/// on its own (design.md's "orchestrator, not a reimplementation" decision). An action whose permission
/// isn't met is skipped, not treated as an error - the rest of the bundle's permitted actions still apply.
public sealed class CustomTriggerCommand
{
    private readonly PluginConfig config;
    private readonly TitleCommand title;
    private readonly OutfitCommand outfit;
    private readonly GestureCommand gesture;
    private readonly MoodlesCommand moodles;
    private readonly RestraintCommand restraints;

    public CustomTriggerCommand(PluginConfig config, TitleCommand title, OutfitCommand outfit, GestureCommand gesture, MoodlesCommand moodles, RestraintCommand restraints)
    {
        this.config = config;
        this.title = title;
        this.outfit = outfit;
        this.gesture = gesture;
        this.moodles = moodles;
        this.restraints = restraints;
    }

    public LocalTestResult Apply(List<CustomTriggerAction> actions)
    {
        var applied = new List<string>();
        var skipped = new List<string>();

        foreach (var action in actions)
        {
            switch (action.Kind)
            {
                case CustomTriggerActionKind.Title:
                    if (!config.Permissions.Title) { skipped.Add("title (permission)"); break; }
                    title.Apply(new TitleAliasDefinition { Text = action.TitleText, IsPrefix = action.TitleIsPrefix, Color = action.TitleColor });
                    applied.Add("title");
                    break;

                case CustomTriggerActionKind.Outfit:
                    if (!config.Permissions.Outfit) { skipped.Add("outfit (permission)"); break; }
                    // A Sub-defined trigger (via ResolveAlias) always carries a real DesignId captured
                    // from this same client's own WardrobeMapping, so it goes through Apply exactly like a
                    // plain outfit alias. An Owner-authored ad-hoc `customtrigger cast` bundle only ever
                    // has the design name (the Owner has no access to the Sub's WardrobeMapping ids), so it
                    // falls back to the same name-based ForceApply an Owner's plain `outfit lock <name>`
                    // override already uses - this is the fix for that gap, not the original Group 4 design.
                    var (outfitOk, _) = action.OutfitDesignId != Guid.Empty
                        ? outfit.Apply(new OutfitAliasDefinition { DesignId = action.OutfitDesignId, DesignName = action.OutfitDesignName, Locked = true })
                        : outfit.ForceApply(action.OutfitDesignName);
                    if (outfitOk)
                        applied.Add("outfit");
                    else
                        skipped.Add("outfit (force-locked, not found, or apply failed)");
                    break;

                case CustomTriggerActionKind.Gesture:
                    if (!(config.Permissions.Gesture && config.TosAcknowledged)) { skipped.Add("gesture (permission/acknowledgement)"); break; }
                    // Same real-id-vs-name-only split as Outfit/Restraint above: `GestureAliasDefinition`'s
                    // own name-fallback additionally requires a ModDirectory match this action doesn't
                    // carry, so a no-id Owner ad-hoc action goes through the plain name/label match
                    // `ForceApply` already uses for a `gesture <name>` override instead.
                    var gestureOk = action.GestureId.Length > 0
                        ? gesture.Apply(new GestureAliasDefinition { GestureId = action.GestureId, AnimationName = action.GestureAnimationName })
                        : gesture.ForceApply(action.GestureAnimationName);
                    if (gestureOk)
                        applied.Add("gesture");
                    else
                        skipped.Add("gesture (not found or failed to play)");
                    break;

                case CustomTriggerActionKind.Moodle:
                    if (!config.Permissions.Moodles) { skipped.Add("moodle (permission)"); break; }
                    if (moodles.Apply(new MoodlesAliasDefinition { StatusId = action.MoodleStatusId, StatusName = action.MoodleStatusName }))
                        applied.Add("moodle");
                    else
                        skipped.Add("moodle (not found or failed to apply)");
                    break;

                case CustomTriggerActionKind.Restraint:
                    if (!(config.Permissions.Restraints && config.TosAcknowledged)) { skipped.Add("restraint (permission/acknowledgement)"); break; }
                    // Both branches are apply-only because the bundle itself arrived as an Owner command.
                    // In particular, do not route stable IDs through the Sub self-service Toggle method:
                    // Toggle is rejected by an Owner force-lock and made multi-restraint bundles depend on
                    // unrelated prior runtime state.
                    var restraintOk = config.RestraintMapping.Devices.ContainsKey(action.RestraintDeviceId)
                        ? restraints.ForceApplyById(action.RestraintDeviceId)
                        : restraints.ForceApply(action.RestraintDeviceName);
                    if (restraintOk)
                        applied.Add($"restraint \"{action.RestraintDeviceName}\"");
                    else
                        skipped.Add($"restraint \"{action.RestraintDeviceName}\" ({restraints.LastFailureReason ?? "apply failed"})");
                    break;

                case CustomTriggerActionKind.Chat:
                    if (!(config.Permissions.CustomChatMessages && config.CustomChatAcknowledged)) { skipped.Add("chat (permission/acknowledgement)"); break; }
                    if (action.ChatText.Trim().Length > 0)
                    {
                        Chat.SendMessage(action.ChatText);
                        applied.Add("chat");
                    }
                    else
                    {
                        skipped.Add("chat (no text configured)");
                    }
                    break;
            }
        }

        var skippedSuffix = skipped.Count > 0 ? $" (skipped: {string.Join(", ", skipped)})" : "";
        return applied.Count > 0
            ? LocalTestResult.Ok($"Applied: {string.Join(", ", applied)}{skippedSuffix}")
            : LocalTestResult.Fail($"Nothing applied{skippedSuffix}");
    }

    /// design.md "customtrigger cast wire shape": mirrors `RestraintCommand.BuildWearCommand`'s quoted-label
    /// + token-list structure. Each non-chat action is one `kind=value` segment joined by ';'; any free-text
    /// field (title text, and every category's own name) is base64-encoded so it can't collide with the '|'/
    /// ';'/'=' delimiters - ids (guids, gesture/moodle/restraint ids) are left raw since they're plugin-
    /// generated and never contain those characters. The chat action, if present, is always last and its raw
    /// text consumes the remainder of the line (deliberately not delimited - see design.md's "pragmatic, not
    /// fully general" note). At most one action per kind is supported in this ad-hoc wire encoding (the
    /// Sub-alias path has no such limit, since it stores the action list directly rather than encoding it).
    public static string BuildCastCommand(string label, List<CustomTriggerAction> actions)
    {
        var segments = new List<string>();
        string? chatSegment = null;

        foreach (var action in actions)
        {
            switch (action.Kind)
            {
                case CustomTriggerActionKind.Title:
                    segments.Add($"title={EncodeText(action.TitleText)}|{(action.TitleIsPrefix ? 1 : 0)}|{FormatColor(action.TitleColor)}");
                    break;
                case CustomTriggerActionKind.Outfit:
                    segments.Add($"outfit={action.OutfitDesignId}|{EncodeText(action.OutfitDesignName)}");
                    break;
                case CustomTriggerActionKind.Gesture:
                    segments.Add($"gesture={action.GestureId}|{EncodeText(action.GestureAnimationName)}");
                    break;
                case CustomTriggerActionKind.Moodle:
                    segments.Add($"moodle={action.MoodleStatusId}|{EncodeText(action.MoodleStatusName)}");
                    break;
                case CustomTriggerActionKind.Restraint:
                    segments.Add($"restraint={action.RestraintDeviceId}|{EncodeText(action.RestraintDeviceName)}");
                    break;
                case CustomTriggerActionKind.Chat:
                    chatSegment = $"chat={action.ChatText}";
                    break;
            }
        }

        if (chatSegment is not null)
            segments.Add(chatSegment);

        return $"customtrigger cast \"{label}\" {string.Join(';', segments)}";
    }

    /// Parses the remainder of a `customtrigger cast ...` command (after the "cast " prefix) into a label
    /// and action list. Fails closed (returns false) on any malformed segment, unknown kind, or a bundle
    /// that ends up with zero actions - an empty ad-hoc trigger is meaningless, same rationale as
    /// `RestraintCommand.TryParseWearCommand` requiring at least one rule.
    public static bool TryParseCastCommand(string remainder, out string label, out List<CustomTriggerAction> actions)
    {
        label = "";
        actions = new List<CustomTriggerAction>();

        var trimmed = remainder.Trim();
        if (!trimmed.StartsWith('"'))
            return false;

        var closing = trimmed.IndexOf('"', 1);
        if (closing < 0)
            return false;

        label = trimmed[1..closing];
        if (label.Length == 0)
            return false;

        var tail = trimmed[(closing + 1)..].Trim();
        if (tail.Length == 0)
            return false;

        string beforeChat;
        string? chatText = null;
        if (tail.StartsWith("chat=", StringComparison.OrdinalIgnoreCase))
        {
            beforeChat = "";
            chatText = tail["chat=".Length..];
        }
        else
        {
            var chatMarker = tail.IndexOf(";chat=", StringComparison.OrdinalIgnoreCase);
            if (chatMarker >= 0)
            {
                beforeChat = tail[..chatMarker];
                chatText = tail[(chatMarker + ";chat=".Length)..];
            }
            else
            {
                beforeChat = tail;
            }
        }

        if (beforeChat.Length > 0)
        {
            foreach (var segment in beforeChat.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = segment.IndexOf('=');
                if (eq < 0)
                    return false;

                var kind = segment[..eq];
                var value = segment[(eq + 1)..];
                var parts = value.Split('|');

                switch (kind.ToLowerInvariant())
                {
                    case "title":
                        if (parts.Length != 3 || !TryDecodeText(parts[0], out var titleText) || titleText.Length == 0)
                            return false;
                        if (!int.TryParse(parts[1], out var prefixFlag))
                            return false;
                        if (!TryParseColor(parts[2], out var color))
                            return false;
                        actions.Add(new CustomTriggerAction { Kind = CustomTriggerActionKind.Title, TitleText = titleText, TitleIsPrefix = prefixFlag != 0, TitleColor = color });
                        break;

                    case "outfit":
                        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var designId) || !TryDecodeText(parts[1], out var designName) || designName.Length == 0)
                            return false;
                        actions.Add(new CustomTriggerAction { Kind = CustomTriggerActionKind.Outfit, OutfitDesignId = designId, OutfitDesignName = designName });
                        break;

                    case "gesture":
                        if (parts.Length != 2 || parts[0].Length == 0 || !TryDecodeText(parts[1], out var animName) || animName.Length == 0)
                            return false;
                        actions.Add(new CustomTriggerAction { Kind = CustomTriggerActionKind.Gesture, GestureId = parts[0], GestureAnimationName = animName });
                        break;

                    case "moodle":
                        if (parts.Length != 2 || parts[0].Length == 0 || !TryDecodeText(parts[1], out var statusName) || statusName.Length == 0)
                            return false;
                        actions.Add(new CustomTriggerAction { Kind = CustomTriggerActionKind.Moodle, MoodleStatusId = parts[0], MoodleStatusName = statusName });
                        break;

                    case "restraint":
                        if (parts.Length != 2 || parts[0].Length == 0 || !TryDecodeText(parts[1], out var deviceName) || deviceName.Length == 0)
                            return false;
                        actions.Add(new CustomTriggerAction { Kind = CustomTriggerActionKind.Restraint, RestraintDeviceId = parts[0], RestraintDeviceName = deviceName });
                        break;

                    default:
                        return false;
                }
            }
        }

        if (chatText is { Length: > 0 })
            actions.Add(new CustomTriggerAction { Kind = CustomTriggerActionKind.Chat, ChatText = chatText });

        return actions.Count > 0;
    }

    /// One-line human-readable summary of a single bundled action - shared by the Sub-side UI's own draft
    /// list (`CollarWindow.SummarizeCustomTriggerAction`) and `CatalogSyncService`'s Aliases export
    /// description, so both places describe a Custom Trigger's contents identically rather than each
    /// re-deriving their own text.
    public static string Summarize(CustomTriggerAction a) => CommandPresentation.Action(a);

    private static string EncodeText(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    private static bool TryDecodeText(string encoded, out string text)
    {
        text = "";
        try
        {
            text = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string FormatColor(Vector3 color) =>
        $"{color.X.ToString(CultureInfo.InvariantCulture)},{color.Y.ToString(CultureInfo.InvariantCulture)},{color.Z.ToString(CultureInfo.InvariantCulture)}";

    private static bool TryParseColor(string token, out Vector3 color)
    {
        color = new Vector3(1, 1, 1);
        var parts = token.Split(',');
        if (parts.Length != 3
            || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var r)
            || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var g)
            || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
            return false;

        color = new Vector3(r, g, b);
        return true;
    }
}
