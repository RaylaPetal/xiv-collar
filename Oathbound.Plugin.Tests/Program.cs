using System.Numerics;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Glamourer.Api.Enums;
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
config.SelectedGestureMods.Add("explicit-mod");
config.SelectedGestureFolders.Add("Animations/Poses");
config.SelectedRestraintFolders.Add("Restraints/Cuffs");
var restraintCatalog = new RestraintCatalogEntry
{
    Id = "0123456789abcdef", ModDirectory = "private/local/path", ModName = "Cuffs",
    GroupSelections = new() { ["Style"] = ["Arms"] },
};
config.RestraintMapping.LocalCatalog[restraintCatalog.Id] = restraintCatalog;
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
Check(restored.RestraintMapping.Devices[device.Id].SourceKind == RestraintSourceKind.Item, "legacy device was reinterpreted as catalog-backed");
Check(restored.SelectedGestureMods.SetEquals(["explicit-mod"]), "explicit gesture mod selection did not survive round trip");
Check(restored.SelectedGestureFolders.SequenceEqual(["Animations/Poses"]) && restored.SelectedRestraintFolders.SequenceEqual(["Restraints/Cuffs"]), "folder scopes did not survive round trip");
Check(restored.RestraintMapping.LocalCatalog[restraintCatalog.Id].GroupSelections["Style"].Single() == "Arms", "full local restraint selection was not preserved");
var restraintExport = RestraintCommand.EncodeExport(RestraintCatalogExportEntry.From(restraintCatalog));
Check(RestraintCommand.TryParseExport(restraintExport, out var parsedRestraint) && parsedRestraint?.Id == restraintCatalog.Id, "slim restraint export did not round trip");
Check(!restraintExport.Contains(restraintCatalog.ModDirectory, StringComparison.Ordinal), "slim restraint export leaked the local mod directory");
for (var i = 0; i < 1000; i++) restraintCatalog.GroupSelections[$"Extra {i}"] = [$"Value {i}"];
Check(RestraintCommand.EncodeExport(RestraintCatalogExportEntry.From(restraintCatalog)) == restraintExport,
    "slim restraint export grew with unrelated local group selections");
Check(CatalogSyncService.FitsPlaintextLimit(restraintExport) &&
      !CatalogSyncService.FitsPlaintextLimit(new string('x', RelayProtocolConstants.CatalogPlaintextMaxBytes + 1)),
    "catalog plaintext size ceiling was not enforced locally");
var restraintWire = RestraintCommand.BuildCatalogLockCommand(restraintCatalog.Id, restraintCatalog.Label,
    1234,
    [new RestraintRuleAssignment { Kind = RestraintRuleKind.GagChat }]);
Check(RestraintCommand.TryParseCatalogCommand(restraintWire["restraint catalog ".Length..], out var parsedCatalogId,
        out var parsedItemId, out var parsedCatalogRules)
      && parsedCatalogId == restraintCatalog.Id && parsedItemId == 1234 &&
      parsedCatalogRules.Single().Kind == RestraintRuleKind.GagChat,
    "catalog restraint command did not round trip");
Check(!RestraintCommand.TryParseCatalogCommand("short \"label\" rules:gag", out _, out _, out _), "short catalog identity was accepted");
Check(!RestraintCommand.TryParseCatalogCommand("0123456789abcdef missing-quote rules:gag", out _, out _, out _), "malformed catalog label was accepted");
Check(!RestraintCommand.TryParseCatalogCommand("0123456789abcdef \"label\" rules:gag" + new string('x', 500), out _, out _, out _), "oversized catalog command was accepted");
Check(restored.Aliases.Restraints.Single().DeviceId == device.Id, "restraint alias link did not survive round trip");
var quick = restored.QuickCommands.Gestures.Single();
Check(quick.Source == ImportSource.Imported && quick.IsFavorite && quick.Target == legacy.Id, "quick-command provenance metadata did not survive round trip");
Check(restored.QuickCommands.Follow.Single().Source == ImportSource.Manual, "manual quick command did not survive round trip");
Check(restored.QuickCommands.Titles.Single().Target is null, "legacy quick command gained fabricated identity metadata");
Check(restored.Aliases.CustomTriggers.Single().Actions.Select(a => a.Kind).SequenceEqual([CustomTriggerActionKind.Restraint, CustomTriggerActionKind.Title]), "custom-trigger action order did not survive round trip");

