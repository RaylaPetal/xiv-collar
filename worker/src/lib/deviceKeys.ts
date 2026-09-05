import type { Env } from "../env";
import { nowSeconds } from "./constants";
import { deviceKeyIdForPublicKey, type EcPublicKeyJwk } from "./crypto";
import { toCanonicalJson } from "./json";

/** Idempotently records a device's signing public key the first time it is proven. */
export async function rememberDeviceKey(env: Env, publicKeyJwk: EcPublicKeyJwk): Promise<string> {
  const deviceKeyId = await deviceKeyIdForPublicKey(publicKeyJwk);
  await env.RELAY_DB.prepare(
    `INSERT INTO device_keys (device_key_id, public_key_jwk, first_seen_at) VALUES (?1, ?2, ?3)
     ON CONFLICT (device_key_id) DO NOTHING`,
  )
    .bind(deviceKeyId, toCanonicalJson(publicKeyJwk), nowSeconds())
    .run();
  return deviceKeyId;
}

export async function lookupDeviceKey(env: Env, deviceKeyId: string): Promise<EcPublicKeyJwk | null> {
  const row = await env.RELAY_DB.prepare(`SELECT public_key_jwk FROM device_keys WHERE device_key_id = ?1`)
    .bind(deviceKeyId)
    .first<{ public_key_jwk: string }>();
  return row ? (JSON.parse(row.public_key_jwk) as EcPublicKeyJwk) : null;
}
