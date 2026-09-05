using System;
using System.Threading;
using System.Threading.Tasks;
using Oathbound.Plugin.Commands;
using Oathbound.Plugin.Config;

namespace Oathbound.Plugin.Relay;

/// A relay invitation this side created and is waiting on (inviter role). Its non-secret reference,
/// target, and expiry are persisted so a matching acknowledgement can still complete after a restart.
public readonly record struct OutgoingInvitation(string InvitationId, PluginRole DeclaredRole, string Target, long ExpiresAt);

/// Mirrors the old `PendingPairingRequest`/`PeerUnpairedNotice` shape ChatCommandListener/CollarWindow/
/// SettingsWindow already know how to render, but populated from a fetched-and-verified relay invitation
/// instead of a code match.
public readonly record struct PendingPairingRequest(string InvitationId, string Name, string World, PluginRole SenderRole, string? TriggerPhrase, long ExpiresAt);

/// collar/pairing's relay-assisted handshake (Relay-assisted pairing binds device proof to verified game
/// identity). Owns every step of both roles' state machine; ChatCommandListener only recognizes the two
/// short lifecycle tells (`collarinvite`, `collarpairack`) and a
/// verified sender, then delegates here. Nothing in this class ever activates a pairing from relay state
/// alone - see HandleAcknowledgementTellAsync's comment.
public sealed class PairingService
{
    private readonly PluginConfig config;
    private readonly RelayClient relay;
    private readonly DeviceIdentityService identity;
    private readonly ChatComposer composer;
    private readonly ChatSender sender;
    private readonly CollarCommand collar;
    private readonly RevocationService revocation;

    private OutgoingInvitation? outgoingInvitation;
    public long? OutgoingInvitationExpiresAt => outgoingInvitation?.ExpiresAt;
    public string? OutgoingInvitationTarget => outgoingInvitation?.Target;
    public string Phase => config.Pairing.IsPaired ? "Paired" : Pending is not null ? "Invitation received" : AwaitingActivation ? "Waiting for peer confirmation" : outgoingInvitation is not null ? "Invitation sent" : "Not paired";

    public PendingPairingRequest? Pending { get; private set; }
    public event Action? PendingChanged;

    /// Fired once a pairing actually activates (inviter side, after consume succeeds). CollarWindow's
    /// stale "your peer panicked" notice is superseded by a freshly-completed pairing - most relevant when
    /// re-pairing with the same person after they panicked - but that notice lives in ChatCommandListener,
    /// not here, so this is an event rather than a direct call.
    public event Action? PairingActivated;
    public event Action? PairingEnded;

    /// Set after CreateAndSendInvitationAsync/AcceptPendingAsync/HandleAcknowledgementTellAsync fail, so
    /// Settings can show *why* without the caller needing its own try/catch around every button click.
    public string? LastError { get; private set; }
    public event Action? LastErrorChanged;

    /// True from a successful Accept until this side's own activation poll (see AwaitActivationAsync)
    /// either succeeds or gives up - lets Settings show "waiting for confirmation" instead of looking stuck.
    public bool AwaitingActivation { get; private set; }
    public event Action? AwaitingActivationChanged;

    public PairingService(PluginConfig config, RelayClient relay, DeviceIdentityService identity, ChatComposer composer, ChatSender sender, CollarCommand collar, RevocationService revocation)
    {
        this.config = config;
        this.relay = relay;
        this.identity = identity;
        this.composer = composer;
        this.sender = sender;
        this.collar = collar;
        this.revocation = revocation;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var interrupted = config.PendingRelayOperations.FindLast(o =>
            o.Kind == "pair-invite" && o.ExpiresAt > now && !string.IsNullOrWhiteSpace(o.OperationId));
        if (interrupted is not null)
            outgoingInvitation = new OutgoingInvitation(interrupted.OperationId, config.Role, interrupted.Target ?? "", interrupted.ExpiresAt);
    }

    private void SetError(string? message)
    {
        LastError = message;
        LastErrorChanged?.Invoke();
    }

