using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Oathbound.Plugin.Commands;
using Oathbound.Plugin.Config;

namespace Oathbound.Plugin.Relay;

/// collar/catalog-sync automatic sync - all the *when*, driven from Plugin.OnFrameworkUpdate (so everything
/// here that reads or rescans the catalog runs on the framework thread; only the relay work itself goes to
/// the thread pool, via CatalogMailboxService):
///
///   Sub   - rescans Glamourer/Penumbra/Moodles at login and ~hourly (if AutoRescanCatalogs), one category
///           per tick so it's never one long frame;
///         - after any save, once things have been quiet for ChangeQuietPeriod, digests the export and
///           publishes it to each Sub-side pairing whose last published digest differs;
///         - ~hourly (and shortly after login), asks the relay whether its last push actually reached the
///           Owner and republishes it if not, even if nothing changed.
///   Owner - checks each Owner-side pairing's mailbox shortly after login and ~hourly, plus on demand.
///
/// Every schedule gets 0-5 minutes of jitter so many clients never hit the relay on the same clock edge.
public sealed class CatalogAutoSync
{
    private static readonly TimeSpan ChangeQuietPeriod = TimeSpan.FromSeconds(60);
    /// Upper bound on the debounce: if unrelated saves keep landing inside every quiet window, a pending
    /// change still goes out this long after it first appeared.
    private static readonly TimeSpan ChangeMaxWait = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Hourly = TimeSpan.FromSeconds(RelayProtocolConstants.CatalogMailboxOwnerPollIntervalSeconds);
    private static readonly TimeSpan OnDemandMinimumGap = TimeSpan.FromSeconds(60);

    private readonly PluginConfig config;
    private readonly CatalogMailboxService mailbox;
    private readonly CatalogSyncService catalogSync;
    private readonly Action[] rescanSteps;
    private readonly Func<CancellationToken> backgroundToken;

    private DateTime nextRescanUtc;
    private int rescanStage = -1;

    // Written from the thread pool (config.Save() inside a publish/check raises Changed), read on the framework thread.
    private int catalogDirty;
    private long lastChangeTicks;
    private long dirtySinceTicks;
    private DateTime nextDeliveryCheckUtc;

    /// Sub-side, per pairing: don't retry before this time for the digest that failed (a genuinely new digest
    /// is still tried right away - only the same content is held back).
    private readonly ConcurrentDictionary<Guid, (DateTime NotBefore, string Digest)> subRetry = new();
    private readonly ConcurrentDictionary<Guid, DateTime> nextOwnerCheckUtc = new();

    public CatalogAutoSync(PluginConfig config, CatalogMailboxService mailbox, CatalogSyncService catalogSync,
        OutfitCommand outfit, GestureCommand gesture, RestraintCommand restraints, MoodlesCommand moodles,
        Func<CancellationToken> backgroundToken)
    {
        this.config = config;
        this.mailbox = mailbox;
        this.catalogSync = catalogSync;
        this.backgroundToken = backgroundToken;
        rescanSteps = [outfit.Rescan, gesture.Rescan, restraints.RescanCatalog, moodles.Rescan];
        config.Changed += MarkDirty;
        ScheduleStartup();
    }

    public void Dispose() => config.Changed -= MarkDirty;

    /// collar/catalog-sync "at login": rescan, publish anything pending, and check the Owner mailboxes soon
    /// after login (a short delay lets Penumbra/Glamourer/Moodles finish coming up first).
    public void OnLogin() => ScheduleStartup();

    private void ScheduleStartup()
    {
        var now = DateTime.UtcNow;
        nextRescanUtc = now.AddSeconds(30);
        nextDeliveryCheckUtc = now.AddSeconds(45);
        nextOwnerCheckUtc.Clear(); // Lazily re-seeded ~15s out on the next tick.
    }

    private void MarkDirty()
    {
        var now = DateTime.UtcNow.Ticks;
        Interlocked.Exchange(ref lastChangeTicks, now);
        if (Interlocked.Exchange(ref catalogDirty, 1) == 0)
            Interlocked.Exchange(ref dirtySinceTicks, now);
    }

    private static TimeSpan Jitter() => TimeSpan.FromSeconds(Random.Shared.Next(0, 300));

    public void OnFrameworkUpdate()
    {
        if (!Plugin.ClientState.IsLoggedIn)
            return;
        var now = DateTime.UtcNow;
        TickRescan(now);
        TickSubPublish(now);
        TickOwnerChecks(now);
    }

    // ---- Sub: periodic rescan ----

    private bool HasSubPairing => config.Pairings.Any(p => p is { Direction: PairingDirection.SubSide, IsPaired: true });

