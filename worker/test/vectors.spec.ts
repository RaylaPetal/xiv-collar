import { describe, expect, it } from "vitest";
import vectors from "../../protocol/vectors/crypto-vectors.json";
import { toCanonicalJson, sha256Hex } from "../src/lib/json";
import { verifyEcdsaSignature } from "../src/lib/crypto";

/**
 * Closes out task 1.2's verification criterion for the Worker side: the
 * cross-runtime vectors in protocol/vectors/crypto-vectors.json (generated
 * once from a Node WebCrypto reference script) must also verify using this
 * Worker's own canonicalization and signature-verification code, not just
 * the script that produced them. See protocol/vectors/README.md.
 */
describe("cross-runtime crypto vectors", () => {
  it("reproduces the canonical JSON digest", async () => {
    const canonical = toCanonicalJson(vectors.canonicalJson.input);
    expect(canonical).toBe(vectors.canonicalJson.canonical);
    expect(await sha256Hex(canonical)).toBe(vectors.canonicalJson.sha256Hex);
  });

  it("verifies the published ECDSA signature against its public key", async () => {
    const ok = await verifyEcdsaSignature(
      vectors.ecdsaSignRequest.signingPublicKeyJwk as never,
      vectors.ecdsaSignRequest.signatureBase64Url,
      vectors.ecdsaSignRequest.baseString,
    );
    expect(ok).toBe(true);
  });

  it("rejects the same signature over a tampered base string", async () => {
    const ok = await verifyEcdsaSignature(
      vectors.ecdsaSignRequest.signingPublicKeyJwk as never,
      vectors.ecdsaSignRequest.signatureBase64Url,
      vectors.ecdsaSignRequest.baseString + "tampered",
    );
    expect(ok).toBe(false);
  });

  it("decrypts the published AES-GCM catalog ciphertext to the exact plaintext", async () => {
    const v = vectors.ecdhHkdfAesGcmCatalogEnvelope;
    const keyBytes = Uint8Array.from(Buffer.from(v.derivedAesKeyHex, "hex"));
    const key = await crypto.subtle.importKey("raw", keyBytes, "AES-GCM", false, ["decrypt"]);
    const nonce = Uint8Array.from(Buffer.from(v.nonceBase64Url, "base64url"));
    const aad = new TextEncoder().encode(v.additionalAuthenticatedDataCanonicalJson);
    const ciphertext = Uint8Array.from(Buffer.from(v.ciphertextWithTagBase64Url, "base64url"));

    const plaintext = await crypto.subtle.decrypt({ name: "AES-GCM", iv: nonce, additionalData: aad, tagLength: 128 }, key, ciphertext);
    expect(new TextDecoder().decode(plaintext)).toBe(v.plaintextUtf8);

    const digest = await sha256Hex(ciphertext.buffer as ArrayBuffer);
    expect(digest).toBe(v.ciphertextDigestSha256Hex);
  });

  it("reproduces the catalog-mailbox-key canonical form and verifies its signature", async () => {
    const v = vectors.catalogMailboxKeySignature;
    expect(toCanonicalJson(v.unsigned)).toBe(v.canonical);
    const ok = await verifyEcdsaSignature(vectors.ecdsaSignRequest.signingPublicKeyJwk as never, v.signatureBase64Url, v.canonical);
    expect(ok).toBe(true);
  });

  it("derives the catalog-push AES key from the receive/ephemeral keys and decrypts the published ciphertext", async () => {
    const e = vectors.ecdhHkdfAesGcmCatalogEnvelope;
    const v = vectors.ecdhHkdfAesGcmCatalogPush;
    const clean = (j: { kty: string; crv: string; x: string; y: string }) => ({ kty: j.kty, crv: j.crv, x: j.x, y: j.y });
    const ecdh = { name: "ECDH", namedCurve: "P-256" };
    const subPrivate = await crypto.subtle.importKey("jwk", e.subEphemeralPrivateKeyJwk as JsonWebKey, ecdh, false, ["deriveBits"]);
    const ownerReceivePublic = await crypto.subtle.importKey("jwk", clean(e.ownerEphemeralPublicKeyJwk), ecdh, true, []);
    const subPublic = await crypto.subtle.importKey("jwk", clean(e.subEphemeralPublicKeyJwk), ecdh, true, []);
    // workers-types spells the ECDH param `$public`; the runtime (and the WebCrypto spec) reads `public`.
    const shared = await crypto.subtle.deriveBits({ name: "ECDH", public: ownerReceivePublic } as never, subPrivate, 256);

    const ownerRaw = new Uint8Array((await crypto.subtle.exportKey("raw", ownerReceivePublic)) as ArrayBuffer);
    const subRaw = new Uint8Array((await crypto.subtle.exportKey("raw", subPublic)) as ArrayBuffer);
    const combined = new Uint8Array(ownerRaw.length + subRaw.length);
    combined.set(ownerRaw, 0);
    combined.set(subRaw, ownerRaw.length);
    const salt = await crypto.subtle.digest("SHA-256", combined);
    expect(Buffer.from(salt).toString("hex")).toBe(v.saltSha256Hex);
    expect(v.infoUtf8).toBe("oathbound-relay-catalog-push-v1" + vectors.catalogMailboxKeySignature.unsigned.pairIdHash + vectors.catalogMailboxKeySignature.unsigned.receiveKeyId);

    const hkdfKey = await crypto.subtle.importKey("raw", shared, "HKDF", false, ["deriveBits"]);
    const aesKeyBytes = await crypto.subtle.deriveBits({ name: "HKDF", hash: "SHA-256", salt, info: new TextEncoder().encode(v.infoUtf8) }, hkdfKey, 256);
    expect(Buffer.from(aesKeyBytes).toString("hex")).toBe(v.derivedAesKeyHex);

    const key = await crypto.subtle.importKey("raw", aesKeyBytes, "AES-GCM", false, ["decrypt"]);
    const ciphertext = Uint8Array.from(Buffer.from(v.ciphertextWithTagBase64Url, "base64url"));
    const plaintext = await crypto.subtle.decrypt(
      { name: "AES-GCM", iv: Uint8Array.from(Buffer.from(v.nonceBase64Url, "base64url")), additionalData: new TextEncoder().encode(v.additionalAuthenticatedDataCanonicalJson), tagLength: 128 },
      key,
      ciphertext,
    );
    expect(new TextDecoder().decode(plaintext)).toBe(v.plaintextUtf8);
    expect(await sha256Hex(ciphertext.buffer as ArrayBuffer)).toBe(v.ciphertextDigestSha256Hex);
  });
});
