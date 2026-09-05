import { sha256Hex } from "./json";

/** Never persists the raw client IP; only its hash is used as a quota scope key. */
export async function originScope(request: Request): Promise<string> {
  const ip = request.headers.get("cf-connecting-ip") ?? "unknown";
  return sha256Hex(ip);
}
