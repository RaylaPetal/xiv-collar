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

/// What one Sub-side publish attempt came to - drives the scheduler's retry timing (CatalogAutoSync).
public enum MailboxPublishOutcome
{
    /// Uploaded (or confirmed already delivered, nothing to do).
    Published,
    /// Nothing to publish to yet: the Owner hasn't published a receive key (older Owner plugin, or not
    /// checked in since pairing), or this device's catalog sync permission is off. Retried on the next
    /// change or hourly pass, never surfaced as an error.
    NotReady,
    /// The relay said to wait (per-pair minimum interval / quota) - retry after RetryAfterSeconds.
    RateLimited,
    /// Anything else (network, relay error, verification failure, too large) - retried on the hourly pass.
    Failed,
}

public readonly record struct MailboxPublishResult(MailboxPublishOutcome Outcome, int RetryAfterSeconds = 0);

/// collar/catalog-sync automatic sync over the per-pair relay mailbox (see the change's design.md): the Sub
/// pushes an encrypted snapshot whenever its catalog changes, the Owner collects it on its own hourly
/// schedule. Neither side ever sends a chat message on this path. Timing (debounce, hourly schedules,
/// login) lives in CatalogAutoSync; this class is only the relay/crypto work for one pairing at a time.
public sealed class CatalogMailboxService
{
    private static readonly byte[] ReceiveKeyEntropy = "oathbound-mailbox-receive-key-v1"u8.ToArray();

    private readonly PluginConfig config;
    private readonly RelayClient relay;
    private readonly DeviceIdentityService identity;
    private readonly CatalogSyncService catalogSync;

    private readonly object gate = new();
    private readonly HashSet<Guid> checksInFlight = new();
    private readonly HashSet<Guid> importing = new();
    private readonly HashSet<Guid> publishesInFlight = new();

    public CatalogMailboxService(PluginConfig config, RelayClient relay, DeviceIdentityService identity, CatalogSyncService catalogSync)
    {
        this.config = config;
        this.relay = relay;
        this.identity = identity;
        this.catalogSync = catalogSync;
    }

    /// Owner-side UI state: a newer snapshot was found and is being retrieved/imported right now.
    public bool IsSyncing(Guid pairingId) { lock (gate) return importing.Contains(pairingId); }

    public bool IsChecking(Guid pairingId) { lock (gate) return checksInFlight.Contains(pairingId); }

    // ---- Sub side ----

    /// Publishes `exportText` (already built on the framework thread, digest `digest`) to this Sub-side
    /// pairing's mailbox, if it isn't already there. `force` skips the "digest unchanged" shortcut - used by
    /// the hourly delivery check, which only asks the relay whether the last push actually arrived.
    public async Task<MailboxPublishResult> PublishAsync(PairingState pairing, string exportText, string digest, CancellationToken ct)
    {
        if (pairing is not { Direction: PairingDirection.SubSide, IsPaired: true, PairIdHash: { Length: > 0 } pairIdHash } ||
            pairing.PeerDeviceKeyId is null || pairing.PeerPublicKeyX is null || pairing.PeerPublicKeyY is null)
            return new(MailboxPublishOutcome.NotReady);
        // collar/catalog-sync "Catalog sync permission is off": nothing leaves this device.
        if (!config.Permissions.RelayCatalogSync)
            return new(MailboxPublishOutcome.NotReady);

        lock (gate)
            if (!publishesInFlight.Add(pairing.Id))
                return new(MailboxPublishOutcome.NotReady);
        try
        {
            identity.EnsureIdentity();
            var peerPublicKey = new EcPublicKeyJwk { Kty = "EC", Crv = "P-256", X = pairing.PeerPublicKeyX, Y = pairing.PeerPublicKeyY };

            // Two passes at most: the second only if the Owner rotated its key between our fetch and upload.
            for (var attempt = 0; attempt < 2; attempt++)
            {
                CatalogMailboxKeyInfo info;
                try
                {
                    info = await relay.FetchMailboxKeyAsync(pairIdHash, pairing.PairEpoch, ct).ConfigureAwait(false);
                }
                catch (RelayException ex) when (ex.Code == "not_found")
                {
                    return new(MailboxPublishOutcome.NotReady); // Owner hasn't published a receive key yet.
                }

                var key = info.Key;
                if (key.PairIdHash != pairIdHash || key.PairEpoch != pairing.PairEpoch || key.OwnerDeviceKeyId != pairing.PeerDeviceKeyId ||
                    !RelayCrypto.VerifyRaw(peerPublicKey, key.Signature ?? "", EnvelopeCanonical.SerializeExcludingSignature(key)))
                {
                    // collar/catalog-sync "Forged receive key": never encrypt to a key the paired Owner didn't sign.
                    Plugin.Log.Warning($"Catalog mailbox for {pairing.PeerName}: receive key did not verify against the paired Owner - not publishing.");
                    return new(MailboxPublishOutcome.Failed);
                }

                // Already delivered? Same catalog as last time, and the relay confirms that push is waiting or
                // was consumed - nothing to do. Otherwise (changed, or the push never arrived) publish.
                var delivered = pairing.LastPublishedMailboxSnapshotId > 0 &&
                    (info.WaitingSnapshotId >= pairing.LastPublishedMailboxSnapshotId || info.LastConsumedSnapshotId >= pairing.LastPublishedMailboxSnapshotId);
                if (digest == pairing.LastPublishedCatalogDigest && delivered)
                    return new(MailboxPublishOutcome.Published);

                try
                {
                    await EncryptAndUploadAsync(pairing, key, exportText, ct).ConfigureAwait(false);
                }
                catch (RelayException ex) when (ex.Code == "expired" && attempt == 0)
                {
                    continue; // Rotated under us - fetch the new key and publish again.
                }

                pairing.LastPublishedCatalogDigest = digest;
                pairing.LastPublishedCatalogUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                config.Save();
                return new(MailboxPublishOutcome.Published);
            }
            return new(MailboxPublishOutcome.Failed);
        }
        catch (RelayException ex) when (ex.Code is "rate_limited" or "cooldown_active")
        {
            return new(MailboxPublishOutcome.RateLimited, Math.Max(ex.RetryAfterSeconds ?? 60, 1));
        }
        catch (RelayException ex)
        {
            Plugin.Log.Information($"Catalog mailbox publish for {pairing.PeerName} failed: {ex.Code}.");
            return new(MailboxPublishOutcome.Failed);
        }
        catch (InvalidDataException ex)
        {
            Plugin.Log.Warning(ex.Message);
            return new(MailboxPublishOutcome.Failed);
        }
        finally
        {
            lock (gate) publishesInFlight.Remove(pairing.Id);
        }
    }

