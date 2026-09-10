using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.ImGuiNotification;
using Oathbound.Plugin.Commands;
using Oathbound.Plugin.Config;

namespace Oathbound.Plugin.Relay;

/// collar/catalog-sync: an actively paired Owner's request for a fresh encrypted catalog snapshot, end to
/// end. Owner and Sub sides live in the same class (like PairingService) since both installs run the same
/// binary and only Role decides which half ever actually runs.
public sealed class CatalogSyncRelayService
{
    private readonly PluginConfig config;
    private readonly RelayClient relay;
    private readonly DeviceIdentityService identity;
    private readonly ChatComposer composer;
    private readonly ChatSender sender;
    private readonly CatalogSyncService catalogSync;

    /// Owner-side only: the ephemeral ECDH private key for a request awaiting its response, keyed by
    /// requestId. Private material is deliberately memory-only; an interrupted request is detected and
    /// cleared on startup rather than persisted insecurely or resumed without its decryption key.
    private readonly Dictionary<string, RelayEcKeyPair> pendingOwnerRequests = new();

    public string? LastError { get; private set; }
    public event Action? LastErrorChanged;

    public bool RequestInFlight { get; private set; }
    public event Action? RequestInFlightChanged;
    public string Phase { get; private set; } = "Idle";

    /// Owner-side: unix seconds of the last import (successful or not, for display purposes) so Settings
    /// can show "last checked" distinctly from "last successful."
    public DateTimeOffset? LastAttemptAt { get; private set; }
    public CatalogSnapshotResult? LastImportResult { get; private set; }

    public CatalogSyncRelayService(PluginConfig config, RelayClient relay, DeviceIdentityService identity, ChatComposer composer, ChatSender sender, CatalogSyncService catalogSync)
    {
        this.config = config;
        this.relay = relay;
        this.identity = identity;
        this.composer = composer;
        this.sender = sender;
        this.catalogSync = catalogSync;

        if (config.PendingRelayOperations.RemoveAll(o => o.Kind == "catalog-request") > 0)
        {
            config.Save();
            LastError = "A catalog refresh was interrupted by a plugin restart. Request a fresh snapshot.";
        }
    }

    private void SetError(string? message)
    {
        LastError = message;
        LastErrorChanged?.Invoke();
    }

    private void SetInFlight(bool value)
    {
        RequestInFlight = value;
        RequestInFlightChanged?.Invoke();
    }

    private void SetPhase(string value)
    {
        Phase = value;
        RequestInFlightChanged?.Invoke();
    }

    public TimeSpan? CooldownRemaining(PairingState pairing)
    {
        var elapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - pairing.LastAcceptedCatalogSyncUnixSeconds;
        var remaining = RelayProtocolConstants.CatalogCooldownSeconds - elapsed;
        return remaining > 0 ? TimeSpan.FromSeconds(remaining) : null;
    }

