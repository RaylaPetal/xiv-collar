import { describe, expect, it } from "vitest";
import { deviceKeyId, genSigningKeyPair, randomCapabilityId, signedFetch } from "./helpers";

describe("invitation/acceptance envelope signatures", () => {
  it("rejects invitation creation whose signature does not match its own declared role", async () => {
    const inviter = await genSigningKeyPair();
    const inviterId = await deviceKeyId(inviter.publicKeyJwk);
    const now = Math.floor(Date.now() / 1000);
    const signedAsOwner = {
      type: "invitation",
      schemaVersion: 1,
      invitationId: randomCapabilityId(),
      inviterDeviceKeyId: inviterId,
      inviterPublicKey: inviter.publicKeyJwk,
      role: "owner",
      triggerPhrase: "kae",
      createdAt: now,
      expiresAt: now + 900,
    };
    const canonical = (await import("canonicalize")).default(signedAsOwner)!;
    const sig = await crypto.subtle.sign({ name: "ECDSA", hash: "SHA-256" }, inviter.privateKey, new TextEncoder().encode(canonical));
    const signature = Buffer.from(sig).toString("base64url");

    // Ship a body that claims "sub" while carrying the signature that was computed over "owner".
    const r = await signedFetch("/v1/invitations", "POST", { ...signedAsOwner, role: "sub", signature }, inviter.privateKey, inviterId);
    expect(r.status).toBe(401);
  });

  it("an invitation fetched later carries its own verifiable signature and acceptance proof, not just the relay's word", async () => {
    const inviter = await genSigningKeyPair();
    const receiver = await genSigningKeyPair();
    const inviterId = await deviceKeyId(inviter.publicKeyJwk);
    const receiverId = await deviceKeyId(receiver.publicKeyJwk);
    const now = Math.floor(Date.now() / 1000);

    const invitationUnsigned = {
      type: "invitation",
      schemaVersion: 1,
      invitationId: randomCapabilityId(),
      inviterDeviceKeyId: inviterId,
      inviterPublicKey: inviter.publicKeyJwk,
      role: "owner",
      triggerPhrase: "kae",
      createdAt: now,
      expiresAt: now + 900,
    };
    const canonicalize = (await import("canonicalize")).default;
    const invitationSig = Buffer.from(
      await crypto.subtle.sign({ name: "ECDSA", hash: "SHA-256" }, inviter.privateKey, new TextEncoder().encode(canonicalize(invitationUnsigned)!)),
    ).toString("base64url");
    const createResult = await signedFetch("/v1/invitations", "POST", { ...invitationUnsigned, signature: invitationSig }, inviter.privateKey, inviterId);
    expect(createResult.status).toBe(200);
    const invitationId = createResult.json.invitationId as string;

    const acceptanceUnsigned = {
      type: "acceptance",
      schemaVersion: 1,
      invitationId,
      accepterDeviceKeyId: receiverId,
      accepterPublicKey: receiver.publicKeyJwk,
      proofDigest: "b".repeat(64),
      role: "sub",
      triggerPhrase: "pet",
      createdAt: now,
      expiresAt: now + 900,
    };
    const acceptanceSig = Buffer.from(
      await crypto.subtle.sign({ name: "ECDSA", hash: "SHA-256" }, receiver.privateKey, new TextEncoder().encode(canonicalize(acceptanceUnsigned)!)),
    ).toString("base64url");
    const acceptResult = await signedFetch(
      `/v1/invitations/${invitationId}/accept`,
      "POST",
      { ...acceptanceUnsigned, signature: acceptanceSig },
      receiver.privateKey,
      receiverId,
    );
    expect(acceptResult.status).toBe(200);

    const fetched = await (await import("cloudflare:test")).SELF.fetch(`https://relay.test/v1/invitations/${invitationId}`);
    const body = (await fetched.json()) as any;

    // The inviter's own client would do exactly this verification independently -- not trust the relay.
    const invitationVerifyOk = await crypto.subtle.verify(
      { name: "ECDSA", hash: "SHA-256" },
      await crypto.subtle.importKey("jwk", { ...body.inviterPublicKey, key_ops: ["verify"], ext: true }, { name: "ECDSA", namedCurve: "P-256" }, false, ["verify"]),
      Buffer.from(body.signature, "base64url"),
      new TextEncoder().encode(canonicalize({ ...body, signature: undefined, acceptance: undefined, status: undefined })!),
    );
    expect(invitationVerifyOk).toBe(true);

    expect(body.acceptance).toBeTruthy();
    expect(body.triggerPhrase).toBe("kae");
    expect(body.acceptance.role).toBe("sub");
    expect(body.acceptance.triggerPhrase).toBe("pet");
    const acceptanceVerifyOk = await crypto.subtle.verify(
      { name: "ECDSA", hash: "SHA-256" },
      await crypto.subtle.importKey(
        "jwk",
        { ...body.acceptance.accepterPublicKey, key_ops: ["verify"], ext: true },
        { name: "ECDSA", namedCurve: "P-256" },
        false,
        ["verify"],
      ),
      Buffer.from(body.acceptance.signature, "base64url"),
      new TextEncoder().encode(canonicalize({ ...body.acceptance, signature: undefined })!),
    );
    expect(acceptanceVerifyOk).toBe(true);
  });
});