    /// Inviter side, step 1: create a single-use invitation via the relay and send its reference in one
    /// tell. One click, one invitation, one tell (task 4.1).
    public async Task<bool> CreateAndSendInvitationAsync(string targetTellAddress, CancellationToken ct)
    {
        try
        {
            identity.EnsureIdentity();
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var envelope = new InvitationEnvelope
            {
                InvitationId = RelayCrypto.RandomCapabilityId(),
                InviterDeviceKeyId = identity.DeviceKeyId!,
                InviterPublicKey = identity.GetPublicKeyJwk(),
                Role = config.Role == PluginRole.Owner ? "owner" : "sub",
                TriggerPhrase = config.TriggerPhrase.Trim(),
                CreatedAt = now,
                ExpiresAt = now + 900,
            };
            envelope.Signature = RelayCrypto.SignRaw(identity.GetSigningKey(), EnvelopeCanonical.SerializeExcludingSignature(envelope));

            var created = await relay.CreateInvitationAsync(envelope, ct).ConfigureAwait(false);
            outgoingInvitation = new OutgoingInvitation(created.InvitationId, config.Role, targetTellAddress.Trim(), created.ExpiresAt);
            config.PendingRelayOperations.RemoveAll(o => o.Kind == "pair-invite");
            config.PendingRelayOperations.Add(new PendingRelayOperationState { Kind = "pair-invite", OperationId = created.InvitationId, Target = targetTellAddress.Trim(), ExpiresAt = created.ExpiresAt });
            config.Save();

            var tell = composer.ComposeRelayInvitation(targetTellAddress, created.InvitationId);
            sender.Send(tell);
            SetError(null);
            return true;
        }
        catch (RelayException ex)
        {
            SetError(DescribeError(ex));
            return false;
        }
    }

    /// Receiver side, step 1: a `collarinvite <invitationId>` tell arrived from `senderName`@`senderWorld`
    /// (already verified by Dalamud's own chat sender field - see ChatCommandListener). Fetches and
    /// independently verifies the invitation's own signature before ever showing it as a Pending request;
    /// a copied/forged reference that doesn't verify is silently dropped, never shown.
    public async Task HandleInvitationTellAsync(string invitationId, string senderName, string senderWorld, CancellationToken ct)
    {
        try
        {
            var invitation = await relay.FetchInvitationAsync(invitationId, ct).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (invitation.Type != "invitation" || invitation.SchemaVersion != 1 || invitation.ExpiresAt <= now || invitation.CreatedAt > now + 300)
            {
                Plugin.Log.Warning("Relay invitation tell ignored: invitation version or lifetime was invalid.");
                return;
            }
            if (!RelayCrypto.VerifyRaw(invitation.InviterPublicKey, invitation.Signature ?? "", EnvelopeCanonical.SerializeExcludingSignature(invitation)))
            {
                Plugin.Log.Warning("Relay invitation tell ignored: invitation signature did not verify.");
                return;
            }
            if (invitation.InviterDeviceKeyId != RelayCrypto.DeviceKeyId(invitation.InviterPublicKey))
            {
                Plugin.Log.Warning("Relay invitation tell ignored: declared inviterDeviceKeyId did not match the inviter's own public key.");
                return;
            }

            if (invitation.Role is not ("owner" or "sub")) return;
            var senderRole = invitation.Role == "owner" ? PluginRole.Owner : PluginRole.Sub;
            if (senderRole == config.Role)
            {
                SetError("Pairing requires one Owner and one Sub. Change Role before accepting this invitation.");
                return;
            }
            Pending = new PendingPairingRequest(invitationId, senderName, senderWorld, senderRole, invitation.TriggerPhrase, invitation.ExpiresAt);
            PendingChanged?.Invoke();
        }
        catch (RelayException ex)
        {
            Plugin.Log.Information($"Relay invitation tell ignored: {DescribeError(ex)}");
        }
    }

    public void DismissPending()
    {
        Pending = null;
        PendingChanged?.Invoke();
    }