    /// Owner-side, explicit UI action: creates a signed one-use catalog request and sends its reference in
    /// one lifecycle tell (task 6.2), addressed to the given pairing (the active pairing, in practice - see
    /// CollarWindow). Client-side cooldown/active-request checks are advisory only - the Worker's own atomic
    /// check is authoritative and is what actually prevents a bypass via clock changes.
    public async Task<bool> RequestRefreshAsync(PairingState pairing, CancellationToken ct)
    {
        if (!pairing.IsPaired)
        {
            SetError("Not paired.");
            return false;
        }
        if (CooldownRemaining(pairing) is { } remaining)
        {
            SetError($"Still cooling down - try again in {FormatRemaining(remaining)}.");
            return false;
        }
        if (RequestInFlight)
        {
            SetError("A request is already in flight.");
            return false;
        }

        SetInFlight(true);
        SetPhase("Creating secure request");
        var pollStarted = false;
        RelayEcKeyPair? unownedEphemeral = null;
        try
        {
            identity.EnsureIdentity();
            var ownerEphemeral = unownedEphemeral = RelayCrypto.GenerateEphemeralKeyPair();
            var requestId = RelayCrypto.RandomCapabilityId();
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var envelope = new CatalogRequestEnvelope
            {
                PairIdHash = pairing.PairIdHash!,
                PairEpoch = pairing.PairEpoch,
                RequestId = requestId,
                RequesterDeviceKeyId = identity.DeviceKeyId!,
                OwnerEphemeralPublicKey = RelayCrypto.ExportPublicKeyJwk(ownerEphemeral),
                CreatedAt = now,
                ExpiresAt = now + RelayProtocolConstants.CatalogRequestExpirySeconds,
            };
            envelope.Signature = RelayCrypto.SignRaw(identity.GetSigningKey(), EnvelopeCanonical.SerializeExcludingSignature(envelope));

            await relay.CreateCatalogRequestAsync(envelope, ct).ConfigureAwait(false);

            // Ownership of `ownerEphemeral` transfers into the pending map (not disposed here); it is
            // disposed wherever it is later removed from the map (success, failure, or expiry).
            pendingOwnerRequests[requestId] = ownerEphemeral;
            unownedEphemeral = null;
            config.PendingRelayOperations.RemoveAll(o => o.Kind == "catalog-request");
            config.PendingRelayOperations.Add(new PendingRelayOperationState { Kind = "catalog-request", OperationId = requestId, ExpiresAt = envelope.ExpiresAt });
            config.Save();

            var tell = composer.ComposeCatalogRequestNotice(pairing.PeerName!, pairing.PeerWorld!, requestId);
            sender.Send(tell);
            SetError(null);

            SetPhase("Waiting for Sub upload");
            _ = PollAndImportAsync(requestId, pairing, ct);
            pollStarted = true;
            return true;
        }
        catch (RelayException ex)
        {
            SetError(DescribeError(ex));
            return false;
        }
        finally
        {
            unownedEphemeral?.Dispose();
            if (!pollStarted)
            {
                SetPhase("Idle");
                SetInFlight(false);
            }
        }
    }

    /// Owner-side: bounded poll for the Sub's upload, then retrieve/decrypt/decompress/validate/commit.
    /// Never re-enables anything on failure; the prior imported snapshot is always left untouched unless
    /// every check here passes (task 6.4).
    private async Task PollAndImportAsync(string requestId, PairingState pairing, CancellationToken ct)
    {
        try
        {
            for (var attempt = 0; attempt < 60; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                CatalogRequestEnvelope status;
                try
                {
                    status = await relay.FetchCatalogRequestAsync(requestId, ct).ConfigureAwait(false);
                }
                catch (RelayException ex)
                {
                    SetError(DescribeError(ex));
                    return;
                }

                if (status.Status == "uploaded") break;
                if (status.Status is "consumed" or "expired") { SetError("The request expired or was already consumed."); return; }
                await Task.Delay(TimeSpan.FromSeconds(3), ct).ConfigureAwait(false);
                if (attempt == 59) { SetError("Timed out waiting for your Sub to respond."); return; }
            }

            if (!pendingOwnerRequests.TryGetValue(requestId, out var ownerEphemeral))
            {
                SetError("Lost track of this request's key material (plugin restarted mid-request?) - request again.");
                return;
            }

            var (envelope, ciphertext) = await relay.ConsumeCatalogResponseAsync(requestId, ct).ConfigureAwait(false);
            SetPhase("Decrypting and validating");
            LastAttemptAt = DateTimeOffset.UtcNow;

            if (!ImportSnapshot(envelope, ciphertext, ownerEphemeral, pairing, out var result, out var error))
            {
                SetError(error);
                LastImportResult = new CatalogSnapshotResult(0, 0, 0, 0, error);
                return;
            }

            pairing.LastAcceptedCatalogSyncUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            pairing.LastImportedSnapshotId = envelope.SnapshotId;
            config.Save();
            LastImportResult = result;
            SetPhase("Complete");
            SetError(null);
        }
        catch (RelayException ex)
        {
            SetError(DescribeError(ex));
        }
        catch (OperationCanceledException)
        {
            // Plugin shutting down - not an error to surface.
        }
        finally
        {
            if (pendingOwnerRequests.Remove(requestId, out var key)) key.Dispose();
            if (config.PendingRelayOperations.RemoveAll(o => o.Kind == "catalog-request" && o.OperationId == requestId) > 0)
                config.Save();
            SetInFlight(false);
            if (Phase != "Complete") SetPhase("Idle");
        }
    }

