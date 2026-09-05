import type { Env } from "../env";
import { nowSeconds } from "../lib/constants";
import { QUOTA_LIMITS } from "../lib/quotas";

/**
 * Aggregate-only: counts and quota pressure, never a payload, key, or
 * character identity. Suitable for an unauthenticated operator dashboard.
 */
export async function health(env: Env): Promise<Response> {
  const now = nowSeconds();
  const [activePairs, pendingInvitations, activeCatalogRequests] = await Promise.all([
    env.RELAY_DB.prepare(`SELECT COUNT(*) AS n FROM pairs WHERE revoked_at IS NULL`).first<{ n: number }>(),
    env.RELAY_DB.prepare(`SELECT COUNT(*) AS n FROM invitations WHERE status = 'pending' AND expires_at > ?1`)
      .bind(now)
      .first<{ n: number }>(),
    env.RELAY_DB.prepare(`SELECT COUNT(*) AS n FROM catalog_requests WHERE status IN ('pending', 'uploaded')`).first<{ n: number }>(),
  ]);

  const globalLimit = QUOTA_LIMITS.globalDailyWork;
  const safetyLimit = QUOTA_LIMITS.globalDailySafety;
  const bucket = Math.floor(now / globalLimit.windowSeconds) * globalLimit.windowSeconds;
  const [globalUsage, safetyUsage] = await Promise.all([
    env.RELAY_DB.prepare(`SELECT count FROM quota_counters WHERE scope = ?1 AND window_start = ?2`)
      .bind("globalDailyWork:all", bucket).first<{ count: number }>(),
    env.RELAY_DB.prepare(`SELECT count FROM quota_counters WHERE scope = ?1 AND window_start = ?2`)
      .bind("globalDailySafety:all", bucket).first<{ count: number }>(),
  ]);

  return Response.json({
    status: "ok",
    environment: env.RELAY_ENVIRONMENT,
    circuitBreakerForcedOpen: env.CIRCUIT_BREAKER_FORCE_OPEN === "true",
    activePairs: activePairs?.n ?? 0,
    pendingInvitations: pendingInvitations?.n ?? 0,
    activeCatalogRequests: activeCatalogRequests?.n ?? 0,
    globalDailyWorkUsage: globalUsage?.count ?? 0,
    globalDailyWorkCeiling: globalLimit.maxCount,
    globalDailySafetyUsage: safetyUsage?.count ?? 0,
    globalDailySafetyCeiling: safetyLimit.maxCount,
  });
}
