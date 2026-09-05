import type { Env } from "../env";
import { resolverFromStoredDeviceKeys, verifySignedRequest } from "../lib/auth";
import { base64UrlToBytes, bytesToBase64Url } from "../lib/base64";
import { capabilityHash, isValidCapabilityShape } from "../lib/capability";
import { CATALOG_COOLDOWN_SECONDS, CATALOG_OBJECT_EXPIRY_SECONDS, CATALOG_REQUEST_EXPIRY_SECONDS, nowSeconds } from "../lib/constants";
import { verifyEcdsaSignature, type EcPublicKeyJwk } from "../lib/crypto";
import { RelayError } from "../lib/errors";
import { sha256Hex, toCanonicalJson } from "../lib/json";
import { latestPair } from "../lib/pairs";
import { lookupDeviceKey } from "../lib/deviceKeys";
import { assertCircuitBreakerClosed, enforceQuota } from "../lib/quotas";
import { deleteCiphertext, getCiphertext, putCiphertext, r2KeyForRequest } from "../lib/r2";
import {
  asRecord,
  isAeadNonce,
  isEcPublicKeyJwk,
  isHex64,
  isNonNegInt,
  isSignature,
  isUnixSeconds,
  requireField,
} from "../lib/validate";

interface CatalogRequestRow {
  request_id_hash: string;
  pair_id_hash: string;
  pair_epoch: number;
  requester_device_key_id: string;
  owner_ephemeral_public_key_jwk: string;
  created_at: number;
  expires_at: number;
  status: "pending" | "uploaded" | "consumed" | "expired";
}

interface CatalogRequestEnvelope {
  type: "catalog-request";
  schemaVersion: 1;
  pairIdHash: string;
  pairEpoch: number;
  requestId: string;
  requesterDeviceKeyId: string;
  ownerEphemeralPublicKey: EcPublicKeyJwk;
  createdAt: number;
  expiresAt: number;
  signature: string;
}

interface CatalogResponseEnvelope {
  type: "catalog-response";
  schemaVersion: 1;
  pairIdHash: string;
  pairEpoch: number;
  requestId: string;
  snapshotId: number;
  senderDeviceKeyId: string;
  recipientDeviceKeyId: string;
  createdAt: number;
  expiresAt: number;
  algorithm: "ECDH-P256+HKDF-SHA256+AES-256-GCM";
  ciphertextDigest: string;
  ciphertextSizeBytes: number;
  nonce: string;
  senderEphemeralPublicKey: EcPublicKeyJwk;
  signature: string;
}

async function verifyEnvelope<T extends { signature: string }>(envelope: T, publicKeyJwk: EcPublicKeyJwk): Promise<boolean> {
  const { signature, ...unsigned } = envelope;
  return verifyEcdsaSignature(publicKeyJwk, signature, toCanonicalJson(unsigned));
}

