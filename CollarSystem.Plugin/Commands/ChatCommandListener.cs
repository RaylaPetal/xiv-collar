using System;
using System.Linq;
using CollarSystem.Plugin.Config;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace CollarSystem.Plugin.Commands;

public readonly record struct PendingPairingRequest(string Name, string World, PluginRole SenderRole);

/// collar/chat-transport's listener, now split into two independent things it watches every incoming
/// tell for:
///  - a one-time pairing handshake message (both roles listen - collar/pairing), which never applies
///    anything, only offers a Pending request once the embedded code matches the locally-configured
///    PeerCode; and
///  - ongoing alias-trigger tells (Sub role only), matched by the sender name+world that a prior accepted
///    handshake captured - the same server-verified-identity check the original design used, just now
///    populated via chat instead of typed in Settings.
public sealed class ChatCommandListener : IDisposable
{
    private const string PairingKeyword = "collarpair";

    /// Reserved first-tokens that route to the Owner's direct "joker" override grammar instead of alias
    /// lookup (see Resolve/HandleForce*). A Sub alias can never be named one of these - CollarWindow's
    /// alias-creation forms validate against this list so the two paths can never collide.
    public static readonly string[] ReservedCategoryWords = ["title", "outfit", "gesture", "collar", "moodle"];

    private readonly PluginConfig config;
    private readonly PairingCommand pairing;
    private readonly TitleCommand title;
    private readonly OutfitCommand outfit;
    private readonly GestureCommand gesture;
    private readonly FollowCommand follow;
    private readonly CollarCommand collar;
    private readonly MoodlesCommand moodles;

    public PendingPairingRequest? Pending { get; private set; }
    public event Action? PendingChanged;

    public ChatCommandListener(PluginConfig config, PairingCommand pairing, TitleCommand title, OutfitCommand outfit, GestureCommand gesture, FollowCommand follow, CollarCommand collar, MoodlesCommand moodles)
    {
        this.config = config;
        this.pairing = pairing;
        this.title = title;
        this.outfit = outfit;
        this.gesture = gesture;
        this.follow = follow;
        this.collar = collar;
        this.moodles = moodles;

        Plugin.ChatGui.ChatMessage += OnChatMessage;
    }

    public void Dispose() => Plugin.ChatGui.ChatMessage -= OnChatMessage;

    /// Called from Settings when the Sub/Owner clicks Accept on a shown Pending request. collar/pairing's
    /// "Accepting a pairing request applies a configured collar": a conditional side effect of acceptance
    /// itself, not a separate command - only fires when the accepting side has both a configured collar
    /// item and the "Collar" permission enabled, silently does nothing otherwise.
    public void AcceptPending()
    {
        if (Pending is not { } request)
            return;

        pairing.AcceptPeer(request.Name, request.World);

        if (config.Permissions.Collar && config.Collar.IsConfigured)
            collar.ForceApply();

        Pending = null;
        PendingChanged?.Invoke();
    }

    public void DismissPending()
    {
        Pending = null;
        PendingChanged?.Invoke();
    }

