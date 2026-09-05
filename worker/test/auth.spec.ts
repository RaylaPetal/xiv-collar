import { SELF } from "cloudflare:test";
import canonicalize from "canonicalize";
import { describe, expect, it } from "vitest";
import { b64url, BASE, deviceKeyId, genSigningKeyPair, hex, randomCapabilityId, signEnvelope } from "./helpers";

describe("request body limits", () => {
  it("rejects a declared oversized body before JSON parsing", async () => {
    const response = await SELF.fetch(BASE + "/v1/invitations", {
      method: "POST",
      headers: {
        "content-type": "application/json",
        "x-relay-device-key-id": "0".repeat(64),
        "x-relay-timestamp": String(Math.floor(Date.now() / 1000)),
        "x-relay-nonce": "A".repeat(22),
        "x-relay-signature": "A".repeat(86),
      },
      body: JSON.stringify({ padding: "x".repeat(1_100_000) }),
    });
    expect(response.status).toBe(413);
    expect(await response.json()).toMatchObject({ code: "payload_too_large" });
  });
});

/** Builds the signed headers for a request without sending it, so a test can mutate the body afterward. */
async function buildSignedHeaders(path: string, method: string, bodyJson: object, privateKey: CryptoKey, deviceKeyIdValue: string) {
  const canonicalBody = canonicalize(bodyJson)!;
  const bodyDigest = hex(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(canonicalBody)));
  const timestamp = Math.floor(Date.now() / 1000);
  const nonce = b64url(crypto.getRandomValues(new Uint8Array(16)));
  const baseString = [method, path, bodyDigest, String(timestamp), nonce].join("\n");
  const sig = await crypto.subtle.sign({ name: "ECDSA", hash: "SHA-256" }, privateKey, new TextEncoder().encode(baseString));
  return {
    canonicalBody,
    headers: {
      "content-type": "application/json",
      "x-relay-device-key-id": deviceKeyIdValue,
      "x-relay-timestamp": String(timestamp),
      "x-relay-nonce": nonce,
      "x-relay-signature": b64url(sig),
    },
  };
}

async function fullInvitationBody(inviter: Awaited<ReturnType<typeof genSigningKeyPair>>, inviterId: string) {
  const now = Math.floor(Date.now() / 1000);
  const unsigned = {
    type: "invitation",
    schemaVersion: 1,
    invitationId: randomCapabilityId(),
    inviterDeviceKeyId: inviterId,
    inviterPublicKey: inviter.publicKeyJwk,
    role: "owner",
    createdAt: now,
    expiresAt: now + 900,
  };
  const signature = await signEnvelope(inviter.privateKey, unsigned);
  return { ...unsigned, signature };
}

describe("signed-request auth guarantees", () => {
  it("rejects a body tampered with after signing", async () => {
    const inviter = await genSigningKeyPair();
    const inviterId = await deviceKeyId(inviter.publicKeyJwk);
    const bodyJson = await fullInvitationBody(inviter, inviterId);
    const { headers } = await buildSignedHeaders("/v1/invitations", "POST", bodyJson, inviter.privateKey, inviterId);

    // Same signature, but the wire body now claims a different role than what was signed.
    const tamperedBody = JSON.stringify({ ...bodyJson, role: "sub" });
    const response = await SELF.fetch(BASE + "/v1/invitations", { method: "POST", headers, body: tamperedBody });
    expect(response.status).toBe(401);
  });

  it("rejects a replayed nonce even with a valid signature", async () => {
    const inviter = await genSigningKeyPair();
    const inviterId = await deviceKeyId(inviter.publicKeyJwk);
    const bodyJson = await fullInvitationBody(inviter, inviterId);
    const { canonicalBody, headers } = await buildSignedHeaders("/v1/invitations", "POST", bodyJson, inviter.privateKey, inviterId);

    const first = await SELF.fetch(BASE + "/v1/invitations", { method: "POST", headers, body: canonicalBody });
    expect(first.status).toBe(200);

    // Byte-identical replay of the same request (same nonce, same signature).
    const second = await SELF.fetch(BASE + "/v1/invitations", { method: "POST", headers, body: canonicalBody });
    expect(second.status).toBe(401);
  });

  it("rejects a request signed with one device's key but claiming another device's id", async () => {
    const real = await genSigningKeyPair();
    const impersonated = await genSigningKeyPair();
    const impersonatedId = await deviceKeyId(impersonated.publicKeyJwk);
    const bodyJson = await fullInvitationBody(real, impersonatedId);
    // Signed with `real`'s key (fullInvitationBody signs with inviter.privateKey = real), but the resolver
    // will look up `inviterPublicKey` from the body (which is `real`'s key) while the device-key-id header
    // claims `impersonated`'s id.
    const { headers } = await buildSignedHeaders("/v1/invitations", "POST", bodyJson, real.privateKey, impersonatedId);
    const canonicalBody = canonicalize(bodyJson)!;

    const response = await SELF.fetch(BASE + "/v1/invitations", { method: "POST", headers, body: canonicalBody });
    expect(response.status).toBe(401);
  });

  it("treats a guessed/nonexistent invitation capability the same as an expired one (no oracle)", async () => {
    const response = await SELF.fetch(BASE + "/v1/invitations/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
    expect(response.status).toBe(404);
    const body = (await response.json()) as { code: string };
    expect(body.code).toBe("not_found");
  });
});
