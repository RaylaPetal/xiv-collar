import { describe, expect, it } from "vitest";
import { acceptInvitation, createInvitation, deviceKeyId, genSigningKeyPair, signEnvelope, signedFetch } from "./helpers";

describe("revocation epoch isolation", () => {
  it("a stale (pre-re-pair epoch) revocation is rejected once the pair has re-paired to a newer epoch", async () => {
    const inviter = await genSigningKeyPair();
    const receiver = await genSigningKeyPair();
    const inviterId = await deviceKeyId(inviter.publicKeyJwk);
    const receiverId = await deviceKeyId(receiver.publicKeyJwk);

    // Pair at epoch 0.
    const invite1 = await createInvitation(inviter, inviterId);
    await acceptInvitation(invite1.json.invitationId, receiver, receiverId);
    const pair0 = (await signedFetch(`/v1/invitations/${invite1.json.invitationId}/consume`, "POST", {}, inviter.privateKey, inviterId)).json;
    const pairIdHash = pair0.pairIdHash as string;

    // Unpair at epoch 0, then re-pair to epoch 1.
    const now = Math.floor(Date.now() / 1000);
    const revocationEpoch0 = {
      type: "revocation",
      schemaVersion: 1,
      pairIdHash,
      pairEpoch: 0,
      sequence: 1,
      reason: "unpair",
      issuedByDeviceKeyId: receiverId,
      createdAt: now,
      expiresAt: now + 604800,
    };
    const sigEpoch0 = await signEnvelope(receiver.privateKey, revocationEpoch0);
    const publishAtEpoch0 = await signedFetch("/v1/revocations", "POST", { ...revocationEpoch0, signature: sigEpoch0 }, receiver.privateKey, receiverId);
    expect(publishAtEpoch0.status).toBe(200);

    const invite2 = await createInvitation(inviter, inviterId);
    await acceptInvitation(invite2.json.invitationId, receiver, receiverId);
    const pair1 = (await signedFetch(`/v1/invitations/${invite2.json.invitationId}/consume`, "POST", {}, inviter.privateKey, inviterId)).json;
    expect(pair1.pairEpoch).toBe(1);

    // A late-arriving revocation still declaring the old epoch 0 must be rejected outright now.
    const staleRevocationEpoch0 = {
      type: "revocation",
      schemaVersion: 1,
      pairIdHash,
      pairEpoch: 0,
      sequence: 2,
      reason: "panic",
      issuedByDeviceKeyId: receiverId,
      createdAt: now + 5,
      expiresAt: now + 604800,
    };
    const staleSig = await signEnvelope(receiver.privateKey, staleRevocationEpoch0);
    const staleResult = await signedFetch("/v1/revocations", "POST", { ...staleRevocationEpoch0, signature: staleSig }, receiver.privateKey, receiverId);
    expect(staleResult.status).not.toBe(200);

    // The current epoch-1 pairing must remain intact: checking revocations at epoch 1 shows nothing new for that epoch.
    const check = await signedFetch(`/v1/revocations/${pairIdHash}?sinceSequence=0`, "GET", undefined, inviter.privateKey, inviterId);
    expect(check.json.revocations.every((r: { pairEpoch: number }) => r.pairEpoch === 0)).toBe(true);
  });
});