    private async Task EncryptAndUploadAsync(PairingState pairing, CatalogMailboxKeyEnvelope key, string exportText, CancellationToken ct)
    {
        var plaintext = System.Text.Encoding.UTF8.GetBytes(exportText);
        if (plaintext.Length > RelayProtocolConstants.CatalogPlaintextMaxBytes)
            throw new InvalidDataException("Catalog exceeds the local plaintext limit and was not published.");
        var compressed = RelayCompression.Compress(plaintext);
        byte[]? ciphertext = null;
        try
        {
            using var subEphemeral = RelayCrypto.GenerateEphemeralKeyPair();
            using var ownerReceivePublic = RelayCrypto.ImportEphemeralPublicKey(key.ReceivePublicKey);
            var sharedSecret = RelayCrypto.DeriveSharedSecret(subEphemeral, ownerReceivePublic);
            var salt = SHA256.HashData([.. RelayCrypto.ExportRawUncompressedPoint(key.ReceivePublicKey), .. RelayCrypto.ExportRawUncompressedPoint(subEphemeral)]);
            var aesKey = RelayCrypto.DeriveAesKey(sharedSecret, salt, RelayCrypto.BuildCatalogPushHkdfInfo(key.PairIdHash, key.ReceiveKeyId));
            var nonceBytes = RelayCrypto.RandomBytes(RelayCrypto.AeadNonceLengthBytes);

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var envelope = new CatalogPushEnvelope
            {
                PairIdHash = key.PairIdHash,
                PairEpoch = key.PairEpoch,
                ReceiveKeyId = key.ReceiveKeyId,
                SenderDeviceKeyId = identity.DeviceKeyId!,
                RecipientDeviceKeyId = key.OwnerDeviceKeyId,
                CreatedAt = now,
                ExpiresAt = now + RelayProtocolConstants.CatalogMailboxExpirySeconds,
                Nonce = RelayCrypto.Base64UrlEncode(nonceBytes),
                SenderEphemeralPublicKey = RelayCrypto.ExportPublicKeyJwk(subEphemeral),
            };

            // Size is checked before a snapshot id is spent, so a too-large catalog never burns through the
            // pair's monotonic sequence on each retry. GCM output is always input + 16-byte tag, so this is
            // exact without encrypting first (and each key/nonce pair is only ever used for one encryption).
            if (compressed.Length + 16 > RelayProtocolConstants.CatalogCiphertextMaxBytes)
                throw new InvalidDataException("Catalog exceeds the encrypted upload limit and was not published.");

            // The id is bound into the AAD, so it's settled before the one and only encryption.
            envelope.SnapshotId = ++pairing.NextOutgoingSnapshotId;
            config.Save();
            ciphertext = RelayCrypto.AesGcmEncrypt(aesKey, nonceBytes, compressed, CatalogPushAad.Build(envelope));
            envelope.CiphertextSizeBytes = ciphertext.Length;
            envelope.CiphertextDigest = RelayCrypto.Sha256Hex(ciphertext);
            envelope.Signature = RelayCrypto.SignRaw(identity.GetSigningKey(), EnvelopeCanonical.SerializeExcludingSignature(envelope));

            await relay.UploadMailboxSnapshotAsync(envelope, ciphertext, ct).ConfigureAwait(false);
            pairing.LastPublishedMailboxSnapshotId = envelope.SnapshotId;
        }
        finally
        {
            // Best-effort scrub, same caveat as the request flow's upload.
            Array.Clear(compressed);
            if (ciphertext is not null) Array.Clear(ciphertext);
        }
    }