    /// Receiver side, step 2: explicit Accept. Publishes a signed acceptance proof, then sends exactly one
    /// bounded acknowledgement tell back to the inviter - this side does not consider itself paired yet
    /// (collar/pairing "Relay acceptance lacks matching game identity" is the inviter's problem to solve,
    /// not this side's; this side's own Pending clears either way once Accept is clicked).
    public async Task<bool> AcceptPendingAsync(CancellationToken ct)
    {
        if (Pending is not { } request) return false;
        if (request.ExpiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            DismissPending();
            SetError("That invitation expired - ask for a fresh one.");
            return false;
        }

        try
        {
            identity.EnsureIdentity();
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var proofDigest = RelayCrypto.Sha256Hex(RelayCrypto.RandomBytes(32));
            var envelope = new AcceptanceEnvelope
            {
                InvitationId = request.InvitationId,
                AccepterDeviceKeyId = identity.DeviceKeyId!,
                AccepterPublicKey = identity.GetPublicKeyJwk(),
                ProofDigest = proofDigest,
                Role = config.Role == PluginRole.Owner ? "owner" : "sub",
                TriggerPhrase = config.TriggerPhrase.Trim(),
                CreatedAt = now,
                ExpiresAt = now + 900,
            };
            envelope.Signature = RelayCrypto.SignRaw(identity.GetSigningKey(), EnvelopeCanonical.SerializeExcludingSignature(envelope));

            await relay.AcceptInvitationAsync(request.InvitationId, envelope, ct).ConfigureAwait(false);

            var ack = composer.ComposePairingAck(request.Name, request.World, request.InvitationId, proofDigest);
            sender.Send(ack);

            // collar/pairing "Accepting a pairing request applies a configured collar": a conditional side
            // effect of acceptance itself, not a separate command.
            if (config.Permissions.Collar && config.Collar.IsConfigured)
                collar.ForceApply();

            Pending = null;
            PendingChanged?.Invoke();
            SetError(null);

            // The accepter never calls consume, so it has no other way to learn the pair epoch the inviter
            // is about to assign - poll the deterministic pairIdHash (bounded) rather than block Accept on it.
            _ = AwaitActivationAsync(request, ct);
            return true;
        }
        catch (RelayException ex)
        {
            SetError(DescribeError(ex));
            return false;
        }
    }