var migration = new PluginConfig { GestureModFolderFilter = " Animations\\Cuffs/ " };
migration.SelectedGestureMods.Add("keep-me");
Check(migration.MigrateFolderScopes(), "legacy animation folder migration reported no change");
Check(migration.SelectedGestureFolders.SequenceEqual(["Animations/Cuffs"]) && migration.SelectedGestureMods.Contains("keep-me"), "folder migration normalized incorrectly or cleared explicit mods");
Check(!migration.MigrateFolderScopes(), "folder migration was not idempotent");
Check(GestureCatalogScanner.IsUnder("Restraints/Cuffs/Arms", "restraints/cuffs") &&
      !GestureCatalogScanner.IsUnder("Restraints/Cufflinks", "Restraints/Cuffs"), "folder-prefix boundary matching failed");
var scopeMods = new (string Directory, string SortPath)[]
{
    ("arms", "Restraints/Cuffs/Arms"), ("legs", "Restraints/Rope/Legs"), ("dress", "Wardrobe/Dresses"),
};
Check(GestureCatalogScanner.SelectScope(scopeMods, ["Restraints/Cuffs", "Restraints/Rope"], new HashSet<string>(), false).SequenceEqual(["arms", "legs"]),
    "multiple restraint folder union included an outside mod or omitted a nested mod");
Check(GestureCatalogScanner.SelectScope(scopeMods, [], new HashSet<string>(), false).Count == 0,
    "empty restraint folders widened to every mod");
Check(GestureCatalogScanner.SelectScope(scopeMods, ["Missing/Folder"], new HashSet<string>(), false).Count == 0,
    "missing restraint folder widened the scan");
Check(GestureCatalogScanner.SelectScope(scopeMods, ["Restraints"], new HashSet<string> { "legs" }, true).SequenceEqual(["legs"]),
    "explicit animation mod did not narrow the selected folder union");
Check(GestureCatalogScanner.SelectScope(scopeMods, [], new HashSet<string>(), true).Count == 3,
    "empty animation folders/mods did not retain scan-all compatibility");

var manifestDir = Path.Combine(Path.GetTempPath(), $"oathbound-scan-{Guid.NewGuid():N}");
Directory.CreateDirectory(manifestDir);
try
{
    File.WriteAllText(Path.Combine(manifestDir, "default_mod.json"), "{\"Files\":{\"chara/equipment/e0001/model.mdl\":\"x\"}}");
    File.WriteAllText(Path.Combine(manifestDir, "group_010.json"), "{\"Type\":\"Single\",\"Name\":\"Style B\",\"Options\":[{\"Name\":\"B1\",\"Files\":{\"chara/equipment/b.mdl\":\"x\"}}]}");
    File.WriteAllText(Path.Combine(manifestDir, "group_002.json"), "{\"Type\":\"Single\",\"Name\":\"Style A\",\"Options\":[{\"Name\":\"A1\",\"Files\":{\"chara/equipment/a.mdl\":\"x\"}},{\"Name\":\"A2\",\"Files\":{}}]}");
    var scanned = GestureCatalogScanner.ScanManifest(manifestDir, "folder/mod", "Test restraints", false,
        new Dictionary<string, List<string>> { ["Style B"] = ["B1"] });
    Check(scanned.Select(x => x.GroupName).SequenceEqual(["Default", "Style A", "Style A", "Style B"]), "manifest numeric group order was not preserved");
    Check(scanned.Count == 4, "default or triggerless restraint options were omitted");
    Check(scanned.Single(x => x.OptionName == "A1").GroupSelections["Style B"].Single() == "B1", "complete group selections were not captured");
    Check(scanned.Single(x => x.OptionName == "A1").Id == GestureCatalogScanner.StableOptionId("folder/mod", "Style A", "A1"), "stable option identity drifted");
}
finally { Directory.Delete(manifestDir, true); }