    // ---- Owner side ----

    /// One Owner-side mailbox check for `pairing`: make sure a receive key is published, look at what's
    /// waiting, and if it's newer than what's imported, retrieve/verify/decrypt/import it and rotate the key.
    /// Records the outcome on the pairing for the Sync tab's up-to-date indicator. Never throws.
    public async Task CheckAsync(PairingState pairing, CancellationToken ct)
    {
        if (pairing is not { Direction: PairingDirection.OwnerSide, IsPaired: true, PairIdHash: { Length: > 0 } pairIdHash })
            return;
        lock (gate)
            if (!checksInFlight.Add(pairing.Id))
                return;
        try
        {
            identity.EnsureIdentity();

            CatalogMailboxStatus status;
            try
            {
                status = await relay.FetchMailboxStatusAsync(pairIdHash, pairing.PairEpoch, ct).ConfigureAwait(false);
            }
            catch (RelayException ex) when (ex.Code == "not_found")
            {
                RecordCheckFailure(pairing, "The relay doesn't support automatic catalog sync yet - use Request refresh.");
                return;
            }

            // First check after upgrading/pairing, a new pair epoch, or a local key we can no longer use: publish
            // a fresh key. (Replacing it discards anything encrypted to the old one; the Sub's delivery check
            // republishes that within the hour.)
            if (!status.HasKey || status.ReceiveKeyId != pairing.MailboxReceiveKeyId || !HasUsableReceiveKey(pairing))
            {
                await PublishFreshKeyAsync(pairing, ct).ConfigureAwait(false);
                pairing.SubLastPublishedUnixSeconds = status.LastUploadAt;
                pairing.LastMailboxCheckOkUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                pairing.LastMailboxCheckError = status.HasSnapshot && status.SnapshotId > pairing.LastImportedSnapshotId
                    ? "A newer catalog was waiting but couldn't be read with this device's key - your Sub's plugin will send it again within the hour."
                    : null;
                config.Save();
                return;
            }

            pairing.SubLastPublishedUnixSeconds = status.LastUploadAt;
            string? importError = null;
            if (status is { HasSnapshot: true, SnapshotId: { } snapshotId } && snapshotId > pairing.LastImportedSnapshotId)
                importError = await RetrieveAndImportAsync(pairing, snapshotId, ct).ConfigureAwait(false);

            pairing.LastMailboxCheckOkUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            pairing.LastMailboxCheckError = importError;
            config.Save();
        }
        catch (RelayException ex)
        {
            RecordCheckFailure(pairing, DescribeError(ex));
        }
        catch (OperationCanceledException)
        {
            // Logout / shutdown - not a failure worth recording.
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Catalog mailbox check failed unexpectedly.");
            RecordCheckFailure(pairing, "The automatic catalog check failed unexpectedly - see /xllog.");
        }
        finally
        {
            lock (gate)
            {
                checksInFlight.Remove(pairing.Id);
                importing.Remove(pairing.Id);
            }
        }
    }

