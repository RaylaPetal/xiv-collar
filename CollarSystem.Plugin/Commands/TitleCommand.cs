using System;
using System.Globalization;
using System.Numerics;
using CollarSystem.Plugin.Config;
using CollarSystem.Plugin.Ipc;
using CollarSystem.Plugin.Safety;

namespace CollarSystem.Plugin.Commands;

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
    }

    /// collar/title "Owner sets Sub's title": the styled counterpart to `ForceApply(string)`, driven by the
    /// `title style "<text>" prefix:<0|1> color:<r>,<g>,<b>` wire command - a new, distinct verb rather than
    /// a suffix on `create` (design.md: title text has no catalog to fail closed against, so an old client
    /// can't safely ignore trailing syntax it doesn't understand).
    public void ForceApply(string text, bool isPrefix, Vector3 color)
    {
        honorific.SetTitle(new HonorificTitleData { Title = text, IsPrefix = isPrefix, Color = color });
        runtimeState.TitleApplied = true;
        runtimeState.TitleForceLocked = true;
    }

    /// The only thing that can release a force-applied title besides panic.
    public void ForceClear()
    {
        honorific.ClearTitle();
        runtimeState.TitleApplied = false;
        runtimeState.TitleForceLocked = false;
    }

    /// Builds the chat text for an Owner's styled title quick command (collar/title "Owner sets Sub's
    /// title"): a new, distinct `style` verb (design.md) rather than a suffix on `create`, since title text
    /// is arbitrary free text with no catalog to fail closed against on an old client.
    public static string BuildStyleCommand(string text, bool isPrefix, Vector3 color) =>
        $"title style \"{text}\" prefix:{(isPrefix ? 1 : 0)} color:{color.X.ToString(CultureInfo.InvariantCulture)},{color.Y.ToString(CultureInfo.InvariantCulture)},{color.Z.ToString(CultureInfo.InvariantCulture)}";

    /// Parses the remainder of a `title style ...` command (after the "style " prefix) into text,
    /// prefix/suffix, and color. Fails closed (returns false) on any malformed or missing segment - a
    /// styled title with no color/prefix carried is meaningless (nothing distinguishes it from `create`),
    /// so this never silently applies a plain title under the styled verb.
    public static bool TryParseStyleCommand(string remainder, out string text, out bool isPrefix, out Vector3 color)
    {
        text = "";
        isPrefix = false;
        color = new Vector3(1, 1, 1);

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
                var parts = token["color:".Length..].Split(',');
                if (parts.Length != 3
                    || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var r)
                    || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var g)
                    || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
                    return false;
                color = new Vector3(r, g, b);
                foundColor = true;
            }
        }

        return foundPrefix && foundColor;
    }
}
