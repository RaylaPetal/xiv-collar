import { env } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import { runScheduledCleanup } from "../src/scheduled";

describe("scheduled cleanup", () => {
  it("deletes expired invitations, revocations, stale catalog requests, and their R2 objects", async () => {
    const past = Math.floor(Date.now() / 1000) - 3600;
    const deviceKeyId = "a".repeat(64);

    await env.RELAY_DB.prepare(
      `INSERT INTO invitations (invitation_id_hash, inviter_device_key_id, inviter_public_key_jwk, role, created_at, expires_at, signature, status)
       VALUES ('expired-inv-hash', ?2, '{}', 'owner', ?1, ?1, ?3, 'pending')`,
    )
      .bind(past, deviceKeyId, "s".repeat(86))
      .run();

    await env.RELAY_DB.prepare(
      `INSERT INTO revocations (pair_id_hash, sequence, pair_epoch, reason, issued_by_device_key_id, signature, created_at, expires_at)
       VALUES ('pair-hash-1', 1, 0, 'unpair', 'device-1', 'sig', ?1, ?1)`,
    )
      .bind(past)
      .run();

    await env.RELAY_DB.prepare(
      `INSERT INTO pair_cooldowns (pair_id_hash, last_accepted_sync_at, last_snapshot_id, active_request_id_hash)
       VALUES ('pair-hash-2', 0, 0, 'stale-request-hash')`,
    ).run();
    await env.RELAY_DB.prepare(
      `INSERT INTO catalog_requests (request_id_hash, pair_id_hash, pair_epoch, requester_device_key_id, owner_ephemeral_public_key_jwk, created_at, expires_at, status)
       VALUES ('stale-request-hash', 'pair-hash-2', 0, 'device-1', '{}', ?1, ?1, 'pending')`,
    )
      .bind(past)
      .run();

    const orphanKey = "catalog/orphan-object-hash";
    await env.RELAY_CATALOG_BUCKET.put(orphanKey, new TextEncoder().encode("leftover"));
    await env.RELAY_DB.prepare(
      `INSERT INTO catalog_requests (request_id_hash, pair_id_hash, pair_epoch, requester_device_key_id, owner_ephemeral_public_key_jwk, created_at, expires_at, status)
       VALUES ('orphan-object-hash', 'pair-hash-3', 0, 'device-1', '{}', ?1, ?1, 'uploaded')`,
    )
      .bind(past)
      .run();
    await env.RELAY_DB.prepare(
      `INSERT INTO catalog_objects (request_id_hash, r2_key, sender_device_key_id, recipient_device_key_id, snapshot_id, ciphertext_digest, ciphertext_size_bytes, nonce, sender_ephemeral_public_key_jwk, algorithm, created_at, expires_at)
       VALUES ('orphan-object-hash', ?2, 'device-1', 'device-2', 1, ?3, 5, ?4, '{}', 'ECDH-P256+HKDF-SHA256+AES-256-GCM', ?1, ?1)`,
    )
      .bind(past, orphanKey, "x".repeat(64), "y".repeat(16))
      .run();

    await runScheduledCleanup(env);

    const invitationRow = await env.RELAY_DB.prepare(`SELECT 1 FROM invitations WHERE invitation_id_hash = 'expired-inv-hash'`).first();
    expect(invitationRow).toBeNull();

    const revocationRow = await env.RELAY_DB.prepare(`SELECT 1 FROM revocations WHERE pair_id_hash = 'pair-hash-1'`).first();
    expect(revocationRow).toBeNull();

    const cooldownRow = await env.RELAY_DB.prepare(`SELECT active_request_id_hash FROM pair_cooldowns WHERE pair_id_hash = 'pair-hash-2'`).first<{
      active_request_id_hash: string | null;
    }>();
    expect(cooldownRow?.active_request_id_hash).toBeNull();

    const catalogRequestRow = await env.RELAY_DB.prepare(`SELECT 1 FROM catalog_requests WHERE request_id_hash = 'stale-request-hash'`).first();
    expect(catalogRequestRow).toBeNull();

    const catalogObjectRow = await env.RELAY_DB.prepare(`SELECT 1 FROM catalog_objects WHERE request_id_hash = 'orphan-object-hash'`).first();
    expect(catalogObjectRow).toBeNull();

    const r2Object = await env.RELAY_CATALOG_BUCKET.get(orphanKey);
    expect(r2Object).toBeNull();
  });
});
