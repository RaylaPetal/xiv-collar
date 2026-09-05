import { base64UrlToBytes } from "./base64";

export interface EcPublicKeyJwk {
  kty: "EC";
  crv: "P-256";
  x: string;
  y: string;
}

/** Signing/verification uses raw ECDSA P-256/SHA-256 signatures (r||s), per protocol/constants.json. */
export async function verifyEcdsaSignature(
  publicKeyJwk: EcPublicKeyJwk,
  signatureBase64Url: string,
  message: string,
): Promise<boolean> {
  if (publicKeyJwk.kty !== "EC" || publicKeyJwk.crv !== "P-256") return false;

  let key: CryptoKey;
  try {
    key = await crypto.subtle.importKey(
      "jwk",
      { ...publicKeyJwk, ext: true, key_ops: ["verify"] },
      { name: "ECDSA", namedCurve: "P-256" },
      false,
      ["verify"],
    );
  } catch {
    return false;
  }

  let signatureBytes: Uint8Array;
  try {
    signatureBytes = base64UrlToBytes(signatureBase64Url);
  } catch {
    return false;
  }
  if (signatureBytes.byteLength !== 64) return false;

  return crypto.subtle.verify(
    { name: "ECDSA", hash: "SHA-256" },
    key,
    signatureBytes,
    new TextEncoder().encode(message),
  );
}

/** Fingerprint used as a deviceKeyId: SHA-256 of the JCS-canonicalized signing public key JWK. */
export async function deviceKeyIdForPublicKey(publicKeyJwk: EcPublicKeyJwk): Promise<string> {
  const { toCanonicalJson, sha256Hex } = await import("./json");
  return sha256Hex(toCanonicalJson(publicKeyJwk));
}
