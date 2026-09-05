import type { Env } from "../env";
import { sha256Hex, toCanonicalJson } from "./json";

/**
 * Deterministic so both peers can compute it locally without a server round
 * trip: the SHA-256 of the two device key ids, sorted so order never matters.
 */
export async function computePairIdHash(deviceKeyIdA: string, deviceKeyIdB: string): Promise<string> {
  const [a, b] = [deviceKeyIdA, deviceKeyIdB].sort();
  return sha256Hex(toCanonicalJson({ a, b }));
}

export interface PairRow {
  pair_id_hash: string;
  pair_epoch: number;
  owner_device_key_id: string;
  sub_device_key_id: string;
  created_at: number;
  revoked_at: number | null;
}

export async function latestPair(env: Env, pairIdHash: string): Promise<PairRow | null> {
  return env.RELAY_DB.prepare(
    `SELECT * FROM pairs WHERE pair_id_hash = ?1 ORDER BY pair_epoch DESC LIMIT 1`,
  )
    .bind(pairIdHash)
    .first<PairRow>();
}

export function isMemberOfPair(pair: PairRow, deviceKeyId: string): boolean {
  return pair.owner_device_key_id === deviceKeyId || pair.sub_device_key_id === deviceKeyId;
}
