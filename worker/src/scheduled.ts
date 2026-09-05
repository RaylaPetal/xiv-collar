import type { Env } from "./env";
import { NONCE_REPLAY_WINDOW_SECONDS, nowSeconds } from "./lib/constants";
import { deleteCiphertext } from "./lib/r2";
import { logEvent } from "./lib/log";
import { QUOTA_LIMITS } from "./lib/quotas";

/**
 * Runs on the cron trigger configured in wrangler.toml (every 15 minutes).
 * Deletes everything past its documented expiry -- see
 * protocol/docs/threat-model.md for why each table exists and how long it
 * is retained -- and releases any pair's active-catalog-request slot that
 * expired without ever being uploaded.
 */
export async function runScheduledCleanup(env: Env): Promise<void> {
  const now = nowSeconds();

  const expiredInvitations = await env.RELAY_DB.prepare(
    `DELETE FROM invitations WHERE expires_at <= ?1 OR status = 'consumed'`,
  )
    .bind(now)
    .run();

  const expiredRevocations = await env.RELAY_DB.prepare(
    `DELETE FROM revocations WHERE expires_at <= ?1`,
  )
    .bind(now)
    .run();

  const staleActiveRequests = await env.RELAY_DB.prepare(
    `SELECT request_id_hash, pair_id_hash FROM catalog_requests WHERE status = 'pending' AND expires_at <= ?1`,
  )
    .bind(now)
    .all<{ request_id_hash: string; pair_id_hash: string }>();
  for (const row of staleActiveRequests.results) {
    await env.RELAY_DB.prepare(
      `UPDATE pair_cooldowns SET active_request_id_hash = NULL WHERE pair_id_hash = ?1 AND active_request_id_hash = ?2`,
    )
      .bind(row.pair_id_hash, row.request_id_hash)
      .run();
  }
  const removedPendingRequests = await env.RELAY_DB.prepare(
    `DELETE FROM catalog_requests WHERE status = 'pending' AND expires_at <= ?1`,
  )
    .bind(now)
    .run();

  const expiredObjects = await env.RELAY_DB.prepare(
    `SELECT request_id_hash, r2_key FROM catalog_objects WHERE expires_at <= ?1`,
  )
    .bind(now)
    .all<{ request_id_hash: string; r2_key: string }>();
  for (const row of expiredObjects.results) {
    await deleteCiphertext(env, row.r2_key);
    await env.RELAY_DB.prepare(`DELETE FROM catalog_objects WHERE request_id_hash = ?1`).bind(row.request_id_hash).run();
    await env.RELAY_DB.prepare(`DELETE FROM catalog_requests WHERE request_id_hash = ?1`).bind(row.request_id_hash).run();
  }

  const removedNonces = await env.RELAY_DB.prepare(
    `DELETE FROM nonces WHERE seen_at <= ?1`,
  )
    .bind(now - NONCE_REPLAY_WINDOW_SECONDS)
    .run();

  const maxWindowSeconds = Math.max(...Object.values(QUOTA_LIMITS).map((limit) => limit.windowSeconds));
  const removedQuotaCounters = await env.RELAY_DB.prepare(
    `DELETE FROM quota_counters WHERE window_start <= ?1`,
  )
    .bind(now - maxWindowSeconds)
    .run();

  await sweepOrphanCatalogObjects(env);

  logEvent("scheduled_cleanup_complete", {
    expiredInvitations: expiredInvitations.meta.changes ?? 0,
    expiredRevocations: expiredRevocations.meta.changes ?? 0,
    removedPendingRequests: removedPendingRequests.meta.changes ?? 0,
    expiredObjects: expiredObjects.results.length,
    removedNonces: removedNonces.meta.changes ?? 0,
    removedQuotaCounters: removedQuotaCounters.meta.changes ?? 0,
  });

  await checkQuotaAlarm(env);
}

/**
 * Best-effort: catches an R2 object left behind by a crash between the R2
 * put and the D1 insert that references it (or the reverse on delete). A
 * missed sweep is not a correctness problem -- the object still expires and
 * is deleted on a later run -- so this is bounded per invocation rather than
 * paginating the whole bucket.
 */
async function sweepOrphanCatalogObjects(env: Env): Promise<void> {
  const listed = await env.RELAY_CATALOG_BUCKET.list({ prefix: "catalog/", limit: 200 });
  for (const object of listed.objects) {
    const requestIdHash = object.key.slice("catalog/".length);
    const row = await env.RELAY_DB.prepare(`SELECT 1 FROM catalog_objects WHERE request_id_hash = ?1`)
      .bind(requestIdHash)
      .first();
    if (!row) {
      await deleteCiphertext(env, object.key);
    }
  }
}

async function checkQuotaAlarm(env: Env): Promise<void> {
  for (const name of ["globalDailyWork", "globalDailySafety"] as const) {
    const limit = QUOTA_LIMITS[name];
    const bucket = Math.floor(nowSeconds() / limit.windowSeconds) * limit.windowSeconds;
    const row = await env.RELAY_DB.prepare(`SELECT count FROM quota_counters WHERE scope = ?1 AND window_start = ?2`)
      .bind(`${name}:all`, bucket)
      .first<{ count: number }>();
    const usageRatio = (row?.count ?? 0) / limit.maxCount;
    if (usageRatio >= 0.8) {
      logEvent("quota_alarm", { scope: name, usageRatioPercent: Math.round(usageRatio * 100) });
    }
  }
}
