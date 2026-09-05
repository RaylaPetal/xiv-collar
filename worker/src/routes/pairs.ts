import type { Env } from "../env";
import { resolverFromStoredDeviceKeys, verifySignedRequest } from "../lib/auth";
import { RelayError } from "../lib/errors";
import { isMemberOfPair, latestPair } from "../lib/pairs";
import { isHex64 } from "../lib/validate";

/**
 * Authenticated lookup of a pair's current epoch. pairIdHash itself is derivable locally by both
 * peers (SHA-256 of their sorted device key ids) with no server round trip, but the epoch is only
 * decided server-side when the inviter calls consume -- so the Accepter, who never calls consume,
 * has no other way to learn it. Membership-gated the same way checkRevocations is: only a device
 * key already on file as this pair's owner or sub may read it.
 */
export async function fetchPair(request: Request, env: Env, pairIdHash: string): Promise<Response> {
  if (!isHex64(pairIdHash)) throw new RelayError("not_found");
  const { deviceKeyId } = await verifySignedRequest(request, env, resolverFromStoredDeviceKeys(env));

  const pair = await latestPair(env, pairIdHash);
  if (!pair || !isMemberOfPair(pair, deviceKeyId)) {
    throw new RelayError("unauthorized");
  }

  return Response.json({
    type: "pair",
    schemaVersion: 1,
    pairIdHash: pair.pair_id_hash,
    pairEpoch: pair.pair_epoch,
    ownerDeviceKeyId: pair.owner_device_key_id,
    subDeviceKeyId: pair.sub_device_key_id,
    createdAt: pair.created_at,
    revokedAt: pair.revoked_at,
  });
}
