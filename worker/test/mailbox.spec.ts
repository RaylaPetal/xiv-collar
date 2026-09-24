import { env } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import { b64url, deviceKeyId, genSigningKeyPair, hex, type Jwk, type KeyPair, pairDevices, signEnvelope, signedFetch } from "./helpers";

interface Pair {
  owner: KeyPair;
  ownerId: string;
  sub: KeyPair;
  subId: string;
  pairIdHash: string;
}

/** A fresh Owner/Sub pair per test so the per-pair upload interval and snapshot sequence never leak between tests. */
async function freshPair(): Promise<Pair> {
  const owner = await genSigningKeyPair();
  const sub = await genSigningKeyPair();
  const ownerId = await deviceKeyId(owner.publicKeyJwk);
  const subId = await deviceKeyId(sub.publicKeyJwk);
  const { pair } = await pairDevices(owner, ownerId, sub, subId, "owner");
  return { owner, ownerId, sub, subId, pairIdHash: pair.pairIdHash };
}

async function ecdhPublicJwk(): Promise<Jwk> {
  const kp = (await crypto.subtle.generateKey({ name: "ECDH", namedCurve: "P-256" }, true, ["deriveBits"])) as CryptoKeyPair;
  const jwk = (await crypto.subtle.exportKey("jwk", kp.publicKey)) as JsonWebKey;
  return { kty: "EC", crv: "P-256", x: jwk.x!, y: jwk.y! };
}

function randomReceiveKeyId(): string {
  return b64url(crypto.getRandomValues(new Uint8Array(16)));
}

async function signedKeyEnvelope(p: Pair, receiveKeyId = randomReceiveKeyId(), signer: KeyPair = p.owner) {
  const unsigned = {
    type: "catalog-mailbox-key",
    schemaVersion: 1,
    pairIdHash: p.pairIdHash,
    pairEpoch: 0,
    receiveKeyId,
    ownerDeviceKeyId: p.ownerId,
    receivePublicKey: await ecdhPublicJwk(),
    createdAt: Math.floor(Date.now() / 1000),
  };
  return { ...unsigned, signature: await signEnvelope(signer.privateKey, unsigned) };
}

async function publishKey(p: Pair, key?: Awaited<ReturnType<typeof signedKeyEnvelope>>) {
  const envelope = key ?? (await signedKeyEnvelope(p));
  const r = await signedFetch("/v1/catalog/mailbox/key", "POST", { pairIdHash: p.pairIdHash, pairEpoch: 0, key: envelope }, p.owner.privateKey, p.ownerId);
  return { r, envelope };
}

async function upload(p: Pair, receiveKeyId: string, snapshotId: number, opts: { as?: KeyPair; asId?: string } = {}) {
  const ciphertext = crypto.getRandomValues(new Uint8Array(64));
  const now = Math.floor(Date.now() / 1000);
  const unsigned = {
    type: "catalog-push",
    schemaVersion: 1,
    pairIdHash: p.pairIdHash,
    pairEpoch: 0,
    receiveKeyId,
    snapshotId,
    senderDeviceKeyId: opts.asId ?? p.subId,
    recipientDeviceKeyId: p.ownerId,
    createdAt: now,
    expiresAt: now + 3600,
    algorithm: "ECDH-P256+HKDF-SHA256+AES-256-GCM",
    ciphertextDigest: hex(await crypto.subtle.digest("SHA-256", ciphertext)),
    ciphertextSizeBytes: ciphertext.byteLength,
    nonce: b64url(crypto.getRandomValues(new Uint8Array(12))),
    senderEphemeralPublicKey: await ecdhPublicJwk(),
  };
  const signer = opts.as ?? p.sub;
  const envelope = { ...unsigned, signature: await signEnvelope(signer.privateKey, unsigned) };
  const r = await signedFetch("/v1/catalog/mailbox/upload", "POST", { envelope, ciphertextBase64Url: b64url(ciphertext) }, signer.privateKey, opts.asId ?? p.subId);
  return { r, ciphertext };
}

const fetchKey = (p: Pair) => signedFetch("/v1/catalog/mailbox/key/fetch", "POST", { pairIdHash: p.pairIdHash, pairEpoch: 0 }, p.sub.privateKey, p.subId);

const status = (p: Pair) => signedFetch("/v1/catalog/mailbox/status", "POST", { pairIdHash: p.pairIdHash, pairEpoch: 0 }, p.owner.privateKey, p.ownerId);

async function consume(p: Pair, snapshotId: number) {
  const nextKey = await signedKeyEnvelope(p);
  const r = await signedFetch("/v1/catalog/mailbox/consume", "POST", { pairIdHash: p.pairIdHash, pairEpoch: 0, snapshotId, nextKey }, p.owner.privateKey, p.ownerId);
  return { r, nextKey };
}

