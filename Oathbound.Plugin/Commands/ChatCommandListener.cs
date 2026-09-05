using System;
using System.Linq;
using System.Threading;
using Oathbound.Plugin.Config;
using Oathbound.Plugin.Relay;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace Oathbound.Plugin.Commands;

/// collar/pairing "Receiving a panic notification updates the header": the peer's declared role at the
/// moment they panicked, so the header can say "your Sub" or "your Owner" - transient, in-memory.
public readonly record struct PeerUnpairedNotice(PluginRole PeerRole);

/// collar/chat-transport's listener, watching every incoming tell for three things:
///  - a relay invitation reference (`collarinvite`) and its acknowledgement (`collarpairack`) - both roles
///    listen, both only ever hand off to PairingService, which owns the actual relay verification and
///    Pending state (collar/pairing);
///  - a panic/unpair notification (`collarunpair`); and
///  - ongoing alias-trigger tells (Sub role only), matched by the sender name+world an accepted relay
///    pairing captured.
public sealed class ChatCommandListener : IDisposable
{
    private const string PairingAckKeyword = "collarpairack";
    private const string InviteKeyword = "collarinvite";
    private const string UnpairNoticeKeyword = "collarunpair";
    private const string CatalogRequestKeyword = "collarcatalogreq";
    private const string CatalogPermissionDeniedKeyword = "collarcatalogdenied";

    /// Reserved first-tokens that route to the Owner's direct "joker" override grammar instead of alias
    /// lookup (see Resolve/HandleForce*). A Sub alias can never be named one of these - CollarWindow's
    /// alias-creation forms validate against this list so the two paths can never collide.
    public static readonly string[] ReservedCategoryWords = ["title", "outfit", "gesture", "collar", "moodle", "restraint", "customtrigger"];

    private readonly PluginConfig config;
    private readonly PairingService pairing;
    private readonly CatalogSyncRelayService catalogSyncRelay;
    private readonly TitleCommand title;
    private readonly OutfitCommand outfit;
    private readonly GestureCommand gesture;
    private readonly FollowCommand follow;
    private readonly CollarCommand collar;
    private readonly MoodlesCommand moodles;
    private readonly RestraintCommand restraints;
    private readonly CustomTriggerCommand customTriggers;

    public PeerUnpairedNotice? PeerUnpairedNotice { get; private set; }
    public event Action? PeerUnpairedNoticeChanged;

    /// Called from the header once the notice has been shown and acted on (Owner clicks Release; Sub
    /// dismisses the informational note) - clears it back to null.
    public void DismissPeerUnpairedNotice()
    {
        PeerUnpairedNotice = null;
        PeerUnpairedNoticeChanged?.Invoke();
    }

    public ChatCommandListener(PluginConfig config, PairingService pairing, CatalogSyncRelayService catalogSyncRelay, TitleCommand title, OutfitCommand outfit, GestureCommand gesture, FollowCommand follow, CollarCommand collar, MoodlesCommand moodles, RestraintCommand restraints, CustomTriggerCommand customTriggers)
    {
        this.config = config;
        this.pairing = pairing;
        this.catalogSyncRelay = catalogSyncRelay;
        this.title = title;
        this.outfit = outfit;
        this.gesture = gesture;
        this.follow = follow;
        this.collar = collar;
        this.moodles = moodles;
        this.restraints = restraints;
        this.customTriggers = customTriggers;

        Plugin.ChatGui.ChatMessage += OnChatMessage;
    }

    public void Dispose() => Plugin.ChatGui.ChatMessage -= OnChatMessage;

