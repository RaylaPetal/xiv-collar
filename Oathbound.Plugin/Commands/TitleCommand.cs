using System;
using System.Globalization;
using System.Numerics;
using Oathbound.Plugin.Config;
using Oathbound.Plugin.Ipc;
using Oathbound.Plugin.Safety;

namespace Oathbound.Plugin.Commands;

/// collar/title: alias-triggered title changes applied via Honorific on the Sub's own client, plus the
/// Owner's "joker" override (ForceApply/ForceClear - see ChatCommandListener's reserved-keyword grammar).
/// A force-applied title locks out the Sub's own alias-triggered Apply/Clear until the matching
/// ForceClear (or panic) releases it - the Sub set up their aliases, but a forced title always wins over
/// them while it's in effect.
public sealed class TitleCommand
{
    private readonly HonorificIpc honorific;
    private readonly SubRuntimeState runtimeState;

    public TitleCommand(HonorificIpc honorific, SubRuntimeState runtimeState)
    {
        this.honorific = honorific;
        this.runtimeState = runtimeState;
    }

    public void Apply(TitleAliasDefinition alias)
    {
        if (runtimeState.TitleForceLocked)
            return;

        honorific.SetTitle(new HonorificTitleData
        {
            Title = alias.Text,
            IsPrefix = alias.IsPrefix,
            Color = alias.Color,
            Glow = alias.Glow,
        });
        runtimeState.TitleApplied = true;
    }

    public void Clear()
    {
        if (runtimeState.TitleForceLocked)
            return;

        honorific.ClearTitle();
        runtimeState.TitleApplied = false;
    }

    /// The Owner's direct override: applies immediately and locks out the Sub's own aliases regardless of
    /// what they're set to. Plain white suffix, same as Honorific's own default - the `title create <text>`
    /// wire command's counterpart. See the styled overload for prefix/color (`title style ...`).
    public void ForceApply(string text)
    {
        honorific.SetTitle(new HonorificTitleData { Title = text, IsPrefix = false, Color = new(1, 1, 1) });
        runtimeState.TitleApplied = true;
        runtimeState.TitleForceLocked = true;
        runtimeState.TitleForceText = text;
        runtimeState.TitleForceIsPrefix = false;
        runtimeState.TitleForceColor = new(1, 1, 1);
        runtimeState.TitleForceGlow = null;
    }

    /// collar/title "Owner sets Sub's title": the styled counterpart to `ForceApply(string)`, driven by the
    /// `title style "<text>" prefix:<0|1> color:<r>,<g>,<b> [glow:<r>,<g>,<b>]` wire command - a new,
    /// distinct verb rather than a suffix on `create` (design.md: title text has no catalog to fail closed
    /// against, so an old client can't safely ignore trailing syntax it doesn't understand). `glow` is
    /// optional and defaults to null (no glow), matching Honorific's own semantics.
    public void ForceApply(string text, bool isPrefix, Vector3 color, Vector3? glow = null)
    {
        honorific.SetTitle(new HonorificTitleData { Title = text, IsPrefix = isPrefix, Color = color, Glow = glow });
        runtimeState.TitleApplied = true;
        runtimeState.TitleForceLocked = true;
        runtimeState.TitleForceText = text;
        runtimeState.TitleForceIsPrefix = isPrefix;
        runtimeState.TitleForceColor = color;
        runtimeState.TitleForceGlow = glow;
    }

    /// The only thing that can release a force-applied title besides panic.
    public void ForceClear()
    {
        honorific.ClearTitle();
        runtimeState.TitleApplied = false;
        runtimeState.TitleForceLocked = false;
        runtimeState.TitleForceText = null;
    }

    /// collar/title "Force-applied title reasserts if removed or changed": Honorific has no "prevent
    /// removal" or change-notification mechanism (same limitation CollarCommand.OnFrameworkUpdate already
    /// works around for the collar's assigned Moodle), so this blindly re-sends the last force-applied style
    /// on an interval for as long as it's still locked - calling Honorific directly rather than through
    /// Apply, which refuses while TitleForceLocked is true.
    public void OnFrameworkUpdate()
    {
        if (!runtimeState.TitleForceLocked || runtimeState.TitleForceText is null)
            return;

        var now = Environment.TickCount64;
        if (now < nextReassertTicks)
            return;

        honorific.SetTitle(new HonorificTitleData
        {
            Title = runtimeState.TitleForceText,
            IsPrefix = runtimeState.TitleForceIsPrefix,
            Color = runtimeState.TitleForceColor,
            Glow = runtimeState.TitleForceGlow,
        });
        nextReassertTicks = now + ReassertIntervalMs;
    }

    /// Matches CollarCommand.MoodleReassertIntervalMs exactly - no reason for Title's reassertion cadence to
    /// differ from the Moodle's.
    private const long ReassertIntervalMs = 10_000;
    private long nextReassertTicks;

    /// Builds the chat text for an Owner's styled title quick command (collar/title "Owner sets Sub's
    /// title"): a new, distinct `style` verb (design.md) rather than a suffix on `create`, since title text
    /// is arbitrary free text with no catalog to fail closed against on an old client. `glow` is optional -
    /// omitted entirely when null, so an old Sub's parser (which only reacts to tokens it recognizes) is
    /// unaffected either way.
    public static string BuildStyleCommand(string text, bool isPrefix, Vector3 color, Vector3? glow = null)
    {
        var command = $"title style \"{text}\" prefix:{(isPrefix ? 1 : 0)} color:{FormatVector(color)}";
        if (glow is { } g)
            command += $" glow:{FormatVector(g)}";
        return command;
    }

    private static string FormatVector(Vector3 v) =>
        $"{v.X.ToString(CultureInfo.InvariantCulture)},{v.Y.ToString(CultureInfo.InvariantCulture)},{v.Z.ToString(CultureInfo.InvariantCulture)}";

    /// Parses the remainder of a `title style ...` command (after the "style " prefix) into text,
    /// prefix/suffix, color, and an optional glow. Fails closed (returns false) if text/prefix/color are
    /// missing or malformed - a styled title with no color/prefix carried is meaningless (nothing
    /// distinguishes it from `create`), so this never silently applies a plain title under the styled verb.
    /// `glow` defaults to null (no glow) when its token is absent, so a command built before glow support
    /// existed still parses exactly as before.
    public static bool TryParseStyleCommand(string remainder, out string text, out bool isPrefix, out Vector3 color, out Vector3? glow)
    {
        text = "";
        isPrefix = false;
        color = new Vector3(1, 1, 1);
        glow = null;

        var trimmed = remainder.Trim();
        if (!trimmed.StartsWith('"'))
            return false;

        var closing = trimmed.IndexOf('"', 1);
        if (closing < 0)
            return false;

        text = trimmed[1..closing];
        if (text.Length == 0)
            return false;

        var foundPrefix = false;
        var foundColor = false;
        foreach (var token in trimmed[(closing + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.StartsWith("prefix:", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(token["prefix:".Length..], out var p))
                    return false;
                isPrefix = p != 0;
                foundPrefix = true;
            }
            else if (token.StartsWith("color:", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseVector(token["color:".Length..], out var c))
                    return false;
                color = c;
                foundColor = true;
            }
            else if (token.StartsWith("glow:", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseVector(token["glow:".Length..], out var g))
                    return false;
                glow = g;
            }
        }

        return foundPrefix && foundColor;
    }

    private static bool TryParseVector(string encoded, out Vector3 value)
    {
        value = default;
        var parts = encoded.Split(',');
        if (parts.Length != 3
            || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
            return false;
        value = new Vector3(x, y, z);
        return true;
    }
}