/** Owner-only: explicit refresh request. Cooldown and one-active-request-per-pair are enforced by a single atomic UPDATE. */
export async function createCatalogRequest(request: Request, env: Env): Promise<Response> {
  await assertCircuitBreakerClosed(env);
  const { deviceKeyId, bodyJson } = await verifySignedRequest(request, env, resolverFromStoredDeviceKeys(env));
  await enforceQuota(env, "endpointGlobal", "catalog.requests.create");
  await enforceQuota(env, "deviceCatalogRequestCreate", deviceKeyId);

  const body = asRecord(bodyJson);
  const envelope: CatalogRequestEnvelope = {
    type: "catalog-request",
    schemaVersion: 1,
    pairIdHash: requireField(body, "pairIdHash", isHex64),
    pairEpoch: requireField(body, "pairEpoch", isNonNegInt),
    requestId: requireField(body, "requestId", (v): v is string => typeof v === "string" && isValidCapabilityShape(v)),
    requesterDeviceKeyId: requireField(body, "requesterDeviceKeyId", isHex64),
    ownerEphemeralPublicKey: requireField(body, "ownerEphemeralPublicKey", isEcPublicKeyJwk),
    createdAt: requireField(body, "createdAt", isUnixSeconds),
    expiresAt: requireField(body, "expiresAt", isUnixSeconds),
    signature: requireField(body, "signature", isSignature),
  };

  if (envelope.requesterDeviceKeyId !== deviceKeyId) throw new RelayError("unauthorized");
  if (envelope.expiresAt - envelope.createdAt > CATALOG_REQUEST_EXPIRY_SECONDS) throw new RelayError("invalid_request");
  const now = nowSeconds();
  if (envelope.expiresAt <= now) throw new RelayError("invalid_request");

  const pair = await latestPair(env, envelope.pairIdHash);
  if (!pair || pair.pair_epoch !== envelope.pairEpoch || pair.owner_device_key_id !== deviceKeyId || pair.revoked_at !== null) {
    throw new RelayError("unauthorized");
  }

  const publicKeyJwk = await lookupDeviceKey(env, deviceKeyId);
  if (!publicKeyJwk || !(await verifyEnvelope(envelope, publicKeyJwk))) {
    throw new RelayError("unauthorized");
  }

  const requestIdHash = await capabilityHash(envelope.requestId);

  await env.RELAY_DB.prepare(
    `INSERT INTO pair_cooldowns (pair_id_hash, last_accepted_sync_at, last_snapshot_id, active_request_id_hash)
     VALUES (?1, 0, 0, NULL) ON CONFLICT (pair_id_hash) DO NOTHING`,
  )
    .bind(envelope.pairIdHash)
    .run();

  const claim = await env.RELAY_DB.prepare(
    `UPDATE pair_cooldowns SET active_request_id_hash = ?1
     WHERE pair_id_hash = ?2 AND active_request_id_hash IS NULL AND (?3 - last_accepted_sync_at) >= ?4`,
  )
    .bind(requestIdHash, envelope.pairIdHash, now, CATALOG_COOLDOWN_SECONDS)
    .run();

  if ((claim.meta.changes ?? 0) === 0) {
    const cooldownRow = await env.RELAY_DB.prepare(
      `SELECT last_accepted_sync_at FROM pair_cooldowns WHERE pair_id_hash = ?1`,
    )
      .bind(envelope.pairIdHash)
      .first<{ last_accepted_sync_at: number }>();
    const remaining = cooldownRow ? CATALOG_COOLDOWN_SECONDS - (now - cooldownRow.last_accepted_sync_at) : CATALOG_COOLDOWN_SECONDS;
    throw new RelayError("cooldown_active", Math.max(remaining, 1));
  }

  await env.RELAY_DB.prepare(
    `INSERT INTO catalog_requests (request_id_hash, pair_id_hash, pair_epoch, requester_device_key_id, owner_ephemeral_public_key_jwk, created_at, expires_at)
     VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7)`,
  )
    .bind(
      requestIdHash,
      envelope.pairIdHash,
      envelope.pairEpoch,
      envelope.requesterDeviceKeyId,
      JSON.stringify(envelope.ownerEphemeralPublicKey),
      envelope.createdAt,
      envelope.expiresAt,
    )
    .run();

  return Response.json(envelope);
}

async function loadCatalogRequest(env: Env, requestId: string): Promise<{ row: CatalogRequestRow; requestIdHash: string }> {
  if (!isValidCapabilityShape(requestId)) throw new RelayError("not_found");
  const requestIdHash = await capabilityHash(requestId);
  const row = await env.RELAY_DB.prepare(`SELECT * FROM catalog_requests WHERE request_id_hash = ?1`)
    .bind(requestIdHash)
    .first<CatalogRequestRow>();
  if (!row) throw new RelayError("not_found");
  return { row, requestIdHash };
}

/** Read-only: capability-gated, used by both the Sub (to learn what to encrypt to) and the Owner (to poll status). */
export async function fetchCatalogRequest(request: Request, env: Env, requestId: string): Promise<Response> {
  const { row } = await loadCatalogRequest(env, requestId);

  return Response.json({
    type: "catalog-request",
    schemaVersion: 1,
    pairIdHash: row.pair_id_hash,
    pairEpoch: row.pair_epoch,
    requestId,
    requesterDeviceKeyId: row.requester_device_key_id,
    ownerEphemeralPublicKey: JSON.parse(row.owner_ephemeral_public_key_jwk),
    createdAt: row.created_at,
    expiresAt: row.expires_at,
    status: row.status,
  });
}