var temporaryCalls = new List<string>();
var temporary = new TemporaryModSettingsCoordinator(
    (_, _, selections) => { temporaryCalls.Add("set:" + selections.Values.Single().Single()); return true; },
    (_, _) => { temporaryCalls.Add("remove"); return true; });
var collectionId = Guid.NewGuid();
Check(temporary.Acquire("gesture", collectionId, "same-mod", new Dictionary<string, IReadOnlyList<string>> { ["G"] = ["gesture"] }), "gesture layer failed");
Check(temporary.Acquire("restraint", collectionId, "same-mod", new Dictionary<string, IReadOnlyList<string>> { ["G"] = ["restraint"] }), "restraint layer failed");
Check(temporary.Release("gesture", collectionId, "same-mod") && temporaryCalls[^1] == "set:restraint", "releasing lower gesture layer disturbed restraint layer");
Check(temporary.Release("restraint", collectionId, "same-mod") && temporaryCalls[^1] == "remove", "releasing final restraint layer did not restore saved settings");
temporaryCalls.Clear();
temporary.Acquire("restraint", collectionId, "same-mod", new Dictionary<string, IReadOnlyList<string>> { ["G"] = ["restraint"] });
temporary.Acquire("gesture", collectionId, "same-mod", new Dictionary<string, IReadOnlyList<string>> { ["G"] = ["gesture"] });
Check(temporary.Release("gesture", collectionId, "same-mod") && temporaryCalls[^1] == "set:restraint", "releasing top gesture layer did not restore restraint layer");
var failedTemporary = new TemporaryModSettingsCoordinator((_, _, _) => false, (_, _) => true);
Check(!failedTemporary.Acquire("failed", collectionId, "same-mod", new Dictionary<string, IReadOnlyList<string>> { ["G"] = ["x"] }) &&
      !failedTemporary.Release("failed", collectionId, "same-mod"), "failed temporary apply left an owned layer behind");

