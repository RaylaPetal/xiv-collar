import { SELF } from "cloudflare:test";
import { describe, expect, it } from "vitest";

describe("GET /v1/health", () => {
  it("reports aggregate status without any request", async () => {
    const response = await SELF.fetch("https://relay.test/v1/health");
    expect(response.status).toBe(200);
    const body = await response.json();
    expect(body).toMatchObject({ status: "ok", environment: "local" });
  });

  it("returns not_found for an unknown route", async () => {
    const response = await SELF.fetch("https://relay.test/v1/nope");
    expect(response.status).toBe(404);
    const body = await response.json();
    expect(body).toMatchObject({ type: "error", code: "not_found" });
  });
});
