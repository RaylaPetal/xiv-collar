import { b64url, createInvitation, acceptInvitation, deviceKeyId, genSigningKeyPair, signEnvelope, signedFetch, type Jwk } from "./helpers";
import { describe, expect, it } from "vitest";

describe("mutual pair (both directions between the same two devices)", () => {
  it("catalog request and revocation still work for the older epoch once the opposite direction creates a newer one", async () => {
    const alice = await genSigningKeyPair();
    const bob = await genSigningKeyPair();
    const aliceId = await deviceKeyId(alice.publicKeyJwk);
    const bobId = await deviceKeyId(bob.publicKeyJwk);

    // Epoch 0: Alice owns Bob.
    const invite1 = await createInvitation(alice, aliceId, "owner");
    await acceptInvitation(invite1.json.invitationId, bob, bobId);
    const pair0 = (await signedFetch(`/v1/invitations/${invite1.json.invitationId}/consume`, "POST", {}, alice.privateKey, aliceId)).json;
    expect(pair0.pairEpoch).toBe(0);
    const pairIdHash = pair0.pairIdHash as string;

    // Epoch 1: same two devices, opposite direction - Bob owns Alice. Same pairIdHash (computed from just
    // the two device keys), a different epoch. This is the exact shape a mutual dom/sub pair produces.
    const invite2 = await createInvitation(bob, bobId, "owner");
    await acceptInvitation(invite2.json.invitationId, alice, aliceId);
    const pair1 = (await signedFetch(`/v1/invitations/${invite2.json.invitationId}/consume`, "POST", {}, bob.privateKey, bobId)).json;
    expect(pair1.pairEpoch).toBe(1);
    expect(pair1.pairIdHash).toBe(pairIdHash);

    // A catalog request scoped to the OLDER epoch (0) must still succeed - it must not be authorized
    // against whichever epoch happens to be newest for this shared hash.
    const ownerEphemeral = (await crypto.subtle.generateKey({ name: "ECDH", namedCurve: "P-256" }, true, ["deriveBits"])) as CryptoKeyPair;
    const ownerEphemeralJwkRaw = (await crypto.subtle.exportKey("jwk", ownerEphemeral.publicKey)) as JsonWebKey;
    const ownerEphemeralJwk: Jwk = { kty: "EC", crv: "P-256", x: ownerEphemeralJwkRaw.x!, y: ownerEphemeralJwkRaw.y! };
    const now = Math.floor(Date.now() / 1000);
    const catalogRequestUnsigned = {
      type: "catalog-request",
      schemaVersion: 1,
      pairIdHash,
      pairEpoch: 0,
      requestId: b64url(crypto.getRandomValues(new Uint8Array(32))),
      requesterDeviceKeyId: aliceId,
      ownerEphemeralPublicKey: ownerEphemeralJwk,
      createdAt: now,
      expiresAt: now + 900,
    };
    const catalogReqSig = await signEnvelope(alice.privateKey, catalogRequestUnsigned);
    const catalogResult = await signedFetch("/v1/catalog/requests", "POST", { ...catalogRequestUnsigned, signature: catalogReqSig }, alice.privateKey, aliceId);
    expect(catalogResult.status).toBe(200);

    // A revocation scoped to the OLDER epoch (0) must also still succeed, independently of epoch 1's
    // existence - ending one direction never blocks or interferes with the other.
    const revocationUnsigned = {
      type: "revocation",
      schemaVersion: 1,
      pairIdHash,
      pairEpoch: 0,
      sequence: 1,
      reason: "unpair" as const,
      issuedByDeviceKeyId: bobId,
      createdAt: now,
      expiresAt: now + 604800,
    };
    const revocationSig = await signEnvelope(bob.privateKey, revocationUnsigned);
    const revocationResult = await signedFetch("/v1/revocations", "POST", { ...revocationUnsigned, signature: revocationSig }, bob.privateKey, bobId);
    expect(revocationResult.status).toBe(200);

    // Epoch 1 (the other direction) must remain entirely unaffected by epoch 0's revocation.
    const epoch1Check = await signedFetch(`/v1/revocations/${pairIdHash}?sinceSequence=0`, "GET", undefined, bob.privateKey, bobId);
    expect(epoch1Check.status).toBe(200);
    const epoch0Entries = epoch1Check.json.revocations.filter((r: { pairEpoch: number }) => r.pairEpoch === 0);
    expect(epoch0Entries).toHaveLength(1);
  });
});
