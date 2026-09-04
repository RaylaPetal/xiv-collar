using Oathbound.Plugin.Config;

namespace Oathbound.Plugin.Commands;

/// collar/pairing: manual code handshake. Codes only ever gate the one-time pairing message
/// (ChatCommandListener.TryHandlePairingMessage); once accepted, ongoing trigger tells are matched by the
/// server-verified sender name+world captured from that handshake, not by codes. This class only ever
/// writes local config - no network round-trip anywhere in this file.
public sealed class PairingCommand
{
    private readonly PluginConfig config;

    public PairingCommand(PluginConfig config)
    {
        this.config = config;
    }

    /// A fresh code invalidates any handshake attempt using the old one - useful if a code was shared with
    /// the wrong person or a re-pair is needed.
    public void RegenerateMyCode()
    {
        config.Pairing.MyCode = CodeGenerator.Generate();
        config.Save();
    }

    public void SetPeerCode(string code)
    {
        config.Pairing.PeerCode = code.Trim();
        config.Save();
    }

    /// The sole consent action for a completed handshake (collar/pairing: "SHALL NOT auto-enable this
    /// setting"). Called once the Sub explicitly accepts a pending pairing request whose code matched.
    /// `triggerPhrase` (collar/chat-transport) is the peer's own declared trigger phrase, captured from
    /// their handshake message if present - null for an older peer whose handshake didn't declare one, in
    /// which case ChatComposer keeps falling back to this side's own configured trigger phrase.
    public void AcceptPeer(string name, string world, string? triggerPhrase = null)
    {
        config.Pairing.PeerName = name;
        config.Pairing.PeerWorld = world;
        config.Pairing.PeerTriggerPhrase = triggerPhrase;
        config.Pairing.Paired = true;
        config.Save();
    }

    /// Local-only disable, used by panic - never touches anything beyond this client's own config.
    public void EndPairingLocally()
    {
        config.Pairing.Paired = false;
        config.Save();
    }

    /// Clears the captured peer identity entirely (name/world/Paired) so a fresh handshake can start -
    /// unlike EndPairingLocally, this also drops the captured name/world, not just the Paired flag.
    /// Intentionally un-gated on the Owner's side (Settings/CollarWindow only ever expose this for
    /// PluginRole.Owner) - the Sub's own copy of pairing stays locked behind panic, since that's the
    /// side collar/pairing's consent model actually protects. Never touches anything beyond this
    /// client's own config, same as everything else here.
    public void ReleasePeer()
    {
        config.Pairing.PeerName = null;
        config.Pairing.PeerWorld = null;
        config.Pairing.Paired = false;
        config.Save();
    }
}