/** Sub-only mutation: builds, encrypts, and uploads exactly once per request. */
export async function uploadCatalogResponse(request: Request, env: Env, requestId: string): Promise<Response> {
  const { row, requestIdHash } = await loadCatalogRequest(env, requestId);
  if (row.status !== "pending" || row.expires_at <= nowSeconds()) throw new RelayError("expired");

  const pair = await latestPair(env, row.pair_id_hash);
  if (!pair || pair.pair_epoch !== row.pair_epoch || pair.revoked_at !== null) throw new RelayError("expired");

  const { deviceKeyId, bodyJson } = await verifySignedRequest(request, env, resolverFromStoredDeviceKeys(env));
  if (deviceKeyId !== pair.sub_device_key_id) throw new RelayError("unauthorized");

  const body = asRecord(bodyJson);
  const envelopeField = body.envelope;
  if (typeof envelopeField !== "object" || envelopeField === null) throw new RelayError("invalid_request");
  const e = asRecord(envelopeField);

  const envelope: CatalogResponseEnvelope = {
    type: "catalog-response",
    schemaVersion: 1,
    pairIdHash: requireField(e, "pairIdHash", isHex64),
    pairEpoch: requireField(e, "pairEpoch", isNonNegInt),
    requestId: requireField(e, "requestId", (v): v is string => v === requestId),
    snapshotId: requireField(e, "snapshotId", isNonNegInt),
    senderDeviceKeyId: requireField(e, "senderDeviceKeyId", isHex64),
    recipientDeviceKeyId: requireField(e, "recipientDeviceKeyId", isHex64),
    createdAt: requireField(e, "createdAt", isUnixSeconds),
    expiresAt: requireField(e, "expiresAt", isUnixSeconds),
    algorithm: requireField(e, "algorithm", (v): v is "ECDH-P256+HKDF-SHA256+AES-256-GCM" => v === "ECDH-P256+HKDF-SHA256+AES-256-GCM"),
    ciphertextDigest: requireField(e, "ciphertextDigest", isHex64),
    ciphertextSizeBytes: requireField(e, "ciphertextSizeBytes", isNonNegInt),
    nonce: requireField(e, "nonce", isAeadNonce),
    senderEphemeralPublicKey: requireField(e, "senderEphemeralPublicKey", isEcPublicKeyJwk),
    signature: requireField(e, "signature", isSignature),
  };

  if (envelope.expiresAt - envelope.createdAt > CATALOG_OBJECT_EXPIRY_SECONDS || envelope.expiresAt <= nowSeconds()) {
    // Server-enforced hard cap: a Sub cannot make its own uploaded ciphertext outlive the 15-minute retention window.
    throw new RelayError("invalid_request");
  }
  if (envelope.pairIdHash !== row.pair_id_hash || envelope.pairEpoch !== row.pair_epoch) throw new RelayError("invalid_request");
  if (envelope.senderDeviceKeyId !== deviceKeyId || envelope.recipientDeviceKeyId !== pair.owner_device_key_id) {
    throw new RelayError("unauthorized");
  }

  const publicKeyJwk = await lookupDeviceKey(env, deviceKeyId);
  if (!publicKeyJwk || !(await verifyEnvelope(envelope, publicKeyJwk))) throw new RelayError("unauthorized");

  const ciphertextBase64Url = body.ciphertextBase64Url;
  if (typeof ciphertextBase64Url !== "string") throw new RelayError("invalid_request");
  const ciphertextBytes = base64UrlToBytes(ciphertextBase64Url);
  if (ciphertextBytes.byteLength !== envelope.ciphertextSizeBytes) throw new RelayError("invalid_request");
  await enforceQuota(env, "catalogUploadBytes", deviceKeyId, ciphertextBytes.byteLength);

  const actualDigest = await sha256Hex(ciphertextBytes.buffer as ArrayBuffer);
  if (actualDigest !== envelope.ciphertextDigest) throw new RelayError("invalid_request");

  const cooldownGuard = await env.RELAY_DB.prepare(
    `UPDATE pair_cooldowns SET last_accepted_sync_at = ?1, active_request_id_hash = NULL, last_snapshot_id = ?2
     WHERE pair_id_hash = ?3 AND last_snapshot_id < ?2`,
  )
    .bind(nowSeconds(), envelope.snapshotId, row.pair_id_hash)
    .run();
  if ((cooldownGuard.meta.changes ?? 0) === 0) throw new RelayError("invalid_request");

  const claimResult = await env.RELAY_DB.prepare(
    `UPDATE catalog_requests SET status = 'uploaded' WHERE request_id_hash = ?1 AND status = 'pending'`,
  )
    .bind(requestIdHash)
    .run();
  if ((claimResult.meta.changes ?? 0) === 0) throw new RelayError("expired");

  const r2Key = r2KeyForRequest(requestIdHash);
  await putCiphertext(env, r2Key, ciphertextBytes);

  await env.RELAY_DB.prepare(
    `INSERT INTO catalog_objects (request_id_hash, r2_key, sender_device_key_id, recipient_device_key_id, snapshot_id, ciphertext_digest, ciphertext_size_bytes, nonce, sender_ephemeral_public_key_jwk, algorithm, created_at, expires_at)
     VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11, ?12)`,
  )
    .bind(
      requestIdHash,
      r2Key,
      envelope.senderDeviceKeyId,
      envelope.recipientDeviceKeyId,
      envelope.snapshotId,
      envelope.ciphertextDigest,
      envelope.ciphertextSizeBytes,
      envelope.nonce,
      JSON.stringify(envelope.senderEphemeralPublicKey),
      envelope.algorithm,
      envelope.createdAt,
      envelope.expiresAt,
    )
    .run();

  return Response.json(envelope);
}

