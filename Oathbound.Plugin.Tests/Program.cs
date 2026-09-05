using System.Numerics;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Oathbound.Plugin.Commands;
using Oathbound.Plugin.Config;
using Oathbound.Plugin.Relay;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static GestureCatalogEntry Gesture(string id, string animation, GestureTrigger? trigger = null) => new()
{
    Id = id,
    ModDirectory = "mod-dir",
    ModName = "Collection",
    GroupName = "Animations",
    AnimationName = animation,
    Trigger = trigger ?? new GestureTrigger { Kind = GestureTriggerKind.SlashCommand, SlashCommand = "wave" },
    GroupSelections = new Dictionary<string, List<string>> { ["Animations"] = [animation] },
};

static string CompleteSnapshot(string wardrobe = "", string moodles = "", string restraints = "") =>
    $"## TITLE_ALIASES\n## WARDROBE\n{wardrobe}## WARDROBE_ALIASES\n## GESTURE\n## GESTURE_ALIASES\n## MOODLES\n{moodles}## MOODLES_ALIASES\n## RESTRAINTS\n{restraints}## RESTRAINTS_ALIASES\n## ALIASES\n";

var legacy = Gesture("ce7d75cb813f295c", "Legacy Wave");
var pose = Gesture("pose-id", "Cuffed Idle", new GestureTrigger { Kind = GestureTriggerKind.Pose, EmoteModeId = 1, CPoseState = 2 });
var entries = new[] { legacy, pose };

Check(CommandSelector.ResolveGestureDetailed(entries, "ce7d75cb813f295c").Entry == legacy, "legacy id did not resolve");
Check(CommandSelector.ResolveGestureDetailed(entries, CommandSelector.Quote(legacy.Label)).Entry == legacy, "quoted readable selector did not resolve");
var slashById = CommandSelector.ResolveGestureDetailed(entries, legacy.Id).Entry;
var slashByName = CommandSelector.ResolveGestureDetailed(entries, legacy.AnimationName).Entry;
Check(slashById == slashByName && slashById?.Trigger?.Kind == GestureTriggerKind.SlashCommand, "slash id/name selected different playback paths");
var poseById = CommandSelector.ResolveGestureDetailed(entries, pose.Id).Entry;
var poseByName = CommandSelector.ResolveGestureDetailed(entries, pose.AnimationName).Entry;
Check(poseById == poseByName && poseById?.Trigger?.Kind == GestureTriggerKind.Pose, "pose id/name selected different playback paths");
Check(CommandSelector.ResolveGestureDetailed(entries, "\"unterminated").Status == CommandSelector.ResolutionStatus.Malformed, "malformed quote was accepted");
Check(CommandSelector.ResolveGestureDetailed(entries, "missing").Status == CommandSelector.ResolutionStatus.Missing, "missing selector was not diagnosed");
Check(CommandSelector.ResolveGestureDetailed([legacy, Gesture("other", legacy.AnimationName)], legacy.AnimationName).Status == CommandSelector.ResolutionStatus.Ambiguous, "ambiguous label was not diagnosed");

var triggerless = new GestureCatalogEntry
{
    Id = "idle-id",
    ModDirectory = "idle-mod",
    ModName = "Cuffs Collection",
    GroupName = "Idle",
    AnimationName = "Bound Idle",
    Trigger = null,
    GroupSelections = new Dictionary<string, List<string>> { ["Idle"] = ["Bound Idle"] },
};
Check(CommandSelector.ResolveGestureDetailed([triggerless], triggerless.Id).Status == CommandSelector.ResolutionStatus.Missing,
    "gesture playback accepted an enable-only animation");
Check(CommandSelector.ResolveGestureDetailed([triggerless], triggerless.Id, requireTrigger: false).Entry == triggerless,
    "restraint resolution rejected a valid enable-only animation");

