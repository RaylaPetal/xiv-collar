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
});