    /// Bounded background poll (accepter side): the inviter typically calls consume within seconds of
    /// receiving the acknowledgement tell, so this checks every few seconds for up to two minutes before
    /// giving up and surfacing an error - the accepted invitation itself already recorded this side's
    /// consent; this is purely "learn what epoch the inviter assigned," not a second consent step.
    private async Task AwaitActivationAsync(PendingPairingRequest request, CancellationToken ct)
    {
        AwaitingActivation = true;
        AwaitingActivationChanged?.Invoke();
        try
        {
            var ownDeviceKeyId = identity.DeviceKeyId!;
            var inviterInvitation = await relay.FetchInvitationAsync(request.InvitationId, ct).ConfigureAwait(false);
            var pairIdHash = RelayCrypto.ComputePairIdHash(ownDeviceKeyId, inviterInvitation.InviterDeviceKeyId);

            for (var attempt = 0; attempt < 40; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var pair = await relay.FetchPairAsync(pairIdHash, ct).ConfigureAwait(false);
                    ActivateLocally(pair, request.Name, request.World, inviterInvitation.InviterDeviceKeyId, inviterInvitation.InviterPublicKey, request.TriggerPhrase);
                    SetError(null);
                    return;
                }
                catch (RelayException ex) when (ex.Code is "unauthorized" or "not_found")
                {
                    // Not activated yet (the inviter hasn't called consume) - keep waiting.
                }
                await Task.Delay(TimeSpan.FromSeconds(3), ct).ConfigureAwait(false);
            }

            SetError("The other side hasn't confirmed yet - ask them to check their pending invitation.");
        }
        catch (RelayException ex)
        {
            SetError(DescribeError(ex));
        }
        catch (OperationCanceledException)
        {
            // Plugin shutting down or caller cancelled - not an error to surface.
        }
        finally
        {
            AwaitingActivation = false;
            AwaitingActivationChanged?.Invoke();
        }
    }

    /// Inviter side, step 2: a `collarpairack <invitationId> <proofDigest>` tell arrived from a
    /// server-verified sender. Activates the pairing only when the fetched invitation's acceptance proof
    /// digest matches exactly what this tell carries - the tell's verified sender is what binds the relay's
    /// claimed acceptance to an actual character; relay state alone is never sufficient (collar/pairing
    /// "Relay acceptance lacks matching game identity").
    public async Task HandleAcknowledgementTellAsync(string invitationId, string proofDigestHex, string senderName, string senderWorld, CancellationToken ct)
    {
        if (outgoingInvitation is not { } outgoing || outgoing.InvitationId != invitationId)
            return; // Not an invitation we created (or already consumed) - ignore, never activate from claims.

        try
        {
            var invitation = await relay.FetchInvitationAsync(invitationId, ct).ConfigureAwait(false);
            if (invitation.Acceptance is not { } acceptance) return;
            if (acceptance.Role is { } acceptedRole &&
                acceptedRole != (config.Role == PluginRole.Owner ? "sub" : "owner")) return;
            if (!string.Equals(acceptance.ProofDigest, proofDigestHex, StringComparison.OrdinalIgnoreCase)) return;
            if (!RelayCrypto.VerifyRaw(acceptance.AccepterPublicKey, acceptance.Signature ?? "", EnvelopeCanonical.SerializeExcludingSignature(acceptance)))
            {
                Plugin.Log.Warning("Relay acknowledgement tell ignored: acceptance signature did not verify.");
                return;
            }

            var pair = await relay.ConsumeInvitationAsync(invitationId, ct).ConfigureAwait(false);
            outgoingInvitation = null;
            config.PendingRelayOperations.RemoveAll(o => o.Kind == "pair-invite" && o.OperationId == invitationId);

            ActivateLocally(pair, senderName, senderWorld, acceptance.AccepterDeviceKeyId, acceptance.AccepterPublicKey, acceptance.TriggerPhrase);
        }
        catch (RelayException ex)
        {
            Plugin.Log.Information($"Relay pairing activation failed: {DescribeError(ex)}");
        }
    }

    private void ActivateLocally(PairEnvelope pair, string peerName, string peerWorld, string peerDeviceKeyId, EcPublicKeyJwk peerPublicKey, string? peerTriggerPhrase)
    {
        var ownKeyId = identity.DeviceKeyId;
        var expectedOwner = config.Role == PluginRole.Owner ? ownKeyId : peerDeviceKeyId;
        var expectedSub = config.Role == PluginRole.Sub ? ownKeyId : peerDeviceKeyId;
        if (pair.Type != "pair" || pair.SchemaVersion != 1 || pair.OwnerDeviceKeyId != expectedOwner || pair.SubDeviceKeyId != expectedSub ||
            pair.PairIdHash != RelayCrypto.ComputePairIdHash(ownKeyId!, peerDeviceKeyId))
        {
            SetError("The relay returned pairing data that did not match the verified devices and roles.");
            return;
        }
        config.Pairing.PairIdHash = pair.PairIdHash;
        config.Pairing.PairEpoch = pair.PairEpoch;
        config.Pairing.PeerDeviceKeyId = peerDeviceKeyId;
        config.Pairing.PeerPublicKeyX = peerPublicKey.X;
        config.Pairing.PeerPublicKeyY = peerPublicKey.Y;
        config.Pairing.PeerName = peerName;
        config.Pairing.PeerWorld = peerWorld;
        config.Pairing.PeerTriggerPhrase = peerTriggerPhrase;
        config.Pairing.Paired = true;
        config.PendingRelayOperations.RemoveAll(o => o.Kind is "pair-invite" or "pair-accept");
        config.Save();
        PairingActivated?.Invoke();
    }

    /// Local-only disable, used by panic - never touches anything beyond this client's own config.
    public void EndPairingLocally()
    {
        config.Pairing.Paired = false;
        config.PendingRelayOperations.RemoveAll(o => o.Kind.StartsWith("pair-", StringComparison.Ordinal));
        config.Save();
        PairingEnded?.Invoke();
    }

    public void EndFromVerifiedPeerNotice()
    {
        if (!config.Pairing.IsPaired) return;
        config.Pairing.Paired = false;
        config.PendingRelayOperations.Clear();
        config.Save();
        PairingEnded?.Invoke();
        PairingActivated?.Invoke();
    }

    /// collar/pairing "User resets the device identity": ends any active pairing first (local teardown,
    /// then a best-effort revocation signed with the *old* identity - a revocation signed by the new key
    /// wouldn't match the deviceKeyId the peer or relay have on file for this pair) before the identity
    /// itself is replaced. Order matters: ReleasePeer must run before DeviceIdentityService.ResetIdentity.
    public async Task ResetDeviceIdentityAsync(CancellationToken ct)
    {
        var pairIdHash = config.Pairing.PairIdHash;
        var pairEpoch = config.Pairing.PairEpoch;
        ReleasePeer(publishRelayRevocation: false);
        if (pairIdHash is not null)
            await revocation.PublishBestEffortAsync(pairIdHash, pairEpoch, "identity-reset", ct).ConfigureAwait(false);

        // A retry signed by the retired identity cannot be authenticated after key replacement. The
        // initial delivery was attempted above; discard any failed old-key entry rather than retaining a
        // permanently unpublishable outbox item under the new identity.
        config.RevocationOutbox.RemoveAll(o => o.PairIdHash == pairIdHash && o.PairEpoch == pairEpoch);
        identity.ResetIdentity();
    }

    /// Owner-only manual release (see UI). Clears the captured peer identity entirely, unlike
    /// EndPairingLocally, which panic uses and which deliberately leaves PeerName/World cached. Local
    /// teardown (clearing config) completes fully before the best-effort revocation publish is even
    /// attempted, same ordering guarantee as PanicHandler.
    public void ReleasePeer(bool publishRelayRevocation = true)
    {
        var peerName = config.Pairing.PeerName;
        var peerWorld = config.Pairing.PeerWorld;
        var pairIdHash = config.Pairing.PairIdHash;
        var pairEpoch = config.Pairing.PairEpoch;

        config.Pairing.PeerName = null;
        config.Pairing.PeerWorld = null;
        config.Pairing.PeerDeviceKeyId = null;
        config.Pairing.PeerPublicKeyX = null;
        config.Pairing.PeerPublicKeyY = null;
        config.Pairing.PairIdHash = null;
        config.Pairing.Paired = false;
        config.Save();
        PairingEnded?.Invoke();

        if (!string.IsNullOrWhiteSpace(peerName) && !string.IsNullOrWhiteSpace(peerWorld))
        {
            try { sender.Send(composer.ComposeUnpairNotice(peerName, peerWorld)); }
            catch (Exception ex) { Plugin.Log.Warning(ex, "Could not send the peer unpair notification tell; relay revocation will still be attempted."); }
        }

        if (publishRelayRevocation && pairIdHash is not null)
            Plugin.FireAndForget(revocation.PublishBestEffortAsync(pairIdHash, pairEpoch, "unpair", CancellationToken.None));
    }

    private static string DescribeError(RelayException ex) => ex.Code switch
    {
        "not_configured" => "No relay endpoint is configured.",
        "network" => "Could not reach the relay - check your connection and try again.",
        "cooldown_active" => "Still cooling down - try again shortly.",
        "rate_limited" => "Too many attempts - try again shortly.",
        "expired" => "That invitation is no longer valid - create a fresh one.",
        "unauthorized" => "The relay rejected this request.",
        "service_unavailable" => "The relay is temporarily unavailable - try again shortly.",
        _ => "The relay request failed.",
    };
}