var config = new PluginConfig();
config.DeviceIdentity.PublicKeyX = "px";
config.DeviceIdentity.PublicKeyY = "py";
config.DeviceIdentity.ProtectedPrivateKey = [1, 2, 3, 4];
config.DeviceIdentity.DeviceKeyId = "deadbeef";
config.Pairing.PairIdHash = "pair-hash";
config.Pairing.PairEpoch = 2;
config.Pairing.PeerDeviceKeyId = "peer-key-id";
config.Pairing.PeerPublicKeyX = "peer-x";
config.Pairing.PeerPublicKeyY = "peer-y";
config.Pairing.OutgoingRevocationSequence = 5;
config.Pairing.IncomingRevocationSequence = 3;
config.RevocationOutbox.Add(new RevocationRetryEntry
{
    PairIdHash = "pair-hash",
    PairEpoch = 2,
    Sequence = 6,
    Reason = "panic",
    CreatedAt = 1000,
    ExpiresAt = 2000,
    Signature = "sig",
    Attempt = 1,
    NextAttemptAtUnixSeconds = 1500,
});
config.PendingRelayOperations.Add(new PendingRelayOperationState { Kind = "pair-invite", OperationId = "opaque", Target = "Peer@World", ExpiresAt = 2000 });

var device = new RestraintDeviceDefinition { Id = "stable-device", Name = "Cuffs", Rules = [new RestraintRuleAssignment { Kind = RestraintRuleKind.ArmsCuffed, AnimationId = "idle-id", AnimationLabel = "Cuffed Idle" }] };
config.RestraintMapping.Devices[device.Id] = device;
config.Aliases.Restraints.Add(new RestraintAliasDefinition { Alias = "cuffs", DeviceId = device.Id, DeviceName = device.Name });
config.QuickCommands.Gestures.Add(new QuickCommand { Label = "Wave", Command = "gesture legacy", Target = legacy.Id, Source = ImportSource.Imported, IsFavorite = true });
config.QuickCommands.Follow.Add(new QuickCommand { Label = "My leash", Command = "leash", Source = ImportSource.Manual });
config.QuickCommands.Titles.Add(new QuickCommand { Label = "Legacy title", Command = "title create Pet" });
config.Aliases.CustomTriggers.Add(new CustomTriggerDefinition
{
    Alias = "scene",
    Actions =
    [
        new CustomTriggerAction { Kind = CustomTriggerActionKind.Restraint, RestraintDeviceId = device.Id, RestraintDeviceName = device.Name },
        new CustomTriggerAction { Kind = CustomTriggerActionKind.Title, TitleText = "Pet", TitleColor = new Vector3(1, .5f, .25f) },
    ],
});

var json = JsonSerializer.Serialize(config);
var restored = JsonSerializer.Deserialize<PluginConfig>(json) ?? throw new InvalidOperationException("config round trip returned null");
Check(restored.RestraintMapping.Devices.ContainsKey(device.Id), "device identity did not survive round trip");
Check(restored.Aliases.Restraints.Single().DeviceId == device.Id, "restraint alias link did not survive round trip");
var quick = restored.QuickCommands.Gestures.Single();
Check(quick.Source == ImportSource.Imported && quick.IsFavorite && quick.Target == legacy.Id, "quick-command provenance metadata did not survive round trip");
Check(restored.QuickCommands.Follow.Single().Source == ImportSource.Manual, "manual quick command did not survive round trip");
Check(restored.QuickCommands.Titles.Single().Target is null, "legacy quick command gained fabricated identity metadata");
Check(restored.Aliases.CustomTriggers.Single().Actions.Select(a => a.Kind).SequenceEqual([CustomTriggerActionKind.Restraint, CustomTriggerActionKind.Title]), "custom-trigger action order did not survive round trip");

Check(restored.DeviceIdentity.HasIdentity, "device identity did not survive round trip");
Check(restored.DeviceIdentity.ProtectedPrivateKey!.SequenceEqual((byte[])[1, 2, 3, 4]), "protected private key bytes did not survive round trip");
Check(RelayClient.RelayOrigin == "https://oathbound-relay-staging.oathbound.workers.dev", "relay origin is not pinned to the shipped service");
Check(restored.Pairing is { PairIdHash: "pair-hash", PairEpoch: 2, PeerDeviceKeyId: "peer-key-id", OutgoingRevocationSequence: 5, IncomingRevocationSequence: 3 },
    "relay pairing state did not survive round trip");
