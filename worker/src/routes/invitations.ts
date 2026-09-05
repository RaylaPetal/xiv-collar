import type { Env } from "../env";
import { resolverFromBodyField, resolverFromStoredDeviceKeys, verifySignedRequest } from "../lib/auth";
import { capabilityHash, isValidCapabilityShape } from "../lib/capability";
import { assertCircuitBreakerClosed, enforceQuota } from "../lib/quotas";
import { INVITATION_EXPIRY_SECONDS, nowSeconds } from "../lib/constants";
import { verifyEcdsaSignature, type EcPublicKeyJwk } from "../lib/crypto";
import { toCanonicalJson } from "../lib/json";
import { rememberDeviceKey } from "../lib/deviceKeys";
import { RelayError } from "../lib/errors";
import {
  asRecord,
  isEcPublicKeyJwk,
  isHex64,
  isProofDigestHex,
  isRole,
  isSignature,
  isUnixSeconds,
  requireField,
} from "../lib/validate";
import { computePairIdHash } from "../lib/pairs";

interface InvitationRow {
  invitation_id_hash: string;
  inviter_device_key_id: string;
  inviter_public_key_jwk: string;
  role: "owner" | "sub";
  trigger_phrase: string | null;
  created_at: number;
  expires_at: number;
  signature: string;
  status: "pending" | "accepted" | "consumed" | "expired";
  accepter_device_key_id: string | null;
  accepter_public_key_jwk: string | null;
  proof_digest: string | null;
  accepter_created_at: number | null;
  accepter_expires_at: number | null;
  accepter_signature: string | null;
  accepter_role: "owner" | "sub" | null;
  accepter_trigger_phrase: string | null;
  accepted_at: number | null;
  consumed_at: number | null;
}

interface InvitationEnvelope {
  type: "invitation";
  schemaVersion: 1;
  invitationId: string;
  inviterDeviceKeyId: string;
  inviterPublicKey: EcPublicKeyJwk;
  role: "owner" | "sub";
  triggerPhrase?: string;
  createdAt: number;
  expiresAt: number;
  signature: string;
}

interface AcceptanceEnvelope {
  type: "acceptance";
  schemaVersion: 1;
  invitationId: string;
  accepterDeviceKeyId: string;
  accepterPublicKey: EcPublicKeyJwk;
  proofDigest: string;
  role?: "owner" | "sub";
  triggerPhrase?: string;
  createdAt: number;
  expiresAt: number;
  signature: string;
}

async function verifyEnvelope<T extends { signature: string }>(envelope: T, publicKeyJwk: EcPublicKeyJwk): Promise<boolean> {
  const { signature, ...unsigned } = envelope;
  return verifyEcdsaSignature(publicKeyJwk, signature, toCanonicalJson(unsigned));
}

/**
 * Mutation: the inviter chooses its own high-entropy invitationId (same reason catalog-request lets the
 * caller choose requestId -- the signed content must include it, so the server can't generate it after the
 * fact) and signs the complete invitation envelope. The signature is stored so anyone who later fetches
 * this row (including the receiver, who has no live signing context to trust) can verify it directly
 * against inviterPublicKey without trusting the relay operator alone (design.md: "fetches the invitation,
 * verifies its signature").
 */
export async function createInvitation(request: Request, env: Env): Promise<Response> {
  await assertCircuitBreakerClosed(env);
  const { deviceKeyId, bodyJson } = await verifySignedRequest(request, env, resolverFromBodyField("inviterPublicKey"));
  await enforceQuota(env, "endpointGlobal", "invitations.create");
  await enforceQuota(env, "deviceInvitationCreate", deviceKeyId);

  const body = asRecord(bodyJson);
  const envelope: InvitationEnvelope = {
    type: "invitation",
    schemaVersion: 1,
    invitationId: requireField(body, "invitationId", (v): v is string => typeof v === "string" && isValidCapabilityShape(v)),
    inviterDeviceKeyId: requireField(body, "inviterDeviceKeyId", isHex64),
    inviterPublicKey: requireField(body, "inviterPublicKey", isEcPublicKeyJwk),
    role: requireField(body, "role", isRole),
    triggerPhrase: typeof body.triggerPhrase === "string" && body.triggerPhrase.trim().length > 0 && body.triggerPhrase.length <= 32
      ? body.triggerPhrase.trim()
      : undefined,
    createdAt: requireField(body, "createdAt", isUnixSeconds),
    expiresAt: requireField(body, "expiresAt", isUnixSeconds),
    signature: requireField(body, "signature", isSignature),
  };

  if (envelope.inviterDeviceKeyId !== deviceKeyId) throw new RelayError("unauthorized");
  if (envelope.expiresAt - envelope.createdAt > INVITATION_EXPIRY_SECONDS || envelope.expiresAt <= nowSeconds()) {
    throw new RelayError("invalid_request");
  }
  if (!(await verifyEnvelope(envelope, envelope.inviterPublicKey))) {
    throw new RelayError("unauthorized");
  }

  await rememberDeviceKey(env, envelope.inviterPublicKey);
  const invitationIdHash = await capabilityHash(envelope.invitationId);

  try {
    await env.RELAY_DB.prepare(
      `INSERT INTO invitations (invitation_id_hash, inviter_device_key_id, inviter_public_key_jwk, role, trigger_phrase, created_at, expires_at, signature)
       VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8)`,
    )
      .bind(invitationIdHash, deviceKeyId, JSON.stringify(envelope.inviterPublicKey), envelope.role, envelope.triggerPhrase ?? null, envelope.createdAt, envelope.expiresAt, envelope.signature)
      .run();
  } catch {
    // Primary-key conflict: this invitationId was already used (by this device or, vanishingly unlikely
    // given 256 bits of entropy, another). Either way the caller must pick a new one.
    throw new RelayError("invalid_request");
  }

  return Response.json(envelope);
}

