using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Oathbound.Plugin.Config;

namespace Oathbound.Plugin.Relay;

/// collar/pairing "Unpair and panic publish authenticated revocation" and "Pairing" 's replay/epoch
/// isolation requirements. This class only ever publishes/checks signed revocations and maintains the
/// retry outbox - it never performs local teardown itself (PanicHandler/PairingService already did that,
/// synchronously, before this is ever called) and never re-enables a pairing.
public sealed class RevocationService
{
    public event Action? PairingRevoked;
    private readonly PluginConfig config;
    private readonly RelayClient relay;
    private readonly DeviceIdentityService identity;

    public RevocationService(PluginConfig config, RelayClient relay, DeviceIdentityService identity)
    {
        this.config = config;
        this.relay = relay;
        this.identity = identity;
    }

    /// Best-effort publish of a revocation for the pair identified by the given snapshot (captured by the
    /// caller *before* it cleared config.Pairing, since EndPairingLocally/ReleasePeer only flip flags, not
    /// erase PairIdHash - see PanicHandler). On any failure, queues a retry entry rather than throwing;
    /// callers should treat this as fire-and-forget.
    public async Task PublishBestEffortAsync(string pairIdHash, int pairEpoch, string reason, CancellationToken ct)
    {
        var sequence = config.Pairing.OutgoingRevocationSequence + 1;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var envelope = new RevocationEnvelope
        {
            PairIdHash = pairIdHash,
            PairEpoch = pairEpoch,
            Sequence = sequence,
            Reason = reason,
            IssuedByDeviceKeyId = identity.DeviceKeyId ?? "",
            CreatedAt = now,
            ExpiresAt = now + 604800,
        };
        envelope.Signature = RelayCrypto.SignRaw(identity.GetSigningKey(), EnvelopeCanonical.SerializeExcludingSignature(envelope));

        // The sequence is reserved locally (and persisted) whether or not the publish itself succeeds, so a
        // retried attempt never reuses a sequence number a peer might already be tracking as consumed.
        config.Pairing.OutgoingRevocationSequence = sequence;
        SetDeliveryStatus("pending");
        config.Save();

        try
        {
            await relay.PublishRevocationAsync(envelope, ct).ConfigureAwait(false);
            SetDeliveryStatus("delivered");
            config.Save();
        }
        catch (RelayException)
        {
            config.RevocationOutbox.Add(new RevocationRetryEntry
            {
                PairIdHash = pairIdHash,
                PairEpoch = pairEpoch,
                Sequence = sequence,
                Reason = reason,
                CreatedAt = envelope.CreatedAt,
                ExpiresAt = envelope.ExpiresAt,
                Signature = envelope.Signature!,
                Attempt = 0,
                NextAttemptAtUnixSeconds = now + 30,
            });
            SetDeliveryStatus("pending");
            config.Save();
        }
    }

    /// Codes a retry can never succeed by simply trying again - the request itself is wrong (signature
    /// no longer matches on-file state, malformed, or the relay has permanently rejected it) rather than
    /// the relay being temporarily unavailable. Task 3.4 "no retry of permanent failures".
    private static readonly HashSet<string> PermanentFailureCodes = ["unauthorized", "invalid_request", "payload_too_large"];