/** Pretends the last upload happened long enough ago that the per-pair minimum interval has passed. */
async function rewindUploadClock(p: Pair) {
  await env.RELAY_DB.prepare(`UPDATE catalog_mailboxes SET last_upload_at = last_upload_at - 3600 WHERE pair_id_hash = ?1`).bind(p.pairIdHash).run();
}

describe("catalog mailbox: receive key", () => {
  it("lets the Owner publish a key and the Sub fetch the same Owner-signed envelope", async () => {
    const p = await freshPair();
    const { r, envelope } = await publishKey(p);
    expect(r.status).toBe(200);

    const fetched = await fetchKey(p);
    expect(fetched.status).toBe(200);
    expect(fetched.json).toEqual({ key: envelope, waitingSnapshotId: null, lastConsumedSnapshotId: null });
  });

  it("reports not_found to the Sub before the Owner ever published a key", async () => {
    const p = await freshPair();
    const fetched = await signedFetch("/v1/catalog/mailbox/key/fetch", "POST", { pairIdHash: p.pairIdHash, pairEpoch: 0 }, p.sub.privateKey, p.subId);
    expect(fetched.status).toBe(404);
  });

  it("rejects a key published by the Sub side, or signed by anyone but the Owner", async () => {
    const p = await freshPair();
    const bySub = await signedFetch("/v1/catalog/mailbox/key", "POST", { pairIdHash: p.pairIdHash, pairEpoch: 0, key: await signedKeyEnvelope(p) }, p.sub.privateKey, p.subId);
    expect(bySub.status).toBe(401);

    const forged = await publishKey(p, await signedKeyEnvelope(p, undefined, p.sub));
    expect(forged.r.status).toBe(401);
  });

  it("rejects the Owner fetching the key through the Sub-only route", async () => {
    const p = await freshPair();
    await publishKey(p);
    const fetched = await signedFetch("/v1/catalog/mailbox/key/fetch", "POST", { pairIdHash: p.pairIdHash, pairEpoch: 0 }, p.owner.privateKey, p.ownerId);
    expect(fetched.status).toBe(401);
  });

  it("rejects every mailbox operation on a revoked pair", async () => {
    const p = await freshPair();
    const { envelope } = await publishKey(p);
    await env.RELAY_DB.prepare(`UPDATE pairs SET revoked_at = ?1 WHERE pair_id_hash = ?2`).bind(Math.floor(Date.now() / 1000), p.pairIdHash).run();

    expect((await publishKey(p)).r.status).toBe(401);
    expect((await upload(p, envelope.receiveKeyId, 1)).r.status).toBe(401);
    expect((await status(p)).status).toBe(401);
  });
});

describe("catalog mailbox: upload", () => {
  it("stores a snapshot encrypted to the current key and reports it in status", async () => {
    const p = await freshPair();
    const { envelope: key } = await publishKey(p);

    const before = await status(p);
    expect(before.json).toMatchObject({ hasKey: true, hasSnapshot: false, lastUploadAt: null });

    const { r } = await upload(p, key.receiveKeyId, 1);
    expect(r.status).toBe(200);

    const after = await status(p);
    expect(after.json).toMatchObject({ hasKey: true, hasSnapshot: true, snapshotId: 1, receiveKeyId: key.receiveKeyId });
    expect(after.json.lastUploadAt).toBeGreaterThan(0);
  });

  it("rejects an upload before the Owner ever published a key", async () => {
    const p = await freshPair();
    const { r } = await upload(p, randomReceiveKeyId(), 1);
    expect(r.status).toBe(404);
  });

  it("rejects an upload encrypted to a stale receive key with expired", async () => {
    const p = await freshPair();
    await publishKey(p);
    const { r } = await upload(p, randomReceiveKeyId(), 1);
    expect(r.status).toBe(410);
    expect(r.json.code).toBe("expired");
  });

  it("rate-limits a second upload inside the per-pair minimum interval, then accepts it once the interval has passed", async () => {
    const p = await freshPair();
    const { envelope: key } = await publishKey(p);
    expect((await upload(p, key.receiveKeyId, 1)).r.status).toBe(200);

    const tooSoon = await upload(p, key.receiveKeyId, 2);
    expect(tooSoon.r.status).toBe(429);
    expect(tooSoon.r.json.code).toBe("rate_limited");
    expect(tooSoon.r.json.retryAfterSeconds).toBeGreaterThan(0);
    expect(tooSoon.r.json.retryAfterSeconds).toBeLessThanOrEqual(60);

    await rewindUploadClock(p);
    expect((await upload(p, key.receiveKeyId, 3)).r.status).toBe(200);
    expect((await status(p)).json.snapshotId).toBe(3);
  });

  it("replaces the waiting snapshot and deletes the previous ciphertext object", async () => {
    const p = await freshPair();
    const { envelope: key } = await publishKey(p);
    await upload(p, key.receiveKeyId, 1);
    await rewindUploadClock(p);
    await upload(p, key.receiveKeyId, 2);

    const listed = await env.RELAY_CATALOG_BUCKET.list({ prefix: `mailbox/${p.pairIdHash}/` });
    expect(listed.objects.map((o) => o.key)).toEqual([`mailbox/${p.pairIdHash}/0/2`]);
  });

  it("rejects a replayed or older snapshot id", async () => {
    const p = await freshPair();
    const { envelope: key } = await publishKey(p);
    await upload(p, key.receiveKeyId, 5);
    await rewindUploadClock(p);
    const older = await upload(p, key.receiveKeyId, 5);
    expect(older.r.status).toBe(400);
    expect((await status(p)).json.snapshotId).toBe(5);
  });

  it("rejects an upload from the Owner's device", async () => {
    const p = await freshPair();
    const { envelope: key } = await publishKey(p);
    const { r } = await upload(p, key.receiveKeyId, 1, { as: p.owner, asId: p.ownerId });
    expect(r.status).toBe(401);
    expect((await status(p)).json.hasSnapshot).toBe(false);
  });
});

