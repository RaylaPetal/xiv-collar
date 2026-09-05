import type { Env } from "../env";
import { SIGNED_REQUEST_MAX_BYTES, TIMESTAMP_TOLERANCE_SECONDS, nowSeconds } from "./constants";
import { deviceKeyIdForPublicKey, verifyEcdsaSignature, type EcPublicKeyJwk } from "./crypto";
import { sha256Hex, toCanonicalJson } from "./json";
import { RelayError } from "./errors";
import { isEcPublicKeyJwk } from "./validate";

const NONCE_PATTERN = /^[A-Za-z0-9_-]{22}$/;
const DEVICE_KEY_ID_PATTERN = /^[0-9a-f]{64}$/;

export interface SignedRequestResult {
  deviceKeyId: string;
  publicKeyJwk: EcPublicKeyJwk;
  bodyText: string;
  bodyJson: unknown;
}

export type PublicKeyResolver = (deviceKeyId: string, bodyJson: unknown) => Promise<EcPublicKeyJwk | null>;

/**
 * Verifies the signed-request envelope described in
 * protocol/constants.json `requestSigning`. Every failure path returns the
 * same "unauthorized" RelayError so a caller cannot distinguish a bad
 * signature from an unknown device key or a replayed nonce (spec: "Relay is
 * capability-secured and replay-resistant").
 */
export async function verifySignedRequest(
  request: Request,
  env: Env,
  resolvePublicKey: PublicKeyResolver,
): Promise<SignedRequestResult> {
  const deviceKeyId = request.headers.get("x-relay-device-key-id");
  const timestampHeader = request.headers.get("x-relay-timestamp");
  const nonce = request.headers.get("x-relay-nonce");
  const signature = request.headers.get("x-relay-signature");

  if (!deviceKeyId || !timestampHeader || !nonce || !signature) {
    throw new RelayError("invalid_request");
  }
  if (!DEVICE_KEY_ID_PATTERN.test(deviceKeyId) || !NONCE_PATTERN.test(nonce)) {
    throw new RelayError("invalid_request");
  }

  const timestamp = Number.parseInt(timestampHeader, 10);
  if (!Number.isFinite(timestamp) || Math.abs(nowSeconds() - timestamp) > TIMESTAMP_TOLERANCE_SECONDS) {
    throw new RelayError("unauthorized");
  }

  const bodyText = await readBoundedBody(request, SIGNED_REQUEST_MAX_BYTES);
  let bodyJson: unknown = {};
  if (bodyText.length > 0) {
    try {
      bodyJson = JSON.parse(bodyText);
    } catch {
      throw new RelayError("invalid_request");
    }
  }
  const bodyDigest = await sha256Hex(toCanonicalJson(bodyJson));

  const url = new URL(request.url);
  const baseString = [request.method.toUpperCase(), url.pathname, bodyDigest, String(timestamp), nonce].join("\n");

  const publicKeyJwk = await resolvePublicKey(deviceKeyId, bodyJson);
  if (!publicKeyJwk) {
    throw new RelayError("unauthorized");
  }

  const expectedDeviceKeyId = await deviceKeyIdForPublicKey(publicKeyJwk);
  if (expectedDeviceKeyId !== deviceKeyId) {
    // The caller tried to substitute a different device key than the one on file.
    throw new RelayError("unauthorized");
  }

  const signatureOk = await verifyEcdsaSignature(publicKeyJwk, signature, baseString);
  if (!signatureOk) {
    throw new RelayError("unauthorized");
  }

  const consumed = await consumeNonce(env, deviceKeyId, nonce);
  if (!consumed) {
    throw new RelayError("unauthorized");
  }

  return { deviceKeyId, publicKeyJwk, bodyText, bodyJson };
}

async function readBoundedBody(request: Request, maxBytes: number): Promise<string> {
  const declaredLength = request.headers.get("content-length");
  if (declaredLength !== null && Number(declaredLength) > maxBytes) throw new RelayError("payload_too_large");
  if (!request.body) return "";

  const reader = request.body.getReader();
  const chunks: Uint8Array[] = [];
  let total = 0;
  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      total += value.byteLength;
      if (total > maxBytes) {
        await reader.cancel();
        throw new RelayError("payload_too_large");
      }
      chunks.push(value);
    }
  } finally {
    reader.releaseLock();
  }

  const bytes = new Uint8Array(total);
  let offset = 0;
  for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.byteLength; }
  try { return new TextDecoder("utf-8", { fatal: true, ignoreBOM: false }).decode(bytes); }
  catch { throw new RelayError("invalid_request"); }
}

/** Returns false if the nonce was already consumed for this device (replay). */
async function consumeNonce(env: Env, deviceKeyId: string, nonce: string): Promise<boolean> {
  const result = await env.RELAY_DB.prepare(
    `INSERT INTO nonces (device_key_id, nonce, seen_at) VALUES (?1, ?2, ?3)
     ON CONFLICT (device_key_id, nonce) DO NOTHING`,
  )
    .bind(deviceKeyId, nonce, nowSeconds())
    .run();
  return (result.meta.changes ?? 0) > 0;
}

/**
 * A request that establishes a new device identity (invitation creation, or
 * a catalog request's Owner ephemeral key exchange) has no prior stored key
 * to resolve against; it authenticates by proving possession of the private
 * key for the signing public key it asserts in its own body field, e.g.
 * `resolverFromBodyField("inviterPublicKey")`.
 */
export function resolverFromBodyField(fieldName: string): PublicKeyResolver {
  return async (_deviceKeyId, bodyJson) => {
    if (typeof bodyJson !== "object" || bodyJson === null) return null;
    const candidate = (bodyJson as Record<string, unknown>)[fieldName];
    return isEcPublicKeyJwk(candidate) ? candidate : null;
  };
}

/** For requests referencing a device that must already have proven itself before (accept, revocation, catalog upload/consume). */
export function resolverFromStoredDeviceKeys(env: Env): PublicKeyResolver {
  return async (deviceKeyId) => {
    const { lookupDeviceKey } = await import("./deviceKeys");
    return lookupDeviceKey(env, deviceKeyId);
  };
}