    /// Called periodically (see Plugin.OnFrameworkUpdate, throttled) to retry anything still pending.
    /// Honors the relay's own Retry-After when it gives one; otherwise backs off exponentially with jitter
    /// (task 3.4 "jittered exponential backoff"). An entry past its ExpiresAt, or one the relay has
    /// permanently rejected, is dropped with a visible warning logged - it never restores pairing and never
    /// blocks anything else.
    public async Task RetryOutboxAsync(CancellationToken ct)
    {
        if (config.RevocationOutbox.Count == 0) return;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        foreach (var entry in config.RevocationOutbox.ToArray())
        {
            if (now >= entry.ExpiresAt)
            {
                Plugin.Log.Warning($"Revocation retry for pair {entry.PairIdHash} (sequence {entry.Sequence}) expired without confirmed delivery.");
                config.RevocationOutbox.Remove(entry);
                SetDeliveryStatus("expired");
                config.Save();
                continue;
            }
            if (now < entry.NextAttemptAtUnixSeconds) continue;

            var envelope = new RevocationEnvelope
            {
                PairIdHash = entry.PairIdHash,
                PairEpoch = entry.PairEpoch,
                Sequence = entry.Sequence,
                Reason = entry.Reason,
                IssuedByDeviceKeyId = identity.DeviceKeyId ?? "",
                CreatedAt = entry.CreatedAt,
                ExpiresAt = entry.ExpiresAt,
                Signature = entry.Signature,
            };

            try
            {
                await relay.PublishRevocationAsync(envelope, ct).ConfigureAwait(false);
                config.RevocationOutbox.Remove(entry);
                SetDeliveryStatus("delivered");
                config.Save();
            }
            catch (RelayException ex) when (PermanentFailureCodes.Contains(ex.Code))
            {
                Plugin.Log.Warning($"Revocation retry for pair {entry.PairIdHash} (sequence {entry.Sequence}) permanently rejected ({ex.Code}); giving up.");
                config.RevocationOutbox.Remove(entry);
                SetDeliveryStatus("failed");
                config.Save();
            }
            catch (RelayException ex)
            {
                entry.Attempt++;
                var backoffSeconds = ex.RetryAfterSeconds ?? Math.Min(30 * (1 << Math.Min(entry.Attempt, 8)), 3600) + Random.Shared.Next(0, 15);
                entry.NextAttemptAtUnixSeconds = now + backoffSeconds;
                config.Save();
            }
        }
    }

    private void SetDeliveryStatus(string status)
    {
        config.Pairing.LastRevocationDeliveryStatus = status;
        config.Pairing.LastRevocationDeliveryUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    /// collar/pairing "Peer missed the notification tell" / "Old revocation is replayed after re-pairing".
    /// Called at login and on a low-frequency bounded schedule (Plugin wires the interval). Only ever ends
    /// pairing locally; never executes any other command a peer's revocation might (in principle) try to
    /// smuggle in - there is nothing else to execute, the schema has no room for it.
    public async Task CheckForMissedRevocationAsync(CancellationToken ct)
    {
        var pairing = config.Pairing;
        if (!pairing.IsPaired || pairing.PairIdHash is null || pairing.PeerPublicKeyX is null || pairing.PeerPublicKeyY is null)
            return;

        config.Pairing.LastRevocationCheckUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        config.Save();

        RevocationEnvelope[] revocations;
        try
        {
            revocations = await relay.CheckRevocationsAsync(pairing.PairIdHash, pairing.IncomingRevocationSequence, ct).ConfigureAwait(false);
        }
        catch (RelayException ex)
        {
            Plugin.Log.Information($"Revocation check skipped: {ex.Code}.");
            return;
        }

        var peerPublicKey = new EcPublicKeyJwk { Kty = "EC", Crv = "P-256", X = pairing.PeerPublicKeyX, Y = pairing.PeerPublicKeyY };

        foreach (var revocation in revocations)
        {
            if (!ApplyIfValid(revocation, peerPublicKey))
                return; // Once pairing has ended locally, later entries in this batch (if any) no longer apply.
        }
    }

    /// Returns true if pairing is still active after processing this revocation (false means it just ended).
    /// Task 5.4: rejects wrong-device, wrong-pair, stale-sequence, expired, and old-epoch revocations.
    private bool ApplyIfValid(RevocationEnvelope revocation, EcPublicKeyJwk peerPublicKey)
    {
        var pairing = config.Pairing;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (revocation.PairIdHash != pairing.PairIdHash) return true;
        if (revocation.PairEpoch < pairing.PairEpoch) return true; // Old epoch - a prior pairing's stale notice.
        if (revocation.Sequence <= pairing.IncomingRevocationSequence) return true; // Replay.
        if (revocation.ExpiresAt <= now) return true;
        if (revocation.IssuedByDeviceKeyId != pairing.PeerDeviceKeyId) return true; // Wrong device.
        if (!RelayCrypto.VerifyRaw(peerPublicKey, revocation.Signature ?? "", EnvelopeCanonical.SerializeExcludingSignature(revocation)))
            return true;

        pairing.IncomingRevocationSequence = revocation.Sequence;
        pairing.Paired = false;
        PairingRevoked?.Invoke();
        config.Save();
        Plugin.Log.Information($"Pairing ended locally: a valid signed revocation (sequence {revocation.Sequence}, reason \"{revocation.Reason}\") was observed from the paired peer.");
        return false;
    }
}