describe("catalog mailbox: consume", () => {
  it("hands out the snapshot once, rotates to the next key, and rejects later uploads to the old key", async () => {
    const p = await freshPair();
    const { envelope: key } = await publishKey(p);
    const { ciphertext } = await upload(p, key.receiveKeyId, 1);

    const { r, nextKey } = await consume(p, 1);
    expect(r.status).toBe(200);
    expect(r.json.envelope.snapshotId).toBe(1);
    expect(r.json.envelope.receiveKeyId).toBe(key.receiveKeyId);
    expect(r.json.ciphertextBase64Url).toBe(b64url(ciphertext));

    const again = await consume(p, 1);
    expect(again.r.status).toBe(404);

    const after = await status(p);
    expect(after.json).toMatchObject({ hasSnapshot: false, receiveKeyId: nextKey.receiveKeyId });
    expect(after.json.lastUploadAt).toBeGreaterThan(0); // the Sub has published before, even though nothing is waiting

    await rewindUploadClock(p);
    const stale = await upload(p, key.receiveKeyId, 2);
    expect(stale.r.status).toBe(410);
    expect((await upload(p, nextKey.receiveKeyId, 3)).r.status).toBe(200);

    const listed = await env.RELAY_CATALOG_BUCKET.list({ prefix: `mailbox/${p.pairIdHash}/` });
    expect(listed.objects.map((o) => o.key)).toEqual([`mailbox/${p.pairIdHash}/0/3`]);
  });

  it("refuses to consume a snapshot id that isn't the one waiting", async () => {
    const p = await freshPair();
    const { envelope: key } = await publishKey(p);
    await upload(p, key.receiveKeyId, 4);
    expect((await consume(p, 3)).r.status).toBe(404);
    expect((await status(p)).json.hasSnapshot).toBe(true);
  });

  it("rejects a consume by the Sub", async () => {
    const p = await freshPair();
    const { envelope: key } = await publishKey(p);
    await upload(p, key.receiveKeyId, 1);
    const nextKey = await signedKeyEnvelope(p);
    const r = await signedFetch("/v1/catalog/mailbox/consume", "POST", { pairIdHash: p.pairIdHash, pairEpoch: 0, snapshotId: 1, nextKey }, p.sub.privateKey, p.subId);
    expect(r.status).toBe(401);
  });

  it("gives the Sub a delivery receipt: waiting while unread, consumed after pickup, neither after a key reset", async () => {
    const p = await freshPair();
    const { envelope: key } = await publishKey(p);
    await upload(p, key.receiveKeyId, 1);
    expect((await fetchKey(p)).json).toMatchObject({ waitingSnapshotId: 1, lastConsumedSnapshotId: null });

    const { nextKey } = await consume(p, 1);
    expect((await fetchKey(p)).json).toMatchObject({ key: { receiveKeyId: nextKey.receiveKeyId }, waitingSnapshotId: null, lastConsumedSnapshotId: 1 });

    await rewindUploadClock(p);
    await upload(p, nextKey.receiveKeyId, 2);
    await publishKey(p); // Owner lost its key and reset: snapshot 2 is gone and was never consumed
    expect((await fetchKey(p)).json).toMatchObject({ waitingSnapshotId: null, lastConsumedSnapshotId: 1 });
  });

  it("republishing a key discards the snapshot that could only be read with the old one", async () => {
    const p = await freshPair();
    const { envelope: key } = await publishKey(p);
    await upload(p, key.receiveKeyId, 1);
    await publishKey(p);
    expect((await status(p)).json.hasSnapshot).toBe(false);
    const listed = await env.RELAY_CATALOG_BUCKET.list({ prefix: `mailbox/${p.pairIdHash}/` });
    expect(listed.objects).toHaveLength(0);
  });
});