Check(restored.RevocationOutbox.Single() is { PairIdHash: "pair-hash", Sequence: 6, Reason: "panic", Attempt: 1 }, "revocation outbox entry did not survive round trip");
Check(restored.PendingRelayOperations.Single() is { Kind: "pair-invite", OperationId: "opaque", Target: "Peer@World" }, "pending relay operation did not survive restart round trip");
Check(RelayClient.RelayOrigin.StartsWith("https://", StringComparison.Ordinal) && !json.Contains("relay.example", StringComparison.Ordinal), "configuration can redirect the pinned relay origin");

Uri? observedRelayUri = null;
using (var boundedClient = new RelayClient(config, new DeviceIdentityService(config), new StubHandler(request =>
{
    observedRelayUri = request.RequestUri;
    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 2_000_000)) };
})))
{
    try
    {
        await boundedClient.FetchInvitationAsync("opaque", CancellationToken.None);
        throw new InvalidOperationException("oversized relay response was accepted");
    }
    catch (RelayException ex)
    {
        Check(ex.Code == "payload_too_large", "oversized relay response returned the wrong structured error");
    }
}
Check(observedRelayUri?.GetLeftPart(UriPartial.Authority) == RelayClient.RelayOrigin, "relay client did not use the fixed origin");

using (var invitationKey = RelayCrypto.GenerateSigningKeyPair())
{
    var invitation = new InvitationEnvelope
    {
        InvitationId = RelayCrypto.RandomCapabilityId(),
        InviterDeviceKeyId = RelayCrypto.DeviceKeyId(RelayCrypto.ExportPublicKeyJwk(invitationKey)),
        InviterPublicKey = RelayCrypto.ExportPublicKeyJwk(invitationKey),
        Role = "owner",
        TriggerPhrase = "kae",
        CreatedAt = 1000,
        ExpiresAt = 1900,
    };
    invitation.Signature = RelayCrypto.SignRaw(invitationKey, EnvelopeCanonical.SerializeExcludingSignature(invitation));
    Check(RelayCrypto.VerifyRaw(invitation.InviterPublicKey, invitation.Signature, EnvelopeCanonical.SerializeExcludingSignature(invitation)),
        "invitation containing the peer trigger phrase did not verify");
    invitation.TriggerPhrase = "changed";
    Check(!RelayCrypto.VerifyRaw(invitation.InviterPublicKey, invitation.Signature, EnvelopeCanonical.SerializeExcludingSignature(invitation)),
        "invitation signature did not bind the trigger phrase");
}

var ownerBundleActions = new List<CustomTriggerAction>
{
    new() { Kind = CustomTriggerActionKind.Restraint, RestraintDeviceId = Guid.NewGuid().ToString(), RestraintDeviceName = "Gagged" },
    new() { Kind = CustomTriggerActionKind.Restraint, RestraintDeviceId = Guid.NewGuid().ToString(), RestraintDeviceName = "Body Cuffed" },
    new() { Kind = CustomTriggerActionKind.Moodle, MoodleStatusId = Guid.NewGuid().ToString(), MoodleStatusName = "Exhibitionists" },
};
var ownerBundle = CustomTriggerCommand.BuildCastCommand("gagbind", ownerBundleActions);
Check(ownerBundle.StartsWith("customtrigger cast ", StringComparison.Ordinal), "owner bundle used the wrong wire verb");
Check(CustomTriggerCommand.TryParseCastCommand(ownerBundle["customtrigger cast ".Length..], out var parsedLabel, out var parsedActions),
    "saved owner bundle could not be reopened for editing");
Check(parsedLabel == "gagbind" && parsedActions.Select(a => a.Kind).SequenceEqual(ownerBundleActions.Select(a => a.Kind)),
    "saved owner bundle edit round trip changed its label or action order");
Check(parsedActions.Select(a => a.RestraintDeviceId ?? a.MoodleStatusId).SequenceEqual(ownerBundleActions.Select(a => a.RestraintDeviceId ?? a.MoodleStatusId)),
    "saved owner bundle edit round trip lost stable action identities");

