import { describe, expect, it } from "vitest";
import { acceptInvitation, createInvitation, deviceKeyId, genSigningKeyPair, signedFetch } from "./helpers";

describe("GET /v1/pairs/:pairIdHash", () => {
  it("lets the accepter (who never calls consume) learn the pair epoch, but not an unrelated device", async () => {
    const inviter = await genSigningKeyPair();
    const receiver = await genSigningKeyPair();
    const stranger = await genSigningKeyPair();
    const inviterId = await deviceKeyId(inviter.publicKeyJwk);
    const receiverId = await deviceKeyId(receiver.publicKeyJwk);
    const strangerId = await deviceKeyId(stranger.publicKeyJwk);

    const invite = await createInvitation(inviter, inviterId);
    await acceptInvitation(invite.json.invitationId, receiver, receiverId);
    const pair = (await signedFetch(`/v1/invitations/${invite.json.invitationId}/consume`, "POST", {}, inviter.privateKey, inviterId)).json;

    // Register `stranger` as a known device (some other invitation) so its signature can be verified at all,
    // then confirm it's still refused for this unrelated pair.
    await createInvitation(stranger, strangerId);

    const asAccepter = await signedFetch(`/v1/pairs/${pair.pairIdHash}`, "GET", undefined, receiver.privateKey, receiverId);
    expect(asAccepter.status).toBe(200);
    expect(asAccepter.json.pairEpoch).toBe(0);
    expect(asAccepter.json.ownerDeviceKeyId).toBe(inviterId);

    const asStranger = await signedFetch(`/v1/pairs/${pair.pairIdHash}`, "GET", undefined, stranger.privateKey, strangerId);
    expect(asStranger.status).not.toBe(200);
  });
});
