import { sha256Hex } from "./json";
import { bytesToBase64Url } from "./base64";

const CAPABILITY_PATTERN = /^[A-Za-z0-9_-]{32,64}$/;

/**
 * Capability identifiers (invitationId, requestId) are themselves the
 * high-entropy bearer secret; the Worker never stores the raw value, only
 * this hash, per protocol/docs/threat-model.md.
 */
export async function capabilityHash(secret: string): Promise<string> {
  if (!CAPABILITY_PATTERN.test(secret)) {
    throw new RangeError("capability secret does not match the expected shape");
  }
  return sha256Hex(secret);
}

export function isValidCapabilityShape(secret: string): boolean {
  return CAPABILITY_PATTERN.test(secret);
}

/** 256-bit random capability secret, base64url without padding (43 characters). */
export function randomCapabilityId(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(32));
  return bytesToBase64Url(bytes);
}
