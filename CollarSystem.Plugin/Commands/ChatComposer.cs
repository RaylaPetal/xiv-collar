using CollarSystem.Plugin.Config;

namespace CollarSystem.Plugin.Commands;

/// This class only ever builds text and returns it - it has no dependency on any chat-send API at all, by
/// construction, so there is no code path here that could ever transmit anything. Sending (either the
/// Owner's own paste into the game's chat box, or the explicit one-click Send button - see ChatSender) is
/// always a separate, deliberate step the UI takes with the string this class hands back, never something
/// this class does on its own.
public sealed class ChatComposer
{
    private readonly PluginConfig config;

    public ChatComposer(PluginConfig config)
    {
        this.config = config;
    }

    /// Builds `/tell <PeerName>@<PeerWorld> <trigger> <command>` - or just the trigger+command, with no
    /// `/tell` target, if a handshake hasn't captured a peer identity yet. `command` is raw text: either a
    /// plain alias, or one of ChatCommandListener's reserved-keyword override commands - this class has no
    /// idea which, since both are just text appended after the trigger phrase.
    public string Compose(string command) => Wrap(command);

    /// collar/pairing's one-time handshake message: the keyword, this side's own declared Role, and this
    /// side's own code - the role lets the receiving side's Pending prompt show what the sender thinks
    /// this pairing will be. No `/tell` target - there's no captured peer identity yet to address it to,
    /// so the Owner/Sub types the recipient themselves the same way they'd start any other tell.
    public string ComposePairing()
    {
        var roleToken = config.Role == PluginRole.Owner ? "owner" : "sub";
        return $"collarpair {roleToken} {config.Pairing.MyCode}";
    }

    private string Wrap(string body)
    {
        var trigger = config.TriggerPhrase.Trim();
        var full = $"{trigger} {body}".Trim();

        var pairing = config.Pairing;
        if (string.IsNullOrWhiteSpace(pairing.PeerName) || string.IsNullOrWhiteSpace(pairing.PeerWorld))
            return full;

        return $"/tell {pairing.PeerName}@{pairing.PeerWorld} {full}";
    }
}