    /// Returns null on success, or the reason the snapshot was not imported (the prior catalog is untouched).
    private async Task<string?> RetrieveAndImportAsync(PairingState pairing, int snapshotId, CancellationToken ct)
    {
        lock (gate) importing.Add(pairing.Id);

        if (!TryLoadReceiveKey(pairing, out var receiveKey))
            return "This device's mailbox key couldn't be read.";
        using var ownerReceive = receiveKey;
        var usedKeyId = pairing.MailboxReceiveKeyId!;

        // Rotation is atomic with the pickup on the relay; persist the next key the moment the relay has it,
        // before any local verification, so a bad snapshot can't leave this device holding a stale key.
        using var nextKey = RelayCrypto.GenerateEphemeralKeyPair();
        var nextEnvelope = BuildSignedKeyEnvelope(pairing, nextKey, RelayCrypto.RandomReceiveKeyId());
        var (envelope, ciphertext) = await relay.ConsumeMailboxSnapshotAsync(pairing.PairIdHash!, pairing.PairEpoch, snapshotId, nextEnvelope, ct).ConfigureAwait(false);
        StoreReceiveKey(pairing, nextEnvelope, nextKey);
        config.Save();

        if (!TryDecryptPush(pairing, envelope, ciphertext, ownerReceive, usedKeyId, out var exportText, out var error))
            return error;

        // Imports touch the same quick-command lists the UI draws from, so apply on the framework thread.
        var result = await Plugin.Framework.RunOnFrameworkThread(() => catalogSync.ApplyRelaySnapshot(exportText!, envelope.PairIdHash)).ConfigureAwait(false);
        if (result.Error is not null)
            return result.Error;

        pairing.LastImportedSnapshotId = envelope.SnapshotId;
        pairing.LastAcceptedCatalogSyncUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        config.Save();
        LastAutoImport = (pairing.Id, result);
        if (result.Added + result.Updated + result.Removed > 0)
        {
            Plugin.NotificationManager.AddNotification(new Notification
            {
                Title = "Oathbound",
                Content = $"Synced {pairing.PeerName}'s catalog: {result.Added} added, {result.Updated} updated, {result.Removed} removed.",
                Type = NotificationType.Success,
                InitialDuration = TimeSpan.FromSeconds(6),
            });
        }
        return null;
    }

    /// The most recent successful automatic import's counts, for the Sync tab.
    public (Guid PairingId, CatalogSnapshotResult Result)? LastAutoImport { get; private set; }

    /// Every check collar/catalog-sync's "Owner checks the mailbox about hourly and imports automatically"
    /// lists, mirroring the request flow's ImportSnapshot, before a single byte is decrypted into the catalog.
    private bool TryDecryptPush(PairingState pairing, CatalogPushEnvelope envelope, byte[] ciphertext, RelayEcKeyPair ownerReceive, string usedKeyId, out string? exportText, out string? error)
    {
        exportText = null;
        error = null;
        if (envelope.PairIdHash != pairing.PairIdHash || envelope.PairEpoch != pairing.PairEpoch)
            error = "Snapshot addressed to a different pair/epoch - ignored.";
        else if (envelope.RecipientDeviceKeyId != identity.DeviceKeyId || envelope.SenderDeviceKeyId != pairing.PeerDeviceKeyId)
            error = "Snapshot sender/recipient device keys did not match this pairing - ignored.";
        else if (envelope.ReceiveKeyId != usedKeyId)
            error = "Snapshot was encrypted to a different mailbox key - ignored.";
        else if (envelope.SnapshotId <= pairing.LastImportedSnapshotId)
            error = "Snapshot is not newer than the last one imported - ignored (stale or replayed).";
        else if (envelope.CiphertextSizeBytes > RelayProtocolConstants.CatalogCiphertextMaxBytes || ciphertext.Length != envelope.CiphertextSizeBytes)
            error = "Snapshot ciphertext size was invalid - ignored.";
        else if (RelayCrypto.Sha256Hex(ciphertext) != envelope.CiphertextDigest)
            error = "Snapshot ciphertext digest did not match - ignored (corrupt or tampered).";
        else if (pairing.PeerPublicKeyX is null || pairing.PeerPublicKeyY is null)
            error = "No peer public key on file - cannot verify this snapshot.";
        else if (!RelayCrypto.VerifyRaw(new EcPublicKeyJwk { Kty = "EC", Crv = "P-256", X = pairing.PeerPublicKeyX, Y = pairing.PeerPublicKeyY },
                     envelope.Signature ?? "", EnvelopeCanonical.SerializeExcludingSignature(envelope)))
            error = "Snapshot signature did not verify against the paired Sub's key - ignored.";
        if (error is not null)
            return false;

        try
        {
            using var subEphemeralPublic = RelayCrypto.ImportEphemeralPublicKey(envelope.SenderEphemeralPublicKey);
            var sharedSecret = RelayCrypto.DeriveSharedSecret(ownerReceive, subEphemeralPublic);
            var salt = SHA256.HashData([.. RelayCrypto.ExportRawUncompressedPoint(ownerReceive), .. RelayCrypto.ExportRawUncompressedPoint(envelope.SenderEphemeralPublicKey)]);
            var aesKey = RelayCrypto.DeriveAesKey(sharedSecret, salt, RelayCrypto.BuildCatalogPushHkdfInfo(envelope.PairIdHash, envelope.ReceiveKeyId));
            var compressed = RelayCrypto.AesGcmDecrypt(aesKey, RelayCrypto.Base64UrlDecode(envelope.Nonce), ciphertext, CatalogPushAad.Build(envelope));
            exportText = System.Text.Encoding.UTF8.GetString(RelayCompression.Decompress(compressed, RelayProtocolConstants.CatalogPlaintextMaxBytes));
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or InvalidDataException)
        {
            error = $"Snapshot failed to decrypt/decompress - ignored ({ex.GetType().Name}).";
            return false;
        }
    }