    private void OnChatMessage(Dalamud.Game.Chat.IChatMessage message)
    {
        if (message.LogKind != XivChatType.TellIncoming)
            return;

        var text = message.Message.TextValue.Trim();

        if (TryHandlePairingMessage(text, message.Sender))
            return;

        // Only the Sub role ever reacts to ongoing alias triggers - the Owner's plugin only composes (see
        // ChatComposer), it never applies anything from a tell.
        if (config.Role != PluginRole.Sub || !config.Pairing.IsPaired)
            return;

        var (senderName, senderWorld) = ExtractNameAndWorld(message.Sender);
        if (senderName is null)
            return;
        if (!string.Equals(senderName, config.Pairing.PeerName, StringComparison.OrdinalIgnoreCase))
            return;
        if (senderWorld is not null && !string.Equals(senderWorld, config.Pairing.PeerWorld, StringComparison.OrdinalIgnoreCase))
            return;

        var trigger = config.TriggerPhrase.Trim();
        if (trigger.Length == 0)
            return;

        if (!text.StartsWith(trigger, StringComparison.OrdinalIgnoreCase))
            return;

        var alias = text[trigger.Length..].Trim();
        if (alias.Length == 0)
            return;

        try
        {
            Resolve(alias);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Failed to apply alias \"{alias}\" from a trigger tell.");
        }
    }

    /// collar/pairing's manual handshake: a "collarpair <role> <code>" tell from either side, `role`
    /// being the sender's own declared Role ("sub"/"owner") so the receiving side's Pending prompt can
    /// show what the sender thinks this pairing will be, not just that a code matched - catches a
    /// same-role misconfiguration (both sides set to Sub, say) before Accept locks it in, rather than
    /// silently pairing into a dead end where nothing ever triggers. Consumes the message (returns true)
    /// whenever it starts with the keyword, whether or not the code actually matches - a wrong/malformed/
    /// unconfigured code is silently ignored, never falls through to alias parsing.
    private bool TryHandlePairingMessage(string text, SeString sender)
    {
        if (!text.StartsWith(PairingKeyword, StringComparison.OrdinalIgnoreCase))
            return false;

        var (roleToken, receivedCode) = SplitFirstToken(text[PairingKeyword.Length..].Trim());
        if (!TryParseRole(roleToken, out var senderRole))
            return true;

        var expectedCode = config.Pairing.PeerCode;
        if (receivedCode.Length == 0 || string.IsNullOrWhiteSpace(expectedCode))
            return true;
        if (!string.Equals(receivedCode, expectedCode, StringComparison.OrdinalIgnoreCase))
            return true;

        var (name, world) = ExtractNameAndWorld(sender);
        if (name is null || world is null)
            return true;

        Pending = new PendingPairingRequest(name, world, senderRole);
        PendingChanged?.Invoke();
        return true;
    }

    private static bool TryParseRole(string token, out PluginRole role)
    {
        switch (token.ToLowerInvariant())
        {
            case "sub":
                role = PluginRole.Sub;
                return true;
            case "owner":
                role = PluginRole.Owner;
                return true;
            default:
                role = default;
                return false;
        }
    }

    /// Dispatches to the Owner's direct "joker" override grammar when the command starts with a reserved
    /// category word, otherwise falls back to collar/chat-transport's normal "alias resolution against a
    /// locally-defined dictionary." An alias that doesn't match anything the Sub has defined is silently
    /// ignored, never an error visible to the Owner - same for an unrecognized joker sub-verb.
    private void Resolve(string commandText)
    {
        var (firstToken, rest) = SplitFirstToken(commandText);
        var permissions = config.Permissions;

        switch (firstToken.ToLowerInvariant())
        {
            case "title":
                if (permissions.Title)
                    HandleForceTitle(rest);
                return;
            case "outfit":
                if (permissions.Outfit)
                    HandleForceOutfit(rest);
                return;
            case "gesture":
                if (permissions.Gesture)
                    HandleForceGesture(rest);
                return;
            case "collar":
                if (permissions.Collar)
                    HandleForceCollar(rest);
                return;
            case "moodle":
                if (permissions.Moodles)
                    HandleForceMoodle(rest);
                return;
        }

        ResolveAlias(commandText);
    }

    private void HandleForceTitle(string rest)
    {
        if (rest.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            title.ForceClear();
            return;
        }

        const string createPrefix = "create ";
        if (rest.StartsWith(createPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var text = StripQuotes(rest[createPrefix.Length..].Trim());
            if (text.Length > 0)
                title.ForceApply(text);
            return;
        }

        Plugin.Log.Information($"Unrecognized \"title\" override \"{rest}\" - expected \"create <text>\" or \"clear\".");
    }

    private void HandleForceOutfit(string rest)
    {
        if (rest.Equals("unlock", StringComparison.OrdinalIgnoreCase))
        {
            outfit.ForceUnlock();
            return;
        }

        const string lockPrefix = "lock ";
        if (rest.StartsWith(lockPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var name = StripQuotes(rest[lockPrefix.Length..].Trim());
            if (name.Length > 0)
                outfit.ForceApply(name);
            return;
        }

        Plugin.Log.Information($"Unrecognized \"outfit\" override \"{rest}\" - expected \"lock <design name>\" or \"unlock\".");
    }

    private void HandleForceGesture(string rest)
    {
        var name = StripQuotes(rest.Trim());
        if (name.Length > 0)
            gesture.ForceQueue(name);
    }

    /// Only `collar unlock` exists - the collar only ever applies as a side effect of pairing acceptance
    /// (see AcceptPending), never through a chat command, so there is no `collar lock` counterpart to
    /// title/outfit's own force-apply grammar.
    private void HandleForceCollar(string rest)
    {
        if (rest.Equals("unlock", StringComparison.OrdinalIgnoreCase))
        {
            collar.ForceUnlock();
            return;
        }

        if (rest.Equals("lock", StringComparison.OrdinalIgnoreCase))
        {
            collar.ForceApply();
            return;
        }

        Plugin.Log.Information($"Unrecognized \"collar\" override \"{rest}\" - expected \"lock\" or \"unlock\".");
    }

    private void HandleForceMoodle(string rest)
    {
        if (rest.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            moodles.ForceClear();
            return;
        }

        const string applyPrefix = "apply ";
        if (rest.StartsWith(applyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var name = StripQuotes(rest[applyPrefix.Length..].Trim());
            if (name.Length > 0)
                moodles.ForceApply(name);
            return;
        }

        Plugin.Log.Information($"Unrecognized \"moodle\" override \"{rest}\" - expected \"apply <preset name>\" or \"clear\".");
    }

    private static (string First, string Remainder) SplitFirstToken(string text)
    {
        var trimmed = text.Trim();
        var spaceIndex = trimmed.IndexOf(' ');
        return spaceIndex < 0 ? (trimmed, "") : (trimmed[..spaceIndex], trimmed[(spaceIndex + 1)..].Trim());
    }

    private static string StripQuotes(string text) => text.Trim('"', '\'');

    private void ResolveAlias(string alias)
    {
        var aliases = config.Aliases;
        var permissions = config.Permissions;

        if (Matches(alias, aliases.ClearTitleAlias))
        {
            if (permissions.Title)
                title.Clear();
            return;
        }

        if (Matches(alias, aliases.UnlockOutfitAlias))
        {
            if (permissions.Outfit)
                outfit.Unlock();
            return;
        }

        if (Matches(alias, aliases.Follow.EngageAlias))
        {
            if (permissions.Follow)
                follow.Engage();
            return;
        }

        if (Matches(alias, aliases.Follow.ReleaseAlias))
        {
            if (permissions.Follow)
                follow.Release();
            return;
        }

        var titleAlias = aliases.Titles.FirstOrDefault(a => Matches(alias, a.Alias));
        if (titleAlias is not null)
        {
            if (permissions.Title)
                title.Apply(titleAlias);
            return;
        }

        var outfitAlias = aliases.Outfits.FirstOrDefault(a => Matches(alias, a.Alias));
        if (outfitAlias is not null)
        {
            if (permissions.Outfit)
                outfit.Apply(outfitAlias);
            return;
        }

        var gestureAlias = aliases.Gestures.FirstOrDefault(a => Matches(alias, a.Alias));
        if (gestureAlias is not null)
        {
            if (permissions.Gesture)
                gesture.Queue(gestureAlias);
            return;
        }

        Plugin.Log.Information($"Received an unrecognized alias \"{alias}\" from the paired Owner - ignored.");
    }

    private static bool Matches(string received, string configured) =>
        !string.IsNullOrWhiteSpace(configured) && string.Equals(received, configured.Trim(), StringComparison.OrdinalIgnoreCase);

    /// Prefers a PlayerPayload when present (structured, unambiguous); falls back to parsing the plain
    /// "Name Surname@World" text form, since not every chat type embeds a PlayerPayload for the sender.
    private static (string? Name, string? World) ExtractNameAndWorld(SeString sender)
    {
        var playerPayload = sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
        if (playerPayload is not null)
            return (playerPayload.PlayerName, playerPayload.World.Value.Name.ExtractText());

        var text = sender.TextValue.Trim();
        var atIndex = text.IndexOf('@');
        return atIndex >= 0 ? (text[..atIndex].Trim(), text[(atIndex + 1)..].Trim()) : (text.Length > 0 ? text : null, null);
    }
}