    /// All of the Owner-side authentication/decryption/validation task 6.4 requires, isolated so a failure
    /// anywhere here guarantees no partial state - `result`/`error` are mutually exclusive on return.
    private bool ImportSnapshot(CatalogResponseEnvelope envelope, byte[] ciphertext, RelayEcKeyPair ownerEphemeral, PairingState pairing, out CatalogSnapshotResult result, out string? error)
    {
        result = default;
        error = null;

        if (envelope.PairIdHash != pairing.PairIdHash || envelope.PairEpoch != pairing.PairEpoch)
        {
            error = "Snapshot addressed to a different pair/epoch - ignored.";
            return false;
        }
        if (envelope.RecipientDeviceKeyId != identity.DeviceKeyId || envelope.SenderDeviceKeyId != pairing.PeerDeviceKeyId)
        {
            error = "Snapshot sender/recipient device keys did not match this pairing - ignored.";
            return false;
        }
        if (envelope.SnapshotId <= pairing.LastImportedSnapshotId)
        {
            error = "Snapshot is not newer than the last one imported - ignored (stale or replayed).";
            return false;
        }
        if (envelope.CiphertextSizeBytes > RelayProtocolConstants.CatalogCiphertextMaxBytes || ciphertext.Length != envelope.CiphertextSizeBytes)
        {
            error = "Snapshot ciphertext size was invalid - ignored.";
            return false;
        }
        if (RelayCrypto.Sha256Hex(ciphertext) != envelope.CiphertextDigest)
        {
            error = "Snapshot ciphertext digest did not match - ignored (corrupt or tampered).";
            return false;
        }
        if (pairing.PeerPublicKeyX is null || pairing.PeerPublicKeyY is null)
        {
            error = "No peer public key on file - cannot verify this snapshot.";
            return false;
        }
        var peerPublicKey = new EcPublicKeyJwk { Kty = "EC", Crv = "P-256", X = pairing.PeerPublicKeyX, Y = pairing.PeerPublicKeyY };
        if (!RelayCrypto.VerifyRaw(peerPublicKey, envelope.Signature ?? "", EnvelopeCanonical.SerializeExcludingSignature(envelope)))
        {
            error = "Snapshot signature did not verify against the paired peer's key - ignored.";
            return false;
        }

        byte[] plaintext;
        try
        {
            using var subEphemeralPublic = RelayCrypto.ImportEphemeralPublicKey(envelope.SenderEphemeralPublicKey);
            var sharedSecret = RelayCrypto.DeriveSharedSecret(ownerEphemeral, subEphemeralPublic);
            var ownerRaw = RelayCrypto.ExportRawUncompressedPoint(ownerEphemeral);
            var subRaw = RelayCrypto.ExportRawUncompressedPoint(envelope.SenderEphemeralPublicKey);
            var combined = new byte[ownerRaw.Length + subRaw.Length];
            Buffer.BlockCopy(ownerRaw, 0, combined, 0, ownerRaw.Length);
            Buffer.BlockCopy(subRaw, 0, combined, ownerRaw.Length, subRaw.Length);
            var salt = SHA256.HashData(combined);
            var info = RelayCrypto.BuildCatalogHkdfInfo(envelope.PairIdHash, envelope.RequestId);
            var aesKey = RelayCrypto.DeriveAesKey(sharedSecret, salt, info);
            var nonce = RelayCrypto.Base64UrlDecode(envelope.Nonce);
            var aad = CatalogResponseAad.Build(envelope);
            var compressed = RelayCrypto.AesGcmDecrypt(aesKey, nonce, ciphertext, aad);
            plaintext = RelayCompression.Decompress(compressed, RelayProtocolConstants.CatalogPlaintextMaxBytes);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or InvalidDataException)
        {
            error = $"Snapshot failed to decrypt/decompress - ignored ({ex.GetType().Name}).";
            return false;
        }

        var exportText = System.Text.Encoding.UTF8.GetString(plaintext);
        var applyResult = catalogSync.ApplyRelaySnapshot(exportText, envelope.PairIdHash);
        if (applyResult.Error is not null)
        {
            error = applyResult.Error;
            return false;
        }
        result = applyResult;
        return true;
    }

