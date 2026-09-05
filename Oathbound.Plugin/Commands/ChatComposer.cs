using Oathbound.Plugin.Config;

namespace Oathbound.Plugin.Commands;

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

    /// collar/pairing's relay-assisted handshake: a short lifecycle tell carrying only the invitation's
    /// capability id - everything else (role, trigger phrase, expiry) lives in the signed invitation itself,
    /// fetched from the relay once this tell's verified sender is captured (see Relay/PairingService.cs).
    /// `targetTellAddress` is typed by the user (e.g. "Name Surname@World") since there's no captured peer
    /// identity yet to address it to automatically.
    public string ComposeRelayInvitation(string targetTellAddress, string invitationId) =>
        $"/tell {targetTellAddress.Trim()} collarinvite {invitationId}";

    /// collar/pairing's acknowledgement tell, sent automatically as part of accepting a pending relay
    /// invitation. The target is already known (the verified sender of the invitation being accepted), so
    /// this composes a full `/tell` directly rather than going through Wrap's already-paired-peer
    /// addressing. `proofDigest` is what the inviter cross-checks against the signed acceptance it fetches
    /// from the relay before activating - a relay claim without this exact tell can never activate pairing.
    public string ComposePairingAck(string name, string world, string invitationId, string proofDigest) =>
        $"/tell {name}@{world} collarpairack {invitationId} {proofDigest}";

    /// collar/catalog-sync: the Owner's lifecycle tell telling the paired Sub a signed catalog-request now
    /// exists on the relay - carries only the request's capability id, same "short lifecycle tell, fetch
    /// the signed content separately" shape as the pairing invitation tell.
    public string ComposeCatalogRequestNotice(string name, string world, string requestId) =>
        $"/tell {name}@{world} collarcatalogreq {requestId}";

    /// collar/catalog-sync "Sub has not opted in": sent back to the Owner instead of building/uploading
    /// anything, so the Owner learns a permission status without any catalog content ever existing.
    public string ComposeCatalogPermissionDenied(string name, string world, string requestId) =>
        $"/tell {name}@{world} collarcatalogdenied {requestId}";

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