    private void OnChatMessage(Dalamud.Game.Chat.IChatMessage message)
    {
        if (message.LogKind != XivChatType.TellIncoming)
            return;

        var text = message.Message.TextValue.Trim();

        if (TryHandleRelayAckMessage(text, message.Sender))
            return;
        if (TryHandleRelayInviteMessage(text, message.Sender))
            return;
        if (TryHandleUnpairNoticeMessage(text, message.Sender))
            return;
        if (TryHandleCatalogRequestMessage(text, message.Sender))
            return;
        if (TryHandleCatalogPermissionDeniedMessage(text, message.Sender))
            return;

        // Only the Sub role ever reacts to ongoing alias triggers - the Owner's plugin only composes (see
        // ChatComposer), it never applies anything from a tell.
        if (config.Role != PluginRole.Sub || !config.Pairing.IsPaired)
            return;

        var (senderName, senderWorld) = ExtractNameAndWorld(message.Sender);
        if (senderName is null)
            return;
        if (!string.Equals(senderName, config.Pairing.PeerName, StringComparison.OrdinalIgnoreCase))
        {
            Plugin.Log.Information($"Trigger tell ignored: sender \"{senderName}\" does not match the configured peer \"{config.Pairing.PeerName}\".");
            return;
        }
        if (senderWorld is not null && !string.Equals(senderWorld, config.Pairing.PeerWorld, StringComparison.OrdinalIgnoreCase))
        {
            Plugin.Log.Information($"Trigger tell ignored: sender world \"{senderWorld}\" does not match the configured peer world \"{config.Pairing.PeerWorld}\".");
            return;
        }

        var trigger = config.TriggerPhrase.Trim();
        if (trigger.Length == 0)
            return;

        if (!text.StartsWith(trigger, StringComparison.OrdinalIgnoreCase))
        {
            Plugin.Log.Information($"Trigger tell ignored: message did not start with the configured trigger phrase \"{trigger}\".");
            return;
        }

        var alias = text[trigger.Length..].Trim();
        if (alias.Length == 0)
            return;

        try
        {
            var outcome = Resolve(alias);
            Plugin.Log.Information($"Trigger tell dispatch: {outcome.Message}");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Failed to apply alias \"{alias}\" from a trigger tell.");
        }
    }

    /// collar/chat-transport "An Owner-style command can be tested entirely locally": exercises the exact
    /// same trigger-phrase check and `Resolve` dispatch a real incoming tell goes through, so this can never
    /// drift from what a real tell would actually do - the only differences are the checks a local test
    /// can't meaningfully perform (there is no real sender to verify, and no tell channel to require).
    /// Never sends or receives any chat message, and never requires pairing.
    public LocalTestResult TestIncomingCommand(string rawText)
    {
        var trigger = config.TriggerPhrase.Trim();
        var trimmed = rawText.Trim();
        if (trigger.Length == 0 || !trimmed.StartsWith(trigger, StringComparison.OrdinalIgnoreCase))
            return LocalTestResult.Fail($"Doesn't start with your configured trigger phrase \"{trigger}\".");

        var alias = trimmed[trigger.Length..].Trim();
        if (alias.Length == 0)
            return LocalTestResult.Fail("No command text after the trigger phrase.");

        try
        {
            return Resolve(alias);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Local command test for \"{alias}\" threw an exception.");
            return LocalTestResult.Fail($"Threw an exception: {ex.Message}");
        }
    }

    /// collar/pairing's relay handshake, inviter side: a "collarpairack <invitationId> <proofDigest>" tell
    /// sent automatically by PairingService.AcceptPendingAsync. Consumes the message (returns true)
    /// whenever it starts with the keyword and parses, matching every other handshake message's "fail
    /// closed, never fall through" shape - the actual verification (does the fetched invitation's signed
    /// acceptance carry this exact proof digest, from an invitation this side created) happens in
    /// PairingService, not here; this method's only job is recognizing the tell and capturing its
    /// server-verified sender.
    private bool TryHandleRelayAckMessage(string text, SeString sender)
    {
        if (!text.StartsWith(PairingAckKeyword, StringComparison.OrdinalIgnoreCase))
            return false;

        var (invitationId, rest) = SplitFirstToken(text[PairingAckKeyword.Length..].Trim());
        var (proofDigest, _) = SplitFirstToken(rest);
        if (invitationId.Length == 0 || proofDigest.Length == 0)
            return true;

        var (name, world) = ExtractNameAndWorld(sender);
        if (name is null || world is null)
            return true;

        Plugin.FireAndForget(pairing.HandleAcknowledgementTellAsync(invitationId, proofDigest, name, world, CancellationToken.None));
        return true;
    }

    /// collar/pairing's relay handshake, receiver side: a "collarinvite <invitationId>" tell from either
    /// role. `senderName`/`senderWorld` (FFXIV's own server-verified sender) is the character identity
    /// PairingService binds the fetched invitation to - never a free-text field. Consumes the message
    /// (returns true) whenever it starts with the keyword and parses, whether or not the invitation turns
    /// out to be valid once fetched - a wrong/expired/forged reference is silently ignored by
    /// PairingService, never falls through to alias parsing.
    private bool TryHandleRelayInviteMessage(string text, SeString sender)
    {
        if (!text.StartsWith(InviteKeyword, StringComparison.OrdinalIgnoreCase))
            return false;

        var (invitationId, _) = SplitFirstToken(text[InviteKeyword.Length..].Trim());
        if (invitationId.Length == 0)
            return true;

        var (name, world) = ExtractNameAndWorld(sender);
        if (name is null || world is null)
            return true;

        Plugin.FireAndForget(pairing.HandleInvitationTellAsync(invitationId, name, world, CancellationToken.None));
        return true;
    }

    /// collar/pairing "Receiving a panic notification updates the header": a "collarunpair <role>" tell
    /// sent automatically by PanicHandler. Verified by comparing the sender against this side's own
    /// currently-configured peer name/world - no code involved, since ending an already-trusted
    /// relationship doesn't need the same shared-secret gate establishing one does (see design.md). A
    /// notice from anyone else is silently ignored, the same "fail closed" shape every other handshake
    /// message here already uses.
    private bool TryHandleUnpairNoticeMessage(string text, SeString sender)
    {
        if (!text.StartsWith(UnpairNoticeKeyword, StringComparison.OrdinalIgnoreCase))
            return false;

        var (roleToken, _) = SplitFirstToken(text[UnpairNoticeKeyword.Length..].Trim());
        if (!TryParseRole(roleToken, out var peerRole))
            return true;

        var (name, world) = ExtractNameAndWorld(sender);
        if (name is null)
            return true;
        if (!string.Equals(name, config.Pairing.PeerName, StringComparison.OrdinalIgnoreCase))
            return true;
        if (world is not null && !string.Equals(world, config.Pairing.PeerWorld, StringComparison.OrdinalIgnoreCase))
            return true;

        PeerUnpairedNotice = new PeerUnpairedNotice(peerRole);
        if (config.Role == PluginRole.Sub)
            restraints.ForceUnlock();
        pairing.EndFromVerifiedPeerNotice();
        PeerUnpairedNoticeChanged?.Invoke();
        return true;
    }

    /// collar/catalog-sync: a "collarcatalogreq <requestId>" tell from either role - CatalogSyncRelayService
    /// itself re-verifies sender identity and the request's own signature before doing anything, so this
    /// method's only job is recognizing the keyword and capturing the verified sender.
    private bool TryHandleCatalogRequestMessage(string text, SeString sender)
    {
        if (!text.StartsWith(CatalogRequestKeyword, StringComparison.OrdinalIgnoreCase))
            return false;

        var (requestId, _) = SplitFirstToken(text[CatalogRequestKeyword.Length..].Trim());
        if (requestId.Length == 0)
            return true;

        var (name, world) = ExtractNameAndWorld(sender);
        if (name is null || world is null)
            return true;

        Plugin.FireAndForget(catalogSyncRelay.HandleCatalogRequestTellAsync(requestId, name, world, CancellationToken.None));
        return true;
    }

    /// collar/catalog-sync "Sub has not opted in": a "collarcatalogdenied <requestId>" tell telling the
    /// Owner their paired Sub hasn't enabled catalog sync.
    private bool TryHandleCatalogPermissionDeniedMessage(string text, SeString sender)
    {
        if (!text.StartsWith(CatalogPermissionDeniedKeyword, StringComparison.OrdinalIgnoreCase))
            return false;

        var (requestId, _) = SplitFirstToken(text[CatalogPermissionDeniedKeyword.Length..].Trim());
        if (requestId.Length == 0)
            return true;
        if (!string.Equals(ExtractNameAndWorld(sender).Name, config.Pairing.PeerName, StringComparison.OrdinalIgnoreCase))
            return true;

        catalogSyncRelay.HandlePermissionDeniedTell(requestId);
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
    /// locally-defined dictionary." Returns a `LocalTestResult` describing what happened - reused both for
    /// this dispatch's own diagnostic log line (OnChatMessage) and for the local test tool
    /// (TestIncomingCommand), so neither can drift from what a real tell actually does. An alias that
    /// doesn't match anything the Sub has defined is still never an error visible to the Owner - the result
    /// here is purely local (logged or shown to the Sub only).
    private LocalTestResult Resolve(string commandText)
    {
        var (firstToken, rest) = SplitFirstToken(commandText);
        var permissions = config.Permissions;

        switch (firstToken.ToLowerInvariant())
        {
            case "title":
                return permissions.Title ? HandleForceTitle(rest) : LocalTestResult.Fail("Title permission is not enabled.");
            case "outfit":
                return permissions.Outfit ? HandleForceOutfit(rest) : LocalTestResult.Fail("Outfit permission is not enabled.");
            case "gesture":
                return permissions.Gesture && config.TosAcknowledged ? HandleForceGesture(rest) : LocalTestResult.Fail("Gesture permission or the automation-risk acknowledgement is not enabled.");
            case "collar":
                return permissions.Collar ? HandleForceCollar(rest) : LocalTestResult.Fail("Collar permission is not enabled.");
            case "moodle":
                return permissions.Moodles ? HandleForceMoodle(rest) : LocalTestResult.Fail("Moodles permission is not enabled.");
            case "restraint":
                return permissions.Restraints && config.TosAcknowledged ? HandleForceRestraint(rest) : LocalTestResult.Fail("Restraints permission or the automation-risk acknowledgement is not enabled.");
            case "customtrigger":
                // Deliberately no outer permission gate here, unlike every other case above - a
                // Custom Trigger bundle mixes categories with independent permissions, so
                // CustomTriggerCommand.Apply checks each bundled action's own permission (and Chat's
                // dedicated acknowledgement) individually as it dispatches (design.md's "orchestrator,
                // not a reimplementation" decision - see also the ResolveAlias CustomTriggers branch).
                return HandleForceCustomTrigger(rest);
        }

        return ResolveAlias(commandText);
    }

    private LocalTestResult HandleForceTitle(string rest)
    {
        if (rest.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            title.ForceClear();
            return LocalTestResult.Ok("Title cleared.");
        }

        const string createPrefix = "create ";
        if (rest.StartsWith(createPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var text = StripQuotes(rest[createPrefix.Length..].Trim());
            if (text.Length > 0)
            {
                title.ForceApply(text);
                return LocalTestResult.Ok($"Title \"{text}\" applied.");
            }
            return LocalTestResult.Fail("\"title create\" was given no text.");
        }

        const string stylePrefix = "style ";
        if (rest.StartsWith(stylePrefix, StringComparison.OrdinalIgnoreCase))
        {
            if (TitleCommand.TryParseStyleCommand(rest[stylePrefix.Length..], out var text, out var isPrefix, out var color))
            {
                title.ForceApply(text, isPrefix, color);
                return LocalTestResult.Ok($"Title \"{text}\" applied with style.");
            }
            return LocalTestResult.Fail("\"title style\" was malformed - expected \"style \\\"<text>\\\" prefix:<0|1> color:<r>,<g>,<b>\".");
        }

        return LocalTestResult.Fail($"Unrecognized \"title\" override \"{rest}\" - expected \"create <text>\", \"style \\\"<text>\\\" prefix:<0|1> color:<r>,<g>,<b>\", or \"clear\".");
    }

    private LocalTestResult HandleForceOutfit(string rest)
    {
        if (rest.Equals("unlock", StringComparison.OrdinalIgnoreCase))
        {
            return outfit.ForceUnlock()
                ? LocalTestResult.Ok("Outfit unlocked.")
                : LocalTestResult.Fail("Outfit unlock failed - nothing was locked.");
        }

        const string lockPrefix = "lock ";
        if (rest.StartsWith(lockPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var name = StripQuotes(rest[lockPrefix.Length..].Trim());
            if (name.Length > 0)
            {
                var (success, reason) = outfit.ForceApply(name);
                return success
                    ? LocalTestResult.Ok($"Outfit \"{name}\" applied and locked." + (reason is null ? "" : $" {reason}"))
                    : LocalTestResult.Fail($"Outfit \"{name}\" not applied: {reason}");
            }
            return LocalTestResult.Fail("\"outfit lock\" was given no design name.");
        }

        return LocalTestResult.Fail($"Unrecognized \"outfit\" override \"{rest}\" - expected \"lock <design name>\" or \"unlock\".");
    }

    private LocalTestResult HandleForceGesture(string rest)
    {
        var name = rest.Trim();
        if (name.Length == 0)
            return LocalTestResult.Fail("\"gesture\" was given no name.");

        var result = gesture.ForceApplyDetailed(name);
        return result.Status switch
        {
            GestureCommand.ApplyStatus.Success => LocalTestResult.Ok($"Gesture \"{result.DisplayName ?? name}\" queued for playback."),
            GestureCommand.ApplyStatus.Missing => LocalTestResult.Fail($"Gesture \"{name}\" is missing or stale in the Sub's current animation catalog. Re-import and edit the Owner quick command."),
            GestureCommand.ApplyStatus.Ambiguous => LocalTestResult.Fail($"Gesture \"{name}\" matches more than one animation; choose a more specific selector."),
            GestureCommand.ApplyStatus.Malformed => LocalTestResult.Fail($"Gesture selector \"{name}\" is malformed."),
            GestureCommand.ApplyStatus.CollectionUnavailable => LocalTestResult.Fail("Gesture failed because the Sub's effective Penumbra collection is unavailable."),
            GestureCommand.ApplyStatus.TemporarySettingsFailed => LocalTestResult.Fail($"Gesture \"{result.DisplayName ?? name}\" failed while applying temporary Penumbra settings."),
            GestureCommand.ApplyStatus.RedrawFailed => LocalTestResult.Fail($"Gesture \"{result.DisplayName ?? name}\" failed while redrawing; temporary settings were rolled back."),
            _ => LocalTestResult.Fail($"Gesture \"{name}\" failed."),
        };
    }

    /// Only `collar unlock` exists - the collar only ever applies as a side effect of pairing acceptance
    /// (see AcceptPending), never through a chat command, so there is no `collar lock` counterpart to
    /// title/outfit's own force-apply grammar.
    private LocalTestResult HandleForceCollar(string rest)
    {
        if (rest.Equals("unlock", StringComparison.OrdinalIgnoreCase))
        {
            return collar.ForceUnlock()
                ? LocalTestResult.Ok("Collar unlocked.")
                : LocalTestResult.Fail("Collar unlock failed - nothing was locked.");
        }

        if (rest.Equals("lock", StringComparison.OrdinalIgnoreCase))
        {
            return collar.ForceApply()
                ? LocalTestResult.Ok("Collar applied and locked.")
                : LocalTestResult.Fail("Collar apply failed - no collar item configured.");
        }

        return LocalTestResult.Fail($"Unrecognized \"collar\" override \"{rest}\" - expected \"lock\" or \"unlock\".");
    }

    private LocalTestResult HandleForceMoodle(string rest)
    {
        if (rest.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            return moodles.ForceClear()
                ? LocalTestResult.Ok("Moodle cleared.")
                : LocalTestResult.Fail("Moodle clear failed - Moodles may be unavailable.");
        }

        const string applyPrefix = "apply ";
        if (rest.StartsWith(applyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var name = rest[applyPrefix.Length..].Trim();
            if (name.Length > 0)
            {
                return moodles.ForceApply(name)
                    ? LocalTestResult.Ok($"Moodle \"{name}\" applied.")
                    : LocalTestResult.Fail($"No Moodles status named \"{name}\" (or the apply failed).");
            }
            return LocalTestResult.Fail("\"moodle apply\" was given no status name.");
        }

        return LocalTestResult.Fail($"Unrecognized \"moodle\" override \"{rest}\" - expected \"apply <status name>\" or \"clear\".");
    }

    private LocalTestResult HandleForceRestraint(string rest)
    {
        if (rest.Equals("unlock", StringComparison.OrdinalIgnoreCase))
        {
            return restraints.ForceUnlock()
                ? LocalTestResult.Ok("Restraints unlocked.")
                : LocalTestResult.Fail("Restraint unlock failed - nothing was force-locked.");
        }

        const string lockPrefix = "lock ";
        if (rest.StartsWith(lockPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var remainder = rest[lockPrefix.Length..];
            if (RestraintCommand.TryParseLockCommand(remainder, out var name, out var rules) && name.Length > 0)
            {
                var applied = rules is { Count: > 0 } ? restraints.ForceApply(name, rules) : restraints.ForceApply(name);
                return applied
                    ? LocalTestResult.Ok($"Restraint device \"{name}\" applied.")
                    : LocalTestResult.Fail($"No restraint device named \"{name}\" (or the apply failed).");
            }
            return LocalTestResult.Fail("\"restraint lock\" was given no device name.");
        }

        const string catalogPrefix = "catalog ";
        if (rest.StartsWith(catalogPrefix, StringComparison.OrdinalIgnoreCase))
        {
            if (!RestraintCommand.TryParseCatalogCommand(rest[catalogPrefix.Length..], out var id, out var itemId, out var rules))
                return LocalTestResult.Fail("The catalog restraint command was malformed.");
            return restraints.ForceApplyCatalog(id, itemId, rules)
                ? LocalTestResult.Ok("Shared restraint applied.")
                : LocalTestResult.Fail(restraints.LastFailureReason ?? "The shared restraint could not be applied.");
        }

        const string wearPrefix = "wear ";
        if (rest.StartsWith(wearPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var remainder = rest[wearPrefix.Length..];
            if (RestraintCommand.TryParseWearCommand(remainder, out var slot, out var itemId, out var label, out var rules))
            {
                return restraints.ForceApplyAdHoc(slot, itemId, label, rules)
                    ? LocalTestResult.Ok($"Ad-hoc restraint device \"{label}\" applied.")
                    : LocalTestResult.Fail($"Ad-hoc restraint device \"{label}\" failed to apply.");
            }
            return LocalTestResult.Fail("\"restraint wear\" was malformed - expected \"wear <slot> <itemId> \\\"<label>\\\" rules:...\".");
        }

        return LocalTestResult.Fail($"Unrecognized \"restraint\" override \"{rest}\" - expected \"catalog <id> \\\"<label>\\\" rules:...\", \"disable <id>\", \"wear <slot> <itemId> \\\"<label>\\\" rules:...\", or \"unlock\".");
    }

    private LocalTestResult HandleForceCustomTrigger(string rest)
    {
        const string castPrefix = "cast ";
        if (rest.StartsWith(castPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var remainder = rest[castPrefix.Length..];
            if (CustomTriggerCommand.TryParseCastCommand(remainder, out var label, out var actions))
            {
                var result = customTriggers.Apply(actions);
                return result.Success
                    ? LocalTestResult.Ok($"Custom trigger \"{label}\": {result.Message}")
                    : LocalTestResult.Fail($"Custom trigger \"{label}\": {result.Message}");
            }
            return LocalTestResult.Fail("\"customtrigger cast\" was malformed - expected \"cast \\\"<label>\\\" title=...;outfit=...;gesture=...;moodle=...;restraint=...;chat=<rest of line>\" (chat, if present, must be last).");
        }

        return LocalTestResult.Fail($"Unrecognized \"customtrigger\" override \"{rest}\" - expected \"cast \\\"<label>\\\" ...\".");
    }

    private static (string First, string Remainder) SplitFirstToken(string text)
    {
        var trimmed = text.Trim();
        var spaceIndex = trimmed.IndexOf(' ');
        return spaceIndex < 0 ? (trimmed, "") : (trimmed[..spaceIndex], trimmed[(spaceIndex + 1)..].Trim());
    }

    private static string StripQuotes(string text) => text.Trim('"', '\'');

    private LocalTestResult ResolveAlias(string alias)
    {
        var aliases = config.Aliases;
        var permissions = config.Permissions;

        if (Matches(alias, aliases.ClearTitleAlias))
        {
            if (!permissions.Title)
                return LocalTestResult.Fail("Title permission is not enabled.");
            title.Clear();
            return LocalTestResult.Ok($"Alias \"{alias}\" matched clear-title.");
        }

        // Fixed release vocabulary: unlike user-authored apply aliases, wardrobe release is always
        // "unlock" so an Owner never has to discover a mutable safety command.
        if (Matches(alias, "unlock"))
        {
            if (!permissions.Outfit)
                return LocalTestResult.Fail("Outfit permission is not enabled.");
            outfit.Unlock();
            return LocalTestResult.Ok($"Alias \"{alias}\" matched unlock-outfit.");
        }

        if (Matches(alias, aliases.Follow.EngageAlias))
        {
            if (!permissions.Follow)
                return LocalTestResult.Fail("Follow permission is not enabled.");
            return follow.Engage()
                ? LocalTestResult.Ok($"Alias \"{alias}\" matched leash-engage.")
                : LocalTestResult.Fail("Leash engage failed - movement lock is unavailable.");
        }

        if (Matches(alias, aliases.Follow.ReleaseAlias))
        {
            if (!permissions.Follow)
                return LocalTestResult.Fail("Follow permission is not enabled.");
            follow.Release();
            return LocalTestResult.Ok($"Alias \"{alias}\" matched leash-release.");
        }

        var titleAlias = aliases.Titles.FirstOrDefault(a => Matches(alias, a.Alias));
        if (titleAlias is not null)
        {
            if (!permissions.Title)
                return LocalTestResult.Fail("Title permission is not enabled.");
            title.Apply(titleAlias);
            return LocalTestResult.Ok($"Alias \"{alias}\" matched a title.");
        }

        var outfitAlias = aliases.Outfits.FirstOrDefault(a => Matches(alias, a.Alias));
        if (outfitAlias is not null)
        {
            if (!permissions.Outfit)
                return LocalTestResult.Fail("Outfit permission is not enabled.");
            var (outfitApplied, outfitReason) = outfit.Apply(outfitAlias);
            return outfitApplied
                ? LocalTestResult.Ok($"Alias \"{alias}\" matched an outfit." + (outfitReason is null ? "" : $" {outfitReason}"))
                : LocalTestResult.Fail($"Alias \"{alias}\" matched an outfit, but it wasn't applied: {outfitReason}");
        }

        var gestureAlias = aliases.Gestures.FirstOrDefault(a => Matches(alias, a.Alias));
        if (gestureAlias is not null)
        {
            if (!(permissions.Gesture && config.TosAcknowledged))
                return LocalTestResult.Fail("Gesture permission or the automation-risk acknowledgement is not enabled.");
            return gesture.Apply(gestureAlias)
                ? LocalTestResult.Ok($"Alias \"{alias}\" matched a gesture.")
                : LocalTestResult.Fail($"Alias \"{alias}\" matched a gesture, but it failed to play.");
        }

        var restraintAlias = aliases.Restraints.FirstOrDefault(a => Matches(alias, a.Alias));
        if (restraintAlias is not null)
        {
            if (!(permissions.Restraints && config.TosAcknowledged))
                return LocalTestResult.Fail("Restraints permission or the automation-risk acknowledgement is not enabled.");
            return restraints.Toggle(restraintAlias)
                ? LocalTestResult.Ok($"Alias \"{alias}\" matched a restraint device (toggled).")
                : LocalTestResult.Fail($"Alias \"{alias}\" matched a restraint device, but it is currently force-locked.");
        }

        if (Matches(alias, aliases.ClearMoodleAlias))
        {
            if (!permissions.Moodles)
                return LocalTestResult.Fail("Moodles permission is not enabled.");
            return moodles.Clear()
                ? LocalTestResult.Ok($"Alias \"{alias}\" matched clear-moodle.")
                : LocalTestResult.Fail($"Alias \"{alias}\" matched clear-moodle, but Moodles may be unavailable.");
        }

        var moodleAlias = aliases.Moodles.FirstOrDefault(a => Matches(alias, a.Alias));
        if (moodleAlias is not null)
        {
            if (!permissions.Moodles)
                return LocalTestResult.Fail("Moodles permission is not enabled.");
            return moodles.Apply(moodleAlias)
                ? LocalTestResult.Ok($"Alias \"{alias}\" matched a Moodle.")
                : LocalTestResult.Fail($"Alias \"{alias}\" matched a Moodle, but it failed to apply.");
        }

        // collar/custom-triggers "Sub defines a named Custom Trigger bundling multiple actions": no
        // category permission gate here - each bundled action checks its own category's permission
        // independently inside CustomTriggerCommand.Apply, so a disabled category is skipped rather than
        // blocking the whole trigger the way every other branch above does.
        var customTrigger = aliases.CustomTriggers.FirstOrDefault(t => Matches(alias, t.Alias));
        if (customTrigger is not null)
            return customTriggers.Apply(customTrigger.Actions);

        return LocalTestResult.Fail($"No matching alias or reserved-word command for \"{alias}\".");
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
