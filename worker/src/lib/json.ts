import canonicalize from "canonicalize";

/** RFC 8785 canonical JSON, per protocol/constants.json `canonicalJson`. */
export function toCanonicalJson(value: unknown): string {
  const result = canonicalize(value);
  if (result === undefined) {
    throw new TypeError("value is not JSON-serializable");
  }
  return result;
}

export async function sha256Hex(input: string | ArrayBuffer): Promise<string> {
  const bytes = typeof input === "string" ? new TextEncoder().encode(input) : input;
  const digest = await crypto.subtle.digest("SHA-256", bytes);
  return toHex(digest);
}

export function toHex(buf: ArrayBuffer): string {
  return Array.from(new Uint8Array(buf))
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");
}