// --- collar/catalog-sync: ApplyRelaySnapshot's atomic add/update/remove reconciliation (task 6.5). Only
// touches the fields ApplyRelaySnapshot actually calls (all static), so the unused IPC-backed dependencies
// can be left null.
{
    var catalogConfig = new PluginConfig();
    catalogConfig.SaveOverride = () => { };
    catalogConfig.QuickCommands.Titles.Add(new QuickCommand { Label = "Manual Title", Command = "title create Pet", Source = ImportSource.Manual });
    var sync = new CatalogSyncService(catalogConfig, null!, null!, null!, null!);

    var firstSnapshot = CompleteSnapshot("DesignA\nDesignB\n");
    var firstResult = sync.ApplyRelaySnapshot(firstSnapshot, "pair-a");
    Check(firstResult.Error is null, "first relay snapshot failed unexpectedly");
    Check(catalogConfig.QuickCommands.Outfits.Count == 2 && catalogConfig.QuickCommands.Outfits.All(c => c.SourcePairIdHash == "pair-a"),
        "first snapshot's outfits were not tagged with the source pair");

    var designA = catalogConfig.QuickCommands.Outfits.Single(c => c.Target == "DesignA");
    designA.IsFavorite = true;

    var secondSnapshot = CompleteSnapshot("DesignA\nDesignC\n");
    var secondResult = sync.ApplyRelaySnapshot(secondSnapshot, "pair-a");
    Check(secondResult.Error is null, "second relay snapshot failed unexpectedly");

    var outfits = catalogConfig.QuickCommands.Outfits;
    Check(outfits.Count == 2, "outfit count after replacement was wrong");
    Check(outfits.Single(c => c.Target == "DesignA").IsFavorite, "favorite flag was not carried forward across snapshots");
    Check(outfits.SingleOrDefault(c => c.Target == "DesignC") is not null, "new entry from the second snapshot was not added");
    Check(outfits.SingleOrDefault(c => c.Target == "DesignB") is null, "entry removed from the second snapshot was not deleted");
    Check(catalogConfig.QuickCommands.Titles.Single().Source == ImportSource.Manual, "a manual entry was touched by relay snapshot reconciliation");

    var associated = sync.AssociateLegacyImportsWithPair("pair-b");
    Check(associated == 0, "no legacy (unscoped) imports should exist in this fixture, but AssociateLegacyImportsWithPair reported some");

    var manualResult = sync.ParseImport("## MOODLES\nFriendly Status\n");
    Check(manualResult.Error is null && catalogConfig.QuickCommands.Moodles.Any(c => c.Target == "Friendly Status"),
        "manual import did not use the validated staging path");

    var beforeFailedSave = catalogConfig.QuickCommands.Outfits.Select(c => c.Command).ToArray();
    catalogConfig.SaveOverride = () => throw new IOException("simulated persistence failure");
    var failedSave = sync.ApplyRelaySnapshot(CompleteSnapshot("MustNotCommit\n"), "pair-a");
    Check(failedSave.Error is not null && catalogConfig.QuickCommands.Outfits.Select(c => c.Command).SequenceEqual(beforeFailedSave),
        "failed snapshot persistence did not roll configuration back atomically");

    catalogConfig.SaveOverride = () => { };
    var beforeMalformed = catalogConfig.QuickCommands.Outfits.Select(c => c.Command).ToArray();
    var malformed = sync.ApplyRelaySnapshot("## WARDROBE\nTruncated\n", "pair-a");
    Check(malformed.Error is not null && catalogConfig.QuickCommands.Outfits.Select(c => c.Command).SequenceEqual(beforeMalformed),
        "incomplete relay snapshot changed existing imports");
}

