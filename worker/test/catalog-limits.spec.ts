import { beforeAll, describe, expect, it } from "vitest";
import {
  acceptInvitation,
  b64url,
  createInvitation,
  deviceKeyId,
  genSigningKeyPair,
  hex,
  type Jwk,
  type KeyPair,
  signEnvelope,
  signedFetch,
} from "./helpers";

describe("catalog upload guardrails", () => {
  let inviter: KeyPair;
  let receiver: KeyPair;
  let inviterId: string;
  let receiverId: string;
  let pairIdHash: string;

  beforeAll(async () => {
    inviter = await genSigningKeyPair();
    receiver = await genSigningKeyPair();
    inviterId = await deviceKeyId(inviter.publicKeyJwk);
    receiverId = await deviceKeyId(receiver.publicKeyJwk);

    const invite = await createInvitation(inviter, inviterId);
    const invitationId = invite.json.invitationId as string;
    await acceptInvitation(invitationId, receiver, receiverId);
    const pair = (await signedFetch(`/v1/invitations/${invitationId}/consume`, "POST", {}, inviter.privateKey, inviterId)).json;
    pairIdHash = pair.pairIdHash;
  });

  async function createCatalogRequest(): Promise<string> {
    const ownerEphemeral = (await crypto.subtle.generateKey({ name: "ECDH", namedCurve: "P-256" }, true, ["deriveBits"])) as CryptoKeyPair;
    const jwkRaw = (await crypto.subtle.exportKey("jwk", ownerEphemeral.publicKey)) as JsonWebKey;
    const ownerEphemeralJwk: Jwk = { kty: "EC", crv: "P-256", x: jwkRaw.x!, y: jwkRaw.y! };
    const requestId = b64url(crypto.getRandomValues(new Uint8Array(32)));
    const now = Math.floor(Date.now() / 1000);
    const unsigned = {
      type: "catalog-request",
      schemaVersion: 1,
      pairIdHash,
      pairEpoch: 0,
      requestId,
      requesterDeviceKeyId: inviterId,
      ownerEphemeralPublicKey: ownerEphemeralJwk,
      createdAt: now,
      expiresAt: now + 900,
    };
    const signature = await signEnvelope(inviter.privateKey, unsigned);
    const r = await signedFetch("/v1/catalog/requests", "POST", { ...unsigned, signature }, inviter.privateKey, inviterId);
    expect(r.status).toBe(200);
    return requestId;
  }

  it("rejects an upload whose declared ciphertextSizeBytes does not match the actual bytes", async () => {
    const requestId = await createCatalogRequest();
    const now = Math.floor(Date.now() / 1000);
    const nonce = b64url(crypto.getRandomValues(new Uint8Array(12)));
    const unsigned = {
      type: "catalog-response",
      schemaVersion: 1,
      pairIdHash,
      pairEpoch: 0,
      requestId,
      snapshotId: 1,
      senderDeviceKeyId: receiverId,
      recipientDeviceKeyId: inviterId,
      createdAt: now,
      expiresAt: now + 900,
      algorithm: "ECDH-P256+HKDF-SHA256+AES-256-GCM",
      ciphertextSizeBytes: 999, // lies about the length
      nonce,
      senderEphemeralPublicKey: inviter.publicKeyJwk, // shape-valid placeholder, content unchecked for this test
    };
    const digest = hex(await crypto.subtle.digest("SHA-256", new TextEncoder().encode("short")));
    const envelope = { ...unsigned, ciphertextDigest: digest, signature: await signEnvelope(receiver.privateKey, { ...unsigned, ciphertextDigest: digest }) };

    const r = await signedFetch(
      `/v1/catalog/requests/${requestId}/upload`,
      "POST",
      { envelope, ciphertextBase64Url: b64url(new TextEncoder().encode("short")) },
      receiver.privateKey,
      receiverId,
    );
    expect(r.status).toBe(400);
  });

  it("rejects an upload whose expiresAt exceeds the 15-minute server-enforced cap", async () => {
    const requestId = await createCatalogRequest();
    const now = Math.floor(Date.now() / 1000);
    const nonce = b64url(crypto.getRandomValues(new Uint8Array(12)));
    const ciphertext = new TextEncoder().encode("payload-bytes");
    const digest = hex(await crypto.subtle.digest("SHA-256", ciphertext));
    const unsigned = {
      type: "catalog-response",
      schemaVersion: 1,
      pairIdHash,
      pairEpoch: 0,
      requestId,
      snapshotId: 1,
      senderDeviceKeyId: receiverId,
      recipientDeviceKeyId: inviterId,
      createdAt: now,
      expiresAt: now + 7200, // 2 hours -- far beyond the 15-minute cap
      algorithm: "ECDH-P256+HKDF-SHA256+AES-256-GCM",
      ciphertextDigest: digest,
      ciphertextSizeBytes: ciphertext.byteLength,
      nonce,
      senderEphemeralPublicKey: inviter.publicKeyJwk,
    };
    const signature = await signEnvelope(receiver.privateKey, unsigned);
    const r = await signedFetch(
      `/v1/catalog/requests/${requestId}/upload`,
      "POST",
      { envelope: { ...unsigned, signature }, ciphertextBase64Url: b64url(ciphertext) },
      receiver.privateKey,
      receiverId,
    );
    expect(r.status).toBe(400);
  });
});
