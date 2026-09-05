import type { Env } from "../env";
import { CATALOG_CIPHERTEXT_MAX_BYTES } from "./constants";
import { RelayError } from "./errors";

const CATALOG_CONTENT_TYPE = "application/octet-stream";

export function r2KeyForRequest(requestIdHash: string): string {
  return `catalog/${requestIdHash}`;
}

/** Content type and max size are both server-controlled; nothing from the caller reaches R2 metadata unchecked. */
export async function putCiphertext(env: Env, key: string, bytes: Uint8Array): Promise<void> {
  if (bytes.byteLength > CATALOG_CIPHERTEXT_MAX_BYTES) {
    throw new RelayError("payload_too_large");
  }
  await env.RELAY_CATALOG_BUCKET.put(key, bytes, {
    httpMetadata: { contentType: CATALOG_CONTENT_TYPE },
  });
}

/** One-use retrieval: callers are expected to delete immediately after a successful read (see catalog.ts consume). */
export async function getCiphertext(env: Env, key: string): Promise<Uint8Array | null> {
  const object = await env.RELAY_CATALOG_BUCKET.get(key);
  if (!object) return null;
  return new Uint8Array(await object.arrayBuffer());
}

export async function deleteCiphertext(env: Env, key: string): Promise<void> {
  await env.RELAY_CATALOG_BUCKET.delete(key);
}
