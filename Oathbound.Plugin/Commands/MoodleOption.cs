using System;

namespace Oathbound.Plugin.Commands;

/// collar/attached-moodles "Owner can override the attached moodle per command": the optional trailing
/// `moodle:"<status name>"` option on `outfit lock`, `restraint lock/catalog/wear` and `leash`. It is always
/// the last thing on the line, so the receiver strips it before handing the rest to the existing, unchanged
/// parsers (`rules:` etc). An older Sub client that doesn't know the option sees it as part of the device/
/// design name, matches nothing, and fails closed - the same degradation `rules:` already accepted.
public static class MoodleOption
{
    private const string Token = "moodle:";

    /// Returns `text` without a trailing `moodle:"..."` option, and the option's status name (null when
    /// there is none or it is malformed - a malformed option is left in place for the normal parser to
    /// reject).
    public static string Strip(string text, out string? moodleName)
    {
        moodleName = null;
        var trimmed = text.TrimEnd();
        if (!trimmed.EndsWith('"'))
            return text;

        var start = trimmed.LastIndexOf(Token + "\"", StringComparison.OrdinalIgnoreCase);
        if (start < 0 || (start > 0 && !char.IsWhiteSpace(trimmed[start - 1])))
            return text;

        var name = trimmed[(start + Token.Length + 1)..^1].Trim();
        if (name.Length == 0 || name.Contains('"'))
            return text;

        moodleName = name;
        return trimmed[..start].TrimEnd();
    }

    /// Appends the option to an outgoing command - nothing when no override was picked, so a default send is
    /// byte-for-byte what older Sub clients already understand.
    public static string Append(string command, string? moodleName) =>
        string.IsNullOrWhiteSpace(moodleName) ? command : $"{command} {Token}\"{moodleName.Trim()}\"";
}