    private void TickRescan(DateTime now)
    {
        if (rescanStage < 0)
        {
            if (now < nextRescanUtc) return;
            nextRescanUtc = now + Hourly + Jitter();
            if (!config.AutoRescanCatalogs || !HasSubPairing) return;
            rescanStage = 0;
        }

        // One category per tick. Each rescan already leaves its catalog untouched when its source plugin is
        // unavailable; the catch is only so one category's unexpected failure can't stop the others.
        try
        {
            rescanSteps[rescanStage]();
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, $"Scheduled catalog rescan step {rescanStage} failed; that category was left as it was.");
        }
        rescanStage = rescanStage + 1 < rescanSteps.Length ? rescanStage + 1 : -1;
    }

    // ---- Sub: change-driven publish + hourly delivery check ----

    private void TickSubPublish(DateTime now)
    {
        if (rescanStage >= 0) return; // Let a rescan finish so its results go out in one publish.

        var deliveryDue = now >= nextDeliveryCheckUtc;
        var changeReady = Volatile.Read(ref catalogDirty) == 1 &&
                          (now - new DateTime(Interlocked.Read(ref lastChangeTicks), DateTimeKind.Utc) >= ChangeQuietPeriod ||
                           now - new DateTime(Interlocked.Read(ref dirtySinceTicks), DateTimeKind.Utc) >= ChangeMaxWait);
        if (!deliveryDue && !changeReady) return;

        if (changeReady) Interlocked.Exchange(ref catalogDirty, 0);
        if (deliveryDue) nextDeliveryCheckUtc = now + Hourly + Jitter();

        // collar/catalog-sync "Catalog sync permission is off": nothing is built or sent. Turning it back on
        // saves the config, which marks the catalog dirty and publishes on the next quiet period.
        if (!config.Permissions.RelayCatalogSync) return;
        var subPairings = config.Pairings
            .Where(p => p is { Direction: PairingDirection.SubSide, IsPaired: true, PairIdHash.Length: > 0 })
            .ToList();
        if (subPairings.Count == 0) return;

        if (!catalogSync.TryBuildBoundedExport(out var exportText, out var exportError))
        {
            Plugin.Log.Warning(exportError ?? "Catalog export exceeded a local size limit; not published.");
            return;
        }
        var digest = RelayCrypto.Sha256Hex(exportText);

        foreach (var pairing in subPairings)
        {
            var held = subRetry.TryGetValue(pairing.Id, out var retry) && now < retry.NotBefore;
            if (deliveryDue)
            {
                // The hourly pass asks the relay even when the digest is unchanged (did the last push arrive?),
                // but still respects an explicit "wait" from the relay for this same content.
                if (held && retry.Digest == digest) continue;
            }
            else
            {
                if (digest == pairing.LastPublishedCatalogDigest) continue; // A save that didn't change the export.
                if (held && retry.Digest == digest) continue;
            }
            Publish(pairing, exportText, digest);
        }
    }

    private void Publish(PairingState pairing, string exportText, string digest)
    {
        var task = mailbox.PublishAsync(pairing, exportText, digest, backgroundToken());
        Plugin.FireAndForget(task.ContinueWith(t =>
        {
            if (t.IsCanceled || t.IsFaulted) return;
            var result = t.Result;
            var now = DateTime.UtcNow;
            switch (result.Outcome)
            {
                case MailboxPublishOutcome.Published:
                    subRetry.TryRemove(pairing.Id, out _);
                    break;
                case MailboxPublishOutcome.RateLimited:
                    subRetry[pairing.Id] = (now.AddSeconds(result.RetryAfterSeconds + 1), digest);
                    MarkDirtyAt(now.AddSeconds(result.RetryAfterSeconds + 1) - ChangeQuietPeriod);
                    break;
                default:
                    // Not ready (no Owner key yet) or failed: the hourly delivery pass retries; a newer change
                    // (different digest) is still tried as soon as it settles.
                    subRetry[pairing.Id] = (now + Hourly, digest);
                    break;
            }
        }, TaskScheduler.Default));
    }

    /// Marks the catalog dirty as if the last change happened at `changeUtc`, so the quiet period elapses at
    /// a chosen moment - used to come back right after a relay-requested wait.
    private void MarkDirtyAt(DateTime changeUtc)
    {
        Interlocked.Exchange(ref lastChangeTicks, changeUtc.Ticks);
        Interlocked.Exchange(ref dirtySinceTicks, changeUtc.Ticks);
        Interlocked.Exchange(ref catalogDirty, 1);
    }

    // ---- Owner: hourly mailbox check ----

    private void TickOwnerChecks(DateTime now)
    {
        foreach (var pairing in config.Pairings)
        {
            if (pairing is not { Direction: PairingDirection.OwnerSide, IsPaired: true, PairIdHash.Length: > 0 })
                continue;
            var due = nextOwnerCheckUtc.GetOrAdd(pairing.Id, _ => now.AddSeconds(15));
            if (now < due) continue;
            nextOwnerCheckUtc[pairing.Id] = now + Hourly + Jitter();
            Plugin.FireAndForget(mailbox.CheckAsync(pairing, backgroundToken()));
        }
    }

    /// collar/catalog-sync: the Sync tab opening (`force` false - skipped if the last successful check was
    /// under a minute ago) and the "Check now" button (`force` true - no cooldown). Either way it also pushes
    /// the next scheduled check a full hour out, since this one just happened.
    public void RequestOwnerCheck(PairingState pairing, bool force)
    {
        if (pairing is not { Direction: PairingDirection.OwnerSide, IsPaired: true, PairIdHash.Length: > 0 } || mailbox.IsChecking(pairing.Id))
            return;
        var now = DateTime.UtcNow;
        if (!force && now - DateTimeOffset.FromUnixTimeSeconds(pairing.LastMailboxCheckOkUnixSeconds).UtcDateTime < OnDemandMinimumGap)
            return;
        nextOwnerCheckUtc[pairing.Id] = now + Hourly + Jitter();
        Plugin.FireAndForget(mailbox.CheckAsync(pairing, backgroundToken()));
    }
}