var rulesManager = new Oathbound.Plugin.Safety.RestrictionRuleManager();
var gagEnforcer = new TestEnforcer();
rulesManager.RegisterEnforcer(RestraintRuleKind.GagChat, gagEnforcer);
var gagRule = new List<RestraintRuleAssignment> { new() { Kind = RestraintRuleKind.GagChat } };
Check(rulesManager.TryActivate("one", gagRule) && rulesManager.TryActivate("two", gagRule) && gagEnforcer.Engages == 1, "shared restriction rule was not reference-counted");
rulesManager.Release("one");
Check(gagEnforcer.Releases == 0, "shared restriction rule released while another owner remained");
rulesManager.Release("two");
Check(gagEnforcer.Releases == 1, "final restriction owner did not release enforcer");
var cuffA = new List<RestraintRuleAssignment> { new() { Kind = RestraintRuleKind.ArmsCuffed, AnimationId = "a" } };
var cuffB = new List<RestraintRuleAssignment> { new() { Kind = RestraintRuleKind.ArmsCuffed, AnimationId = "b" } };
Check(rulesManager.TryActivate("cuff-a", cuffA) && rulesManager.WouldConflict(cuffB, "cuff-b"), "conflicting cuff animation was not rejected before activation");

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

    var sharedOne = new RestraintCatalogExportEntry { Id = "1111111111111111", ModName = "Cuffs" };
    var sharedTwo = new RestraintCatalogExportEntry { Id = "2222222222222222", ModName = "Rope" };
    var configuredOne = new ConfiguredModRestraintExportEntry { Id = "configured-cuffs", CatalogId = sharedOne.Id, Name = "Strict cuffs",
        ItemId = 1234,
        Rules = [new RestraintRuleAssignment { Kind = RestraintRuleKind.GagChat }] };
    var restraintSnapshot = CompleteSnapshot(restraints: RestraintCommand.EncodeExport(sharedOne) + "\n" + RestraintCommand.EncodeConfiguredExport(configuredOne) + "\n");
    var restraintResult = sync.ApplyRelaySnapshot(restraintSnapshot, "pair-restraints");
    Check(restraintResult.Error is null && catalogConfig.RestraintMapping.ImportedPeerCatalog.ContainsKey(sharedOne.Id), "structured restraint relay import failed");
    var sharedQuick = catalogConfig.QuickCommands.Restraints.Single(x => x.RestraintCatalogId == sharedOne.Id);
    Check(sharedQuick.Label == configuredOne.Name && sharedQuick.RestraintItemId == 1234 &&
          sharedQuick.RestraintRules?.Single().Kind == RestraintRuleKind.GagChat,
        "Sub-configured restraint was not imported as a ready-made command");
    sharedQuick.IsFavorite = true;
    sharedQuick.RestraintRules = [new RestraintRuleAssignment { Kind = RestraintRuleKind.GagChat }];
    var updatedRestraintSnapshot = CompleteSnapshot(restraints: RestraintCommand.EncodeExport(sharedOne) + "\n" + RestraintCommand.EncodeExport(sharedTwo) + "\n" + RestraintCommand.EncodeConfiguredExport(configuredOne) + "\n");
    Check(sync.ApplyRelaySnapshot(updatedRestraintSnapshot, "pair-restraints").Error is null, "updated restraint snapshot failed");
    var carried = catalogConfig.QuickCommands.Restraints.Single(x => x.RestraintCatalogId == sharedOne.Id);
    Check(carried.IsFavorite && carried.RestraintRules?.Single().Kind == RestraintRuleKind.GagChat, "restraint favorite/rules were not carried forward");
    Check(catalogConfig.QuickCommands.Restraints.All(x => x.RestraintCatalogId != sharedTwo.Id), "raw restraint mod import auto-created a command");
    catalogConfig.QuickCommands.Restraints.Add(new QuickCommand { Label = sharedTwo.ModName,
        Command = RestraintCommand.BuildCatalogLockCommand(sharedTwo.Id, sharedTwo.ModName, 5678,
            [new RestraintRuleAssignment { Kind = RestraintRuleKind.GagChat }]),
        RestraintCatalogId = sharedTwo.Id, RestraintItemId = 5678,
        Target = sharedTwo.Id, Source = ImportSource.Manual });
    Check(sync.ApplyRelaySnapshot(CompleteSnapshot(restraints: RestraintCommand.EncodeExport(sharedTwo) + "\n"), "pair-restraints").Error is null &&
          !catalogConfig.RestraintMapping.ImportedPeerCatalog.ContainsKey(sharedOne.Id) &&
          catalogConfig.QuickCommands.Restraints.All(x => x.RestraintCatalogId != sharedOne.Id) &&
          catalogConfig.QuickCommands.Restraints.Any(x => x.RestraintCatalogId == sharedTwo.Id),
        "newer snapshot did not remove the retired Sub creation or removed an Owner-authored restraint");
    catalogConfig.QuickCommands.Restraints.Add(new QuickCommand { Label = "Manual legacy", Command = "restraint lock legacy", Source = ImportSource.Manual });
    var beforeBadRestraints = catalogConfig.QuickCommands.Restraints.Select(x => x.Command).ToArray();
    var badStructured = sync.ApplyRelaySnapshot(CompleteSnapshot(restraints: "OATHBOUND-RESTRAINT-V1|not-base64\n"), "pair-restraints");
    Check(badStructured.Error is not null && catalogConfig.QuickCommands.Restraints.Select(x => x.Command).SequenceEqual(beforeBadRestraints), "malformed structured restraint changed catalog state");
    Check(catalogConfig.QuickCommands.Restraints.Any(x => x.Label == "Manual legacy"), "relay reconciliation removed a manual legacy restraint");
    var offlineConfig = new PluginConfig { SaveOverride = () => { } };
    var offlineSync = new CatalogSyncService(offlineConfig, null!, null!, null!, null!);
    var offline = offlineSync.ParseImport("## RESTRAINTS\n" + RestraintCommand.EncodeExport(sharedOne) + "\n");
    Check(offline.Error is null && offlineConfig.RestraintMapping.ImportedPeerCatalog.ContainsKey(sharedOne.Id), "offline structured restraint import failed");
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

sealed class TestEnforcer : Oathbound.Plugin.Safety.IRestrictionEnforcer
{
    public bool IsAvailable => true;
    public int Engages { get; private set; }
    public int Releases { get; private set; }
    public void Engage() => Engages++;
    public void Release() => Releases++;
}