    // ---- Sub side ----

    /// Sub-side: a `collarcatalogreq <requestId>` tell arrived from `senderName`@`senderWorld` (already
    /// verified by Dalamud's chat sender field). Everything here fails closed silently except the one
    /// explicit case the spec calls out (permission not enabled), which gets its own notice tell so the
    /// Owner isn't left guessing (task 6.1/6.3).
    public async Task HandleCatalogRequestTellAsync(string requestId, string senderName, string senderWorld, CancellationToken ct)
    {
        // collar/multi-pairing: resolved against every pairing where this device is the Sub-side, not a
        // single configured peer - honored using that pairing's own state, independent of which pairing is
        // active. Direction-specific on purpose: a mutual pair has two pairings with this same sender, and
        // only the Sub-side one is a valid source for a catalog *request* (the Owner-side one is this
        // device requesting catalog *from* them, a different flow entirely).
        var pairing = config.FindPairing(senderName, senderWorld, PairingDirection.SubSide);
        if (pairing is null) return;

        CatalogRequestEnvelope request;
        try
        {
            request = await relay.FetchCatalogRequestAsync(requestId, ct).ConfigureAwait(false);
        }
        catch (RelayException)
        {
            return;
        }

        if (request.PairIdHash != pairing.PairIdHash || request.PairEpoch != pairing.PairEpoch) return;
        if (request.RequesterDeviceKeyId != pairing.PeerDeviceKeyId) return; // Not from the actual paired Owner's device.
        if (pairing.PeerPublicKeyX is null || pairing.PeerPublicKeyY is null) return;
        var peerPublicKey = new EcPublicKeyJwk { Kty = "EC", Crv = "P-256", X = pairing.PeerPublicKeyX, Y = pairing.PeerPublicKeyY };
        if (!RelayCrypto.VerifyRaw(peerPublicKey, request.Signature ?? "", EnvelopeCanonical.SerializeExcludingSignature(request))) return;

        if (!config.Permissions.RelayCatalogSync)
        {
            sender.Send(composer.ComposeCatalogPermissionDenied(senderName, senderWorld, requestId));
            return;
        }

        try
        {
            identity.EnsureIdentity();
            if (!catalogSync.TryBuildBoundedExport(out var exportText, out var exportError))
            {
                Plugin.Log.Warning(exportError ?? "Catalog snapshot exceeded a local size limit.");
                return;
            }
            var plaintext = System.Text.Encoding.UTF8.GetBytes(exportText);
            var compressed = RelayCompression.Compress(plaintext);
            if (compressed.Length > RelayProtocolConstants.CatalogCiphertextMaxBytes)
            {
                Plugin.Log.Warning("Catalog snapshot too large to upload even compressed; request left unanswered.");
                return;
            }

            using var subEphemeral = RelayCrypto.GenerateEphemeralKeyPair();
            using var ownerEphemeralPublic = RelayCrypto.ImportEphemeralPublicKey(request.OwnerEphemeralPublicKey);
            var sharedSecret = RelayCrypto.DeriveSharedSecret(subEphemeral, ownerEphemeralPublic);
            var ownerRaw = RelayCrypto.ExportRawUncompressedPoint(request.OwnerEphemeralPublicKey);
            var subRaw = RelayCrypto.ExportRawUncompressedPoint(subEphemeral);
            var combined = new byte[ownerRaw.Length + subRaw.Length];
            Buffer.BlockCopy(ownerRaw, 0, combined, 0, ownerRaw.Length);
            Buffer.BlockCopy(subRaw, 0, combined, ownerRaw.Length, subRaw.Length);
            var salt = SHA256.HashData(combined);
            var info = RelayCrypto.BuildCatalogHkdfInfo(request.PairIdHash, requestId);
            var aesKey = RelayCrypto.DeriveAesKey(sharedSecret, salt, info);
            var nonceBytes = RelayCrypto.RandomBytes(RelayCrypto.AeadNonceLengthBytes);

            var snapshotId = ++pairing.NextOutgoingSnapshotId;
            config.Save();

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var envelope = new CatalogResponseEnvelope
            {
                PairIdHash = request.PairIdHash,
                PairEpoch = request.PairEpoch,
                RequestId = requestId,
                SnapshotId = snapshotId,
                SenderDeviceKeyId = identity.DeviceKeyId!,
                RecipientDeviceKeyId = request.RequesterDeviceKeyId,
                CreatedAt = now,
                ExpiresAt = now + RelayProtocolConstants.CatalogObjectExpirySeconds,
                CiphertextSizeBytes = 0,
                Nonce = RelayCrypto.Base64UrlEncode(nonceBytes),
                SenderEphemeralPublicKey = RelayCrypto.ExportPublicKeyJwk(subEphemeral),
            };
            var aad = CatalogResponseAad.Build(envelope);
            var ciphertext = RelayCrypto.AesGcmEncrypt(aesKey, nonceBytes, compressed, aad);
            if (ciphertext.Length > RelayProtocolConstants.CatalogCiphertextMaxBytes)
            {
                Plugin.Log.Warning("Catalog snapshot exceeds the encrypted upload limit; request left unanswered.");
                return;
            }
            envelope.CiphertextSizeBytes = ciphertext.Length;
            envelope.CiphertextDigest = RelayCrypto.Sha256Hex(ciphertext);
            envelope.Signature = RelayCrypto.SignRaw(identity.GetSigningKey(), EnvelopeCanonical.SerializeExcludingSignature(envelope));

            await relay.UploadCatalogResponseAsync(requestId, envelope, ciphertext, ct).ConfigureAwait(false);

            // The Sub side otherwise has no way to know this ever happened - everything up to here is
            // silent by design (fails closed with no feedback on any rejection), but a successful upload
            // is worth a transient, self-dismissing notice rather than nothing at all.
            Plugin.NotificationManager.AddNotification(new Notification
            {
                Title = "Oathbound",
                Content = $"Sent an updated catalog to {senderName}.",
                Type = NotificationType.Success,
                InitialDuration = TimeSpan.FromSeconds(5),
            });

            // Clearing the sensitive buffers as soon as they've served their purpose - task 6.3 "clearing
            // sensitive buffers". These are managed byte[]s (no unmanaged memory to free), so this is a
            // best-effort scrub, not a hard guarantee against a GC copy lingering.
            Array.Clear(compressed);
            Array.Clear(ciphertext);
        }
        catch (RelayException ex)
        {
            Plugin.Log.Information($"Catalog snapshot upload failed: {DescribeError(ex)}");
        }
    }

    /// Sub-side: a `collarcatalogdenied <requestId>` tell (permission not enabled on the Sub's side).
    public void HandlePermissionDeniedTell(string requestId)
    {
        if (pendingOwnerRequests.Remove(requestId, out var key)) key.Dispose();
        SetError("Your Sub has not enabled catalog synchronization.");
    }

    private static string FormatRemaining(TimeSpan span) => span.TotalHours >= 1 ? $"{span.Hours}h {span.Minutes}m" : $"{span.Minutes}m {span.Seconds}s";

    private static string DescribeError(RelayException ex) => ex.Code switch
    {
        "not_configured" => "No relay endpoint is configured.",
        "network" => "Could not reach the relay - check your connection and try again.",
        "cooldown_active" => "Still cooling down - try again shortly.",
        "rate_limited" => "Too many attempts - try again shortly.",
        "expired" => "That request is no longer valid.",
        "unauthorized" => "The relay rejected this request.",
        "service_unavailable" => "The relay is temporarily unavailable - try again shortly.",
        _ => "The relay request failed.",
    };
}
