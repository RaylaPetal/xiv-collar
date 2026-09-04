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

    /// collar/pairing's one-time handshake message: the keyword, this side's own declared Role, this
    /// side's own code, and (collar/chat-transport) this side's own currently-configured trigger phrase -
    /// the role lets the receiving side's Pending prompt show what the sender thinks this pairing will be,
    /// and the trigger phrase lets the receiving side compose future commands using the phrase this side
    /// actually expects, instead of needing to manually match it. No `/tell` target - there's no captured
    /// peer identity yet to address it to, so the Owner/Sub types the recipient themselves the same way
    /// they'd start any other tell.
    public string ComposePairing()
    {
        var roleToken = config.Role == PluginRole.Owner ? "owner" : "sub";
        return $"collarpair {roleToken} {config.Pairing.MyCode} {config.TriggerPhrase.Trim()}";
    }

    /// collar/pairing's "One-way pairing handshake completes both sides": the automatic confirmation tell
    /// sent back to the inviter as part of accepting a pending request. Unlike ComposePairing, the target
    /// is already known (the verified sender of the request being accepted), so this composes a full
    /// `/tell` directly rather than going through Wrap's already-paired-peer addressing. `code` echoes back
    /// the code that was matched to accept, which the inviter checks against their own MyCode.
    public string ComposePairingAck(string name, string world, string code, string triggerPhrase)
    {
        var roleToken = config.Role == PluginRole.Owner ? "owner" : "sub";
        return $"/tell {name}@{world} collarpairack {roleToken} {code} {triggerPhrase.Trim()}";
    }

    /// collar/pairing "Panic notifies the peer, best-effort": the automatic notification tell sent from
    /// PanicHandler as a direct consequence of the panic action itself. Like ComposePairingAck, the target
    /// is already known (the peer identity cached at the moment panic ran), so this composes a full
    /// `/tell` directly rather than going through Wrap. Carries no code - ending a trust relationship
    /// doesn't need one, only establishing a new one does (see design.md).
    public string ComposeUnpairNotice(string name, string world)
    {
        var roleToken = config.Role == PluginRole.Owner ? "owner" : "sub";
        return $"/tell {name}@{world} collarunpair {roleToken}";
    }

    /// collar/chat-transport: uses the peer's trigger phrase captured during pairing (see
    /// PairingState.PeerTriggerPhrase) when known, so an already-paired relationship can never silently
    /// diverge again - falls back to this side's own configured TriggerPhrase only when no peer phrase has
    /// been captured (no pairing yet, or a peer whose handshake didn't declare one).
    ///
    /// collar/chat-transport "Composing and sending require active pairing, not just a remembered peer":
    /// addresses a `/tell` only while `IsPaired` is true, not merely whenever PeerName/PeerWorld happen to
    /// be non-empty - PanicHandler.EndPairingLocally deliberately leaves those cached after panic clears
    /// Paired, so checking presence alone would let a side that just panicked its own pairing away keep
    /// composing (and, via CollarWindow's canSend, keep sending) to the peer it just unpaired from.
    private string Wrap(string body)
    {
        var pairing = config.Pairing;
        var trigger = (!string.IsNullOrWhiteSpace(pairing.PeerTriggerPhrase) ? pairing.PeerTriggerPhrase : config.TriggerPhrase).Trim();
        var full = $"{trigger} {body}".Trim();

        if (!pairing.IsPaired)
            return full;

        return $"/tell {pairing.PeerName}@{pairing.PeerWorld} {full}";
    }
}