/** Owner-only mutation: one-use retrieval, eagerly deletes the R2 object on success. */
export async function consumeCatalogResponse(request: Request, env: Env, requestId: string): Promise<Response> {
  const { row, requestIdHash } = await loadCatalogRequest(env, requestId);
  const { deviceKeyId } = await verifySignedRequest(request, env, resolverFromStoredDeviceKeys(env));

  if (deviceKeyId !== row.requester_device_key_id) throw new RelayError("unauthorized");
  if (row.status !== "uploaded") throw new RelayError("not_found");

  const claim = await env.RELAY_DB.prepare(
    `UPDATE catalog_requests SET status = 'consumed' WHERE request_id_hash = ?1 AND status = 'uploaded'`,
  )
    .bind(requestIdHash)
    .run();
  if ((claim.meta.changes ?? 0) === 0) {
    // Already consumed by a prior call: one-use retrieval, fail closed rather than serve it twice.
    throw new RelayError("not_found");
  }

  const objectRow = await env.RELAY_DB.prepare(`SELECT * FROM catalog_objects WHERE request_id_hash = ?1`)
    .bind(requestIdHash)
    .first<{
      r2_key: string;
      sender_device_key_id: string;
      recipient_device_key_id: string;
      snapshot_id: number;
      ciphertext_digest: string;
      ciphertext_size_bytes: number;
      nonce: string;
      sender_ephemeral_public_key_jwk: string;
      algorithm: string;
      created_at: number;
      expires_at: number;
    }>();
  if (!objectRow) throw new RelayError("not_found");

  const ciphertextBytes = await getCiphertext(env, objectRow.r2_key);
  await deleteCiphertext(env, objectRow.r2_key);
  await env.RELAY_DB.prepare(`UPDATE catalog_objects SET consumed_at = ?1 WHERE request_id_hash = ?2`)
    .bind(nowSeconds(), requestIdHash)
    .run();

  if (!ciphertextBytes) throw new RelayError("not_found");

  return Response.json({
    envelope: {
      type: "catalog-response",
      schemaVersion: 1,
      pairIdHash: row.pair_id_hash,
      pairEpoch: row.pair_epoch,
      requestId,
      snapshotId: objectRow.snapshot_id,
      senderDeviceKeyId: objectRow.sender_device_key_id,
      recipientDeviceKeyId: objectRow.recipient_device_key_id,
      createdAt: objectRow.created_at,
      expiresAt: objectRow.expires_at,
      algorithm: objectRow.algorithm,
      ciphertextDigest: objectRow.ciphertext_digest,
      ciphertextSizeBytes: objectRow.ciphertext_size_bytes,
      nonce: objectRow.nonce,
      senderEphemeralPublicKey: JSON.parse(objectRow.sender_ephemeral_public_key_jwk),
    },
    ciphertextBase64Url: bytesToBase64Url(ciphertextBytes),
  });
}