async function loadInvitation(env: Env, invitationId: string): Promise<InvitationRow> {
  if (!isValidCapabilityShape(invitationId)) throw new RelayError("not_found");
  const invitationIdHash = await capabilityHash(invitationId);
  const row = await env.RELAY_DB.prepare(`SELECT * FROM invitations WHERE invitation_id_hash = ?1`)
    .bind(invitationIdHash)
    .first<InvitationRow>();
  if (!row) throw new RelayError("not_found");
  if (row.expires_at <= nowSeconds() && row.status !== "consumed") throw new RelayError("expired");
  return row;
}

/**
 * Read-only: the capability id in the path is itself the proof of possession (spec: reads require only the
 * capability). Includes the stored signature(s) so the caller can verify authenticity itself instead of
 * trusting this response at face value.
 */
export async function fetchInvitation(request: Request, env: Env, invitationId: string): Promise<Response> {
  const row = await loadInvitation(env, invitationId);

  const body: Record<string, unknown> = {
    type: "invitation",
    schemaVersion: 1,
    invitationId,
    inviterDeviceKeyId: row.inviter_device_key_id,
    inviterPublicKey: JSON.parse(row.inviter_public_key_jwk),
    role: row.role,
    ...(row.trigger_phrase ? { triggerPhrase: row.trigger_phrase } : {}),
    createdAt: row.created_at,
    expiresAt: row.expires_at,
    signature: row.signature,
    status: row.status,
  };

  if (row.accepter_device_key_id && row.accepter_public_key_jwk && row.proof_digest && row.accepter_signature) {
    body.acceptance = {
      type: "acceptance",
      schemaVersion: 1,
      invitationId,
      accepterDeviceKeyId: row.accepter_device_key_id,
      accepterPublicKey: JSON.parse(row.accepter_public_key_jwk),
      proofDigest: row.proof_digest,
      ...(row.accepter_role ? { role: row.accepter_role } : {}),
      ...(row.accepter_trigger_phrase ? { triggerPhrase: row.accepter_trigger_phrase } : {}),
      createdAt: row.accepter_created_at,
      expiresAt: row.accepter_expires_at,
      signature: row.accepter_signature,
    };
  }

  return Response.json(body);
}

/**
 * Mutation: the receiver's explicit Accept. Persists no active pair; only marks the invitation accepted.
 * The acceptance envelope itself is signed by the accepter and stored so the inviter can verify it later
 * from a plain fetch (see fetchInvitation) without needing to have witnessed this request.
 */