    private async Task PublishFreshKeyAsync(PairingState pairing, CancellationToken ct)
    {
        using var key = RelayCrypto.GenerateEphemeralKeyPair();
        var envelope = BuildSignedKeyEnvelope(pairing, key, RelayCrypto.RandomReceiveKeyId());
        await relay.PublishMailboxKeyAsync(envelope, ct).ConfigureAwait(false);
        StoreReceiveKey(pairing, envelope, key);
        config.Save();
    }

    private CatalogMailboxKeyEnvelope BuildSignedKeyEnvelope(PairingState pairing, RelayEcKeyPair key, string receiveKeyId)
    {
        var envelope = new CatalogMailboxKeyEnvelope
        {
            PairIdHash = pairing.PairIdHash!,
            PairEpoch = pairing.PairEpoch,
            ReceiveKeyId = receiveKeyId,
            OwnerDeviceKeyId = identity.DeviceKeyId!,
            ReceivePublicKey = RelayCrypto.ExportPublicKeyJwk(key),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
        envelope.Signature = RelayCrypto.SignRaw(identity.GetSigningKey(), EnvelopeCanonical.SerializeExcludingSignature(envelope));
        return envelope;
    }

    /// Replaces the pairing's receive key - the previous private scalar is overwritten, so it's gone for good.
    private static void StoreReceiveKey(PairingState pairing, CatalogMailboxKeyEnvelope envelope, RelayEcKeyPair key)
    {
        var privateD = RelayCrypto.ExportPrivateD(key);
        var (protectedD, wasProtected) = DeviceIdentityService.Protect(privateD, ReceiveKeyEntropy);
        if (!ReferenceEquals(protectedD, privateD)) Array.Clear(privateD);
        pairing.MailboxReceiveKeyId = envelope.ReceiveKeyId;
        pairing.MailboxReceivePublicKeyX = envelope.ReceivePublicKey.X;
        pairing.MailboxReceivePublicKeyY = envelope.ReceivePublicKey.Y;
        pairing.MailboxReceivePrivateKey = protectedD;
        pairing.MailboxReceivePrivateKeyProtected = wasProtected;
    }

    private static bool TryLoadReceiveKey(PairingState pairing, out RelayEcKeyPair key)
    {
        key = null!;
        if (pairing.MailboxReceivePrivateKey is null || pairing.MailboxReceivePublicKeyX is null || pairing.MailboxReceivePublicKeyY is null)
            return false;
        try
        {
            var privateD = DeviceIdentityService.Unprotect(pairing.MailboxReceivePrivateKey, pairing.MailboxReceivePrivateKeyProtected ?? false, ReceiveKeyEntropy);
            key = RelayCrypto.ImportEphemeralPrivateKey(
                new EcPublicKeyJwk { Kty = "EC", Crv = "P-256", X = pairing.MailboxReceivePublicKeyX, Y = pairing.MailboxReceivePublicKeyY }, privateD);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Catalog mailbox receive key could not be loaded; a fresh one will be published.");
            return false;
        }
    }

    private static bool HasUsableReceiveKey(PairingState pairing)
    {
        if (pairing.MailboxReceiveKeyId is null || !TryLoadReceiveKey(pairing, out var key))
            return false;
        key.Dispose();
        return true;
    }

    private void RecordCheckFailure(PairingState pairing, string message)
    {
        pairing.LastMailboxCheckError = message;
        config.Save();
    }

    private static string DescribeError(RelayException ex) => ex.Code switch
    {
        "network" => "Could not reach the relay - will check again later.",
        "rate_limited" => "The relay asked to slow down - will check again later.",
        "service_unavailable" => "The relay is temporarily unavailable - will check again later.",
        "unauthorized" => "The relay rejected the check (pairing no longer active on the relay?).",
        "not_found" => "The waiting catalog was already collected or replaced - will check again later.",
        _ => "The automatic catalog check failed - will try again later.",
    };
}
