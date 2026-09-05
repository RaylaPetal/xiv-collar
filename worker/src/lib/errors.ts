export type ErrorCode =
  | "invalid_request"
  | "unauthorized"
  | "not_found"
  | "expired"
  | "cooldown_active"
  | "rate_limited"
  | "payload_too_large"
  | "service_unavailable";

const STATUS_BY_CODE: Record<ErrorCode, number> = {
  invalid_request: 400,
  unauthorized: 401,
  not_found: 404,
  expired: 410,
  cooldown_active: 429,
  rate_limited: 429,
  payload_too_large: 413,
  service_unavailable: 503,
};

/**
 * Every rejection path in this Worker throws RelayError and nothing else
 * reaches a client as an error body, so responses stay uniform and
 * non-enumerating (see protocol/schemas/error.schema.json and
 * protocol/docs/threat-model.md "trust boundaries").
 */
export class RelayError extends Error {
  constructor(
    public readonly code: ErrorCode,
    public readonly retryAfterSeconds?: number,
    public readonly requestId?: string,
  ) {
    super(code);
    this.name = "RelayError";
  }

  toResponse(): Response {
    const body: Record<string, unknown> = {
      type: "error",
      schemaVersion: 1,
      code: this.code,
    };
    if (this.retryAfterSeconds !== undefined) body.retryAfterSeconds = this.retryAfterSeconds;
    if (this.requestId !== undefined) body.requestId = this.requestId;

    const headers: Record<string, string> = { "content-type": "application/json" };
    if (this.retryAfterSeconds !== undefined) headers["retry-after"] = String(this.retryAfterSeconds);

    return new Response(JSON.stringify(body), { status: STATUS_BY_CODE[this.code], headers });
  }
}

/**
 * Same shape as an authentication/authorization failure, deliberately. A
 * missing invitation, a wrong capability secret, and an invalid signature
 * must all look identical to the caller (see spec: "Capability identifier is
 * guessed without proof").
 */
export function notFoundOrUnauthorized(): RelayError {
  return new RelayError("unauthorized");
}