export async function acceptInvitation(request: Request, env: Env, invitationId: string): Promise<Response> {
  if (!isValidCapabilityShape(invitationId)) throw new RelayError("not_found");
  const { deviceKeyId, bodyJson } = await verifySignedRequest(request, env, resolverFromBodyField("accepterPublicKey"));
  await enforceQuota(env, "endpointGlobal", "invitations.accept");

  const body = asRecord(bodyJson);
  const envelope: AcceptanceEnvelope = {
    type: "acceptance",
    schemaVersion: 1,
    invitationId: requireField(body, "invitationId", (v): v is string => v === invitationId),
    accepterDeviceKeyId: requireField(body, "accepterDeviceKeyId", isHex64),
    accepterPublicKey: requireField(body, "accepterPublicKey", isEcPublicKeyJwk),
    proofDigest: requireField(body, "proofDigest", isProofDigestHex),
    role: isRole(body.role) ? body.role : undefined,
    triggerPhrase: typeof body.triggerPhrase === "string" && body.triggerPhrase.trim().length > 0 && body.triggerPhrase.length <= 32
      ? body.triggerPhrase.trim()
      : undefined,
    createdAt: requireField(body, "createdAt", isUnixSeconds),
    expiresAt: requireField(body, "expiresAt", isUnixSeconds),
    signature: requireField(body, "signature", isSignature),
  };

  if (envelope.accepterDeviceKeyId !== deviceKeyId) throw new RelayError("unauthorized");
  if (envelope.expiresAt - envelope.createdAt > INVITATION_EXPIRY_SECONDS || envelope.expiresAt <= nowSeconds()) {
    throw new RelayError("invalid_request");
  }
  if (!(await verifyEnvelope(envelope, envelope.accepterPublicKey))) {
    throw new RelayError("unauthorized");
  }
  const invitation = await loadInvitation(env, invitationId);
  if (envelope.role && envelope.role === invitation.role) throw new RelayError("invalid_request");

  await rememberDeviceKey(env, envelope.accepterPublicKey);

  const invitationIdHash = await capabilityHash(invitationId);
  const now = nowSeconds();

  const result = await env.RELAY_DB.prepare(
    `UPDATE invitations
     SET status = 'accepted', accepter_device_key_id = ?1, accepter_public_key_jwk = ?2, proof_digest = ?3,
         accepter_created_at = ?4, accepter_expires_at = ?5, accepter_signature = ?6,
         accepter_role = ?7, accepter_trigger_phrase = ?8, accepted_at = ?9
     WHERE invitation_id_hash = ?10 AND status = 'pending' AND expires_at > ?9`,
  )
    .bind(
      deviceKeyId,
      JSON.stringify(envelope.accepterPublicKey),
      envelope.proofDigest,
      envelope.createdAt,
      envelope.expiresAt,
      envelope.signature,
      envelope.role ?? null,
      envelope.triggerPhrase ?? null,
      now,
      invitationIdHash,
    )
    .run();

  if ((result.meta.changes ?? 0) === 0) {
    // Reused, expired, or never existed -- fails closed identically either way.
    throw new RelayError("expired");
  }

  return Response.json(envelope);
}

/** Mutation: the inviter's final activation, only after it independently verified the acknowledgement tell. */
export async function consumeInvitation(request: Request, env: Env, invitationId: string): Promise<Response> {
  if (!isValidCapabilityShape(invitationId)) throw new RelayError("not_found");
  const { deviceKeyId } = await verifySignedRequest(request, env, resolverFromStoredDeviceKeys(env));
  await enforceQuota(env, "endpointGlobal", "invitations.consume");

  const invitationIdHash = await capabilityHash(invitationId);
  const row = await env.RELAY_DB.prepare(`SELECT * FROM invitations WHERE invitation_id_hash = ?1`)
    .bind(invitationIdHash)
    .first<InvitationRow>();

  if (!row || row.status !== "accepted" || row.inviter_device_key_id !== deviceKeyId || !row.accepter_device_key_id) {
    throw new RelayError("expired");
  }

  const now = nowSeconds();
  const pairIdHash = await computePairIdHash(row.inviter_device_key_id, row.accepter_device_key_id);
  const isInviterOwner = row.role === "owner";
  const ownerDeviceKeyId = isInviterOwner ? row.inviter_device_key_id : row.accepter_device_key_id;
  const subDeviceKeyId = isInviterOwner ? row.accepter_device_key_id : row.inviter_device_key_id;

  const existingEpochRow = await env.RELAY_DB.prepare(
    `SELECT MAX(pair_epoch) AS max_epoch FROM pairs WHERE pair_id_hash = ?1`,
  )
    .bind(pairIdHash)
    .first<{ max_epoch: number | null }>();
  const pairEpoch = (existingEpochRow?.max_epoch ?? -1) + 1;

  const consumeResult = await env.RELAY_DB.prepare(
    `UPDATE invitations SET status = 'consumed', consumed_at = ?1 WHERE invitation_id_hash = ?2 AND status = 'accepted'`,
  )
    .bind(now, invitationIdHash)
    .run();
  if ((consumeResult.meta.changes ?? 0) === 0) {
    throw new RelayError("expired");
  }

  await env.RELAY_DB.prepare(
    `INSERT INTO pairs (pair_id_hash, pair_epoch, owner_device_key_id, sub_device_key_id, created_at)
     VALUES (?1, ?2, ?3, ?4, ?5)`,
  )
    .bind(pairIdHash, pairEpoch, ownerDeviceKeyId, subDeviceKeyId, now)
    .run();

  return Response.json({
    type: "pair",
    schemaVersion: 1,
    pairIdHash,
    pairEpoch,
    ownerDeviceKeyId,
    subDeviceKeyId,
    createdAt: now,
  });
}