// --- collar/catalog-sync: the full Sub -> Owner encrypted snapshot path (compress, ECDH+HKDF, AES-GCM
// encrypt with the real CatalogResponseAad, decrypt, decompress) using the actual production helpers, not
// a re-derivation - this is what CatalogSyncRelayService itself does on each side.
{
    var pairIdHash = "6".PadRight(64, '6');
    var requestId = RelayCrypto.RandomCapabilityId();

    using var ownerEphemeral = RelayCrypto.GenerateEphemeralKeyPair();
    using var subEphemeral = RelayCrypto.GenerateEphemeralKeyPair();

    var plaintext = System.Text.Encoding.UTF8.GetBytes("## WARDROBE\nSomeDesign\n## WARDROBE_ALIASES\n");
    var compressed = RelayCompression.Compress(plaintext);

    var sharedFromSub = RelayCrypto.DeriveSharedSecret(subEphemeral, RelayCrypto.ImportEphemeralPublicKey(RelayCrypto.ExportPublicKeyJwk(ownerEphemeral)));
    var ownerRaw = RelayCrypto.ExportRawUncompressedPoint(ownerEphemeral);
    var subRaw = RelayCrypto.ExportRawUncompressedPoint(subEphemeral);
    var salt = System.Security.Cryptography.SHA256.HashData([.. ownerRaw, .. subRaw]);
    var info = RelayCrypto.BuildCatalogHkdfInfo(pairIdHash, requestId);
    var aesKey = RelayCrypto.DeriveAesKey(sharedFromSub, salt, info);
    var nonce = RelayCrypto.RandomBytes(RelayCrypto.AeadNonceLengthBytes);

    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var responseEnvelope = new CatalogResponseEnvelope
    {
        PairIdHash = pairIdHash,
        PairEpoch = 0,
        RequestId = requestId,
        SnapshotId = 1,
        SenderDeviceKeyId = "s".PadRight(64, 's'),
        RecipientDeviceKeyId = "r".PadRight(64, 'r'),
        CreatedAt = now,
        ExpiresAt = now + 900,
        CiphertextSizeBytes = 0,
        Nonce = RelayCrypto.Base64UrlEncode(nonce),
        SenderEphemeralPublicKey = RelayCrypto.ExportPublicKeyJwk(subEphemeral),
    };
    var aad = CatalogResponseAad.Build(responseEnvelope);
    var ciphertext = RelayCrypto.AesGcmEncrypt(aesKey, nonce, compressed, aad);
    responseEnvelope.CiphertextSizeBytes = ciphertext.Length;
    responseEnvelope.CiphertextDigest = RelayCrypto.Sha256Hex(ciphertext);

    Check(RelayCrypto.Sha256Hex(ciphertext) == responseEnvelope.CiphertextDigest, "ciphertext digest mismatch");

    // Owner side: independently re-derive the AAD from the (now-complete) envelope and confirm it decrypts.
    var aadFromOwner = CatalogResponseAad.Build(responseEnvelope);
    var sharedFromOwner = RelayCrypto.DeriveSharedSecret(ownerEphemeral, RelayCrypto.ImportEphemeralPublicKey(responseEnvelope.SenderEphemeralPublicKey));
    var aesKeyOwner = RelayCrypto.DeriveAesKey(sharedFromOwner, salt, info);
    var decompressedFromOwner = RelayCompression.Decompress(RelayCrypto.AesGcmDecrypt(aesKeyOwner, nonce, ciphertext, aadFromOwner), RelayProtocolConstants.CatalogPlaintextMaxBytes);
    Check(decompressedFromOwner.SequenceEqual(plaintext), "catalog snapshot did not round-trip through compress/encrypt/decrypt/decompress");

    // A tampered ciphertext byte must fail authentication, not silently decrypt to garbage.
    var tampered = (byte[])ciphertext.Clone();
    tampered[0] ^= 0xFF;
    try
    {
        RelayCrypto.AesGcmDecrypt(aesKeyOwner, nonce, tampered, aadFromOwner);
        throw new InvalidOperationException("ASSERTION FAILED: decrypting a tampered catalog ciphertext did not throw");
    }
    catch (System.Security.Cryptography.CryptographicException) { /* expected */ }

    // A decompression bomb (small compressed input claiming to expand far past the plaintext cap) must be refused.
    var hugePlaintext = new byte[RelayProtocolConstants.CatalogPlaintextMaxBytes + 1];
    var hugeCompressed = RelayCompression.Compress(hugePlaintext);
    try
    {
        RelayCompression.Decompress(hugeCompressed, RelayProtocolConstants.CatalogPlaintextMaxBytes);
        throw new InvalidOperationException("ASSERTION FAILED: decompressing past the plaintext cap did not throw");
    }
    catch (InvalidDataException) { /* expected */ }
}

