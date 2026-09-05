import { describe, expect, it } from "vitest";
import { env } from "cloudflare:test";
import { createInvitation, deviceKeyId, genSigningKeyPair } from "./helpers";

describe("per-device quota enforcement", () => {
  it("trips after the configured number of invitation creations in the window and reports Retry-After", async () => {
    const inviter = await genSigningKeyPair();
    const inviterId = await deviceKeyId(inviter.publicKeyJwk);

    const statuses: number[] = [];
    let limitedResponse: Awaited<ReturnType<typeof createInvitation>> | null = null;
    for (let i = 0; i < 11; i++) {
      const r = await createInvitation(inviter, inviterId);
      statuses.push(r.status);
      if (r.status === 429) limitedResponse = r;
    }

    expect(statuses.filter((s) => s === 200)).toHaveLength(10);
    expect(statuses.filter((s) => s === 429)).toHaveLength(1);
    expect(limitedResponse?.headers.get("retry-after")).toBeTruthy();
    const bucket = Math.floor(Date.now() / 1000 / 3600) * 3600;
    const counter = await env.RELAY_DB.prepare(
      "SELECT count FROM quota_counters WHERE scope = ?1 AND window_start = ?2",
    ).bind(`deviceInvitationCreate:${inviterId}`, bucket).first<{ count: number }>();
    expect(counter?.count).toBe(10);
  });
});