// --- Cross-runtime relay protocol vectors (task 1.2/1.3): the same protocol/vectors/crypto-vectors.json
// file the Worker's test/vectors.spec.ts verifies against, generated once from a Node WebCrypto reference
// script. This is the C# half of "published cross-runtime vectors pass in both C# and Worker tests" -
// task 1.2 in openspec/changes/add-cloudflare-pairing-catalog-relay/tasks.md is only checked off once this
// and the Worker suite both pass against the same file.
{
    var vectorsPath = FindRepoFile("protocol/vectors/crypto-vectors.json");
    using var vectorsDoc = JsonDocument.Parse(File.ReadAllText(vectorsPath));
    var root = vectorsDoc.RootElement;

    var canonicalJsonVector = root.GetProperty("canonicalJson");
    var canonicalInput = JsonElementToCanonicalValue(canonicalJsonVector.GetProperty("input"));
    var canonical = CanonicalJson.Serialize(canonicalInput);
    Check(canonical == canonicalJsonVector.GetProperty("canonical").GetString(), "canonical JSON did not match the published vector byte-for-byte");
    Check(RelayCrypto.Sha256Hex(canonical) == canonicalJsonVector.GetProperty("sha256Hex").GetString(), "canonical JSON digest did not match the published vector");

    var ecdsaVector = root.GetProperty("ecdsaSignRequest");
    var signingPublicKeyJwk = JsonSerializer.Deserialize<EcPublicKeyJwk>(ecdsaVector.GetProperty("signingPublicKeyJwk").GetRawText())
        ?? throw new InvalidOperationException("vector signingPublicKeyJwk failed to parse");
    var baseString = ecdsaVector.GetProperty("baseString").GetString()!;
    var signatureBase64Url = ecdsaVector.GetProperty("signatureBase64Url").GetString()!;
    Check(RelayCrypto.VerifyRaw(signingPublicKeyJwk, signatureBase64Url, baseString), "published ECDSA signature did not verify against its public key");
    Check(!RelayCrypto.VerifyRaw(signingPublicKeyJwk, signatureBase64Url, baseString + "tampered"), "ECDSA verification accepted a tampered base string");

    var aeadVector = root.GetProperty("ecdhHkdfAesGcmCatalogEnvelope");
    var ownerPublicJwk = JsonSerializer.Deserialize<EcPublicKeyJwk>(aeadVector.GetProperty("ownerEphemeralPublicKeyJwk").GetRawText())!;
    var ownerPrivateD = RelayCrypto.Base64UrlDecode(aeadVector.GetProperty("ownerEphemeralPrivateKeyJwk").GetProperty("d").GetString()!);
    var subPublicJwk = JsonSerializer.Deserialize<EcPublicKeyJwk>(aeadVector.GetProperty("subEphemeralPublicKeyJwk").GetRawText())!;
    var subPrivateD = RelayCrypto.Base64UrlDecode(aeadVector.GetProperty("subEphemeralPrivateKeyJwk").GetProperty("d").GetString()!);

    using var ownerPrivateKey = RelayCrypto.ImportEphemeralPrivateKey(ownerPublicJwk, ownerPrivateD);
    using var ownerPublicKeyOnly = RelayCrypto.ImportEphemeralPublicKey(ownerPublicJwk);
    using var subPrivateKey = RelayCrypto.ImportEphemeralPrivateKey(subPublicJwk, subPrivateD);
    using var subPublicKeyOnly = RelayCrypto.ImportEphemeralPublicKey(subPublicJwk);

    var sharedFromSub = RelayCrypto.DeriveSharedSecret(subPrivateKey, ownerPublicKeyOnly);
    var sharedFromOwner = RelayCrypto.DeriveSharedSecret(ownerPrivateKey, subPublicKeyOnly);
    Check(sharedFromSub.SequenceEqual(sharedFromOwner), "ECDH shared secrets disagreed between the two sides");

    var ownerRaw = RelayCrypto.ExportRawUncompressedPoint(ownerPublicJwk);
    var subRaw = RelayCrypto.ExportRawUncompressedPoint(subPublicJwk);
    var salt = System.Security.Cryptography.SHA256.HashData([.. ownerRaw, .. subRaw]);
    Check(Convert.ToHexStringLower(salt) == aeadVector.GetProperty("saltSha256Hex").GetString(), "derived salt did not match the published vector");

    var info = System.Text.Encoding.UTF8.GetBytes(aeadVector.GetProperty("infoUtf8").GetString()!);
    var aesKey = RelayCrypto.DeriveAesKey(sharedFromSub, salt, info);
    Check(Convert.ToHexStringLower(aesKey) == aeadVector.GetProperty("derivedAesKeyHex").GetString(), "derived AES key did not match the published vector");

    var nonce = RelayCrypto.Base64UrlDecode(aeadVector.GetProperty("nonceBase64Url").GetString()!);
    var aad = System.Text.Encoding.UTF8.GetBytes(aeadVector.GetProperty("additionalAuthenticatedDataCanonicalJson").GetString()!);
    var ciphertextWithTag = RelayCrypto.Base64UrlDecode(aeadVector.GetProperty("ciphertextWithTagBase64Url").GetString()!);
    Check(RelayCrypto.Sha256Hex(ciphertextWithTag) == aeadVector.GetProperty("ciphertextDigestSha256Hex").GetString(), "ciphertext digest did not match the published vector");

    var decrypted = RelayCrypto.AesGcmDecrypt(aesKey, nonce, ciphertextWithTag, aad);
    Check(System.Text.Encoding.UTF8.GetString(decrypted) == aeadVector.GetProperty("plaintextUtf8").GetString(), "decrypting the published ciphertext did not recover the published plaintext");

    // Round trip the other direction too: re-encrypt with the same key/nonce/aad and confirm byte-identical output.
    var reEncrypted = RelayCrypto.AesGcmEncrypt(aesKey, nonce, decrypted, aad);
    Check(reEncrypted.SequenceEqual(ciphertextWithTag), "re-encrypting the recovered plaintext did not reproduce the published ciphertext byte-for-byte");

    // task 3.3 "altered metadata/ciphertext fails authentication": a tampered AAD or ciphertext byte must
    // make AES-GCM's own authentication tag check fail, not silently decrypt to garbage.
    var tamperedAad = System.Text.Encoding.UTF8.GetBytes(aeadVector.GetProperty("additionalAuthenticatedDataCanonicalJson").GetString() + "x");
    try
    {
        RelayCrypto.AesGcmDecrypt(aesKey, nonce, ciphertextWithTag, tamperedAad);
        throw new InvalidOperationException("ASSERTION FAILED: decrypting with tampered AAD did not throw");
    }
    catch (System.Security.Cryptography.CryptographicException) { /* expected */ }

    var tamperedCiphertext = (byte[])ciphertextWithTag.Clone();
    tamperedCiphertext[0] ^= 0xFF;
    try
    {
        RelayCrypto.AesGcmDecrypt(aesKey, nonce, tamperedCiphertext, aad);
        throw new InvalidOperationException("ASSERTION FAILED: decrypting tampered ciphertext did not throw");
    }
    catch (System.Security.Cryptography.CryptographicException) { /* expected */ }
}

Console.WriteLine("Oathbound regression checks passed.");

static string FindRepoFile(string relativePath)
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, relativePath);
        if (File.Exists(candidate)) return candidate;
        dir = dir.Parent;
    }
    throw new FileNotFoundException($"Could not locate '{relativePath}' by walking up from {AppContext.BaseDirectory}");
}

static object? JsonElementToCanonicalValue(JsonElement element) => element.ValueKind switch
{
    JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => JsonElementToCanonicalValue(p.Value)),
    JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToCanonicalValue).ToList(),
    JsonValueKind.String => element.GetString(),
    JsonValueKind.Number => element.GetInt64(),
    JsonValueKind.True => true,
    JsonValueKind.False => false,
    JsonValueKind.Null => null,
    _ => throw new NotSupportedException($"Unsupported JSON value kind: {element.ValueKind}"),
};

sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(respond(request));
}
