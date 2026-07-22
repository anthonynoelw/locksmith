# Locksmith — API Surface

Locksmith is a REST API for managing the full lifecycle of API keys: issuance, listing, validation, secret retrieval, status transitions (activate/deactivate/revoke), rotation, deletion, and permission (action) management. It is called by a single trusted internal service that uses the keys it receives to authenticate its own downstream consumers. This document covers every implemented endpoint: the request shape, all possible responses, and how errors are returned. The only pieces still missing are listed at the end so this document never claims more than what exists.

Quick links: [Authentication](#authentication) · [Identifying a key](#identifying-a-key) · [Response caching](#response-caching) · [Key states](#key-states) · [Key management](#key-management) · [Key status](#key-status) · [Key actions](#key-actions) · [Infrastructure](#infrastructure) · [Error responses](#error-responses) · [Happy-path flow](#happy-path-flow) · [Not yet implemented](#not-yet-implemented)

---

## Authentication

All endpoints under `/api/v{version}/` require a static bearer token configured at deploy time (`ApiSettings.BearerToken`), validated by a custom `BearerTokenAuthenticationHandler`.

```http
Authorization: Bearer <token>
```

Any request missing this header, presenting a malformed value, or presenting the wrong token receives a `401 Unauthorized` (with a `WWW-Authenticate: Bearer` response header) before any controller action runs. The token is compared with constant-time equality (`CryptographicOperations.FixedTimeEquals`) to prevent timing attacks. Inject it via environment variable or secrets manager — never commit it to source control.

See [ADR-004](../Decisions/ADR-004-authentication.md) for the full reasoning.

This is separate from the `X-Api-Key` header described next — the bearer token proves you're the trusted caller; `X-Api-Key` says *which key* you're asking about.

---

## Identifying a key

There is no `{id}` path segment anywhere in the API. Every endpoint that acts on a specific key identifies it one of two ways:

| Mechanism | Used by | How |
|---|---|---|
| `X-Api-Key` header (the key's raw secret) | The four "current key" `GET` endpoints | `ResolveApiKeyFilter` hashes the header value, looks up the owning key, and stashes its ID on the request |
| `idempotencyKey` in the request body | Every mutating endpoint except create and validate | The service hashes it and looks up the `IdempotencyKey` record, which points at the key |

`ResolveApiKeyFilter` requires **exactly one** `X-Api-Key` header:

- Missing, blank, or duplicated → `400 Bad Request` with a plain `ProblemDetails` body (`title: "Missing API key"`), before any service runs.
- Present but unknown → `404 Not Found` (`NotFoundException` from the underlying lookup).

`POST .../validate` is the one exception: it takes the secret in the request **body**, not the header, because its entire purpose is checking whether an arbitrary presented secret is known.

---

## Response caching

Every response carries an explicit `Cache-Control` directive — there is no unmarked default.

- **Default:** `Cache-Control: no-store, no-cache, must-revalidate, max-age=0` plus `Pragma: no-cache`, applied by `ResponseCacheControlFilter` to every action, and separately by `GlobalExceptionHandler` to every error response (which is produced in middleware, outside the MVC result pipeline the filter runs in).
- **`[Cacheable(60)]` actions:** `Cache-Control: private, max-age=60` and `Vary: X-Api-Key`. Applied only to the four `X-Api-Key`-resolved `GET` endpoints (current key, current status, status history, active actions) — their response depends on which key's secret was presented, so any cache must key on that header too.

`GET .../all` (the bulk admin listing) is **not** cacheable — it isn't scoped to a single caller's key.

---

## Key states

A key moves through a defined set of states. The state history is stored in a separate append-only table (`api_key_statuses`), preserving every transition for audit purposes. See [ADR-001](../Decisions/ADR-001-api-key-lifecycle.md).

| State | Meaning |
|---|---|
| `Inactive` | Created but not yet activated. This is the state every key starts in. |
| `Active` | Accepted for use by downstream consumers. |
| `Revoked` | Permanently disabled. Cannot be re-activated. |
| `Expired` | Past its `expiresAt` date. Treated as invalid. Nothing currently sets this automatically — the Agent expiry job that would do so isn't implemented yet (see [TODO.md](../TODO.md)). |

`PATCH .../status` (below) drives transitions. The guard is a **terminal-state check, not a full transition matrix**: any status can be set as the new status *except* when the current status is already `Revoked` or `Expired`, which always returns `409 Conflict`. There is no dedicated validation that, say, `Inactive → Expired` is a "valid" transition — only that the *current* state isn't already terminal.

---

## Key management

Routes use `/api/v{version}/api-key` (**singular**) — not `/api/v1/keys` or `/api/v1/api-keys`.

### Issue a key

```http
POST /api/v{version}/api-key
```

Creates a new API key in `Inactive` state. Generates a random secret and a random idempotency key, encrypts the secret at rest (AES-256-GCM with a per-key Argon2id-derived DEK), and returns the plaintext secret and idempotency key exactly once. See [ADR-002](../Decisions/ADR-002-api-key-creation.md).

**Request body**

```json
{
  "expiresAt": "2026-09-01T12:00:00Z",
  "actions": ["Read", "Write"]
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `expiresAt` | `string` (ISO 8601) | No | Moment the key expires. Must be in the future. Defaults to 30 days from creation when omitted. |
| `actions` | `string[]` | No | Actions to grant on the new key. Valid values: `Read`, `Write`, `Delete`, `Execute`. Duplicates are deduplicated. Defaults to none. |

**Response 201 — created**

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "secret": "lk_...",
  "idempotencyKey": "..."
}
```

`secret` and `idempotencyKey` are returned only in this response — store both securely. `idempotencyKey` is required later to retrieve, rotate, delete, or update the key. The response is always `no-store` (see [Response caching](#response-caching)) so intermediaries never cache the secret.

**Response 422 — validation error**

Returned when `expiresAt` is not in the future:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.21",
  "title": "Unprocessable Entity",
  "status": 422,
  "errors": {
    "ExpiresAt": ["ExpiresAt must be in the future."]
  }
}
```

---

### Get the current key's metadata

```http
GET /api/v{version}/api-key
```

Returns metadata for the key identified by the `X-Api-Key` header. `[Cacheable(60)]`.

**Headers**

| Header | Required | Description |
|---|---|---|
| `X-Api-Key` | Yes | The key's plaintext secret |

**Response 200**

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "maskedSecretHash": "****...a1b2",
  "createdAt": "2026-06-03T12:00:00Z",
  "createdBy": "api-client",
  "expiresAt": "2026-09-01T12:00:00Z",
  "status": "Inactive",
  "actions": ["Read"]
}
```

`status` is the most recent non-deleted row in `api_key_statuses`; `actions` excludes soft-deleted grants. `createdBy` is always the constant `"api-client"` — it is never the bearer token itself.

**Response 400** — `X-Api-Key` header missing, blank, or duplicated.
**Response 404** — no key matches the presented secret, or the key has since been deleted (see [Delete a key](#delete-a-key)).

---

### List all keys

```http
GET /api/v{version}/api-key/all?limit=&offset=
```

Returns a page of key metadata (no raw secrets) across **all** keys — an admin/bulk listing, not scoped to a caller's own key. Always `no-store`.

| Query param | Default | Description |
|---|---|---|
| `limit` | `50` | Max items to return. Values `<= 0` fall back to the default; values `> 1000` are clamped to `1000`. |
| `offset` | `0` | Items to skip. Negative values fall back to `0`. |

**Response 200**

```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "maskedSecretHash": "****...a1b2",
      "createdAt": "2026-06-03T12:00:00Z",
      "createdBy": "api-client",
      "expiresAt": "2026-09-01T12:00:00Z",
      "status": "Inactive",
      "actions": ["Read"]
    }
  ],
  "total": 1,
  "limit": 50,
  "offset": 0
}
```

---

### Validate a secret

```http
POST /api/v{version}/api-key/validate
```

Hashes a presented secret and looks it up by the unique `SecretHash` index. Returns whether it belongs to a key and whether that key is currently valid. Does **not** return the raw secret.

**Request body**

```json
{ "secret": "lk_..." }
```

**Response 200**

```json
{
  "apiKeyId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "isValid": true
}
```

`isValid` is `true` only when the key's current status is `Active`. Unlike every other read, this endpoint does **not** filter out soft-deleted status rows when picking "current" — it always uses whichever status row has the latest `CreatedAt`, even if that row was soft-deleted by [`DELETE .../api-key`](#delete-a-key). In practice this means a deleted key still validates against its last-known status rather than uniformly reporting unknown/invalid.

**Response 404** — no key matches the presented secret.
**Response 422** — `secret` is empty or whitespace.

---

### Retrieve the raw secret

```http
POST /api/v{version}/api-key/secret
```

Decrypts and returns the raw secret, looked up by the idempotency key returned at creation time.

> **Security note:** The raw key is a high-value secret. This response must travel over TLS and must never be written to logs. See the [threat model](../Security/threat-model.md) for full risk details.

**Request body**

```json
{ "idempotencyKey": "..." }
```

**Response 200**

```json
{
  "apiKeyId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "secret": "lk_..."
}
```

**Response 404** — no idempotency key record matches (including after the key has been deleted — see below).
**Response 422** — `idempotencyKey` is empty or whitespace, or (rarely) the stored ciphertext fails to decrypt (`DecryptionFailedException`).

---

### Rotate a key

```http
POST /api/v{version}/api-key/rotate
```

Atomically deletes the key identified by `idempotencyKey` and issues a brand-new key carrying the same **currently active** actions. Runs inside a single database transaction.

**Request body**

```json
{ "idempotencyKey": "..." }
```

**Response 201 — created**

Same shape as [Issue a key](#issue-a-key) — a new `id`, `secret`, and `idempotencyKey`. The old idempotency key, secret, status history, and actions are soft-deleted; the old `idempotencyKey`/`secret` stop working immediately.

**Response 404** — no key matches the provided `idempotencyKey`.

---

### Delete a key

```http
DELETE /api/v{version}/api-key
```

Identified by `idempotencyKey` in the body (not a header, and not a URL segment). Soft-deletes every currently active status, action, and idempotency-key record tied to the key.

**Request body**

```json
{ "idempotencyKey": "..." }
```

**Response 204** — success.
**Response 404** — no key matches the provided `idempotencyKey`.

> **Note:** this does not remove the `api_keys` row itself — `ApiKey` has no `DeletedAt` column and is never modified after creation (see [database.md](database.md)). "Delete" makes the key functionally unreachable: its idempotency key no longer resolves (so retrieve/rotate/status-update/action grants against it 404), and `GET /api-key`, `GET /api-key/status`, and `GET /api-key/status/history` for it 404 too, because no non-deleted status row remains. `POST /api-key/validate` is the exception — see the note on that endpoint above.

---

## Key status

Routes are under the same `/api/v{version}/api-key` prefix, with a `status` segment.

### Get the current key's status

```http
GET /api/v{version}/api-key/status
```

Identified by the `X-Api-Key` header. `[Cacheable(60)]`.

**Response 200**

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Inactive",
  "createdAt": "2026-06-03T12:00:00Z"
}
```

**Response 400** — `X-Api-Key` header missing, blank, or duplicated.
**Response 404** — no key matches the header, or no non-deleted status row remains.

---

### Get the full status history

```http
GET /api/v{version}/api-key/status/history
```

Identified by the `X-Api-Key` header. `[Cacheable(60)]`. Returns every status row ever recorded for the key, including soft-deleted ones, oldest first.

**Response 200**

```json
[
  { "id": "...", "status": "Inactive", "createdAt": "2026-06-03T12:00:00Z", "deletedAt": "2026-06-04T09:00:00Z" },
  { "id": "...", "status": "Active", "createdAt": "2026-06-04T09:00:00Z", "deletedAt": null }
]
```

**Response 400** — `X-Api-Key` header missing, blank, or duplicated.
**Response 404** — no key matches the header.

---

### Update a key's status

```http
PATCH /api/v{version}/api-key/status
```

Identified by `idempotencyKey` in the body. Soft-deletes the current status row and appends a new one — no direct URL segment for the target status; there is no dedicated "activate", "deactivate", or "revoke" route.

**Request body**

```json
{ "idempotencyKey": "...", "status": "Active" }
```

`status` accepts the enum by name or number (`Inactive` / `0`, `Active` / `1`, `Revoked` / `2`, `Expired` / `3`).

**Response 200** — no body.
**Response 404** — no key matches the provided `idempotencyKey`.
**Response 409** — the key's current status is already `Revoked` or `Expired` (see [Key states](#key-states)).
**Response 422** — `status` is missing or `null`.

---

## Key actions

Routes are under the same `/api/v{version}/api-key` prefix, with an `actions` segment.

### List currently granted actions

```http
GET /api/v{version}/api-key/actions
```

Identified by the `X-Api-Key` header. `[Cacheable(60)]`. Excludes revoked (soft-deleted) grants.

**Response 200**

```json
[
  { "id": "...", "action": "Read", "createdAt": "2026-06-03T12:00:00Z" },
  { "id": "...", "action": "Write", "createdAt": "2026-06-03T12:00:00Z" }
]
```

**Response 400** — `X-Api-Key` header missing, blank, or duplicated.
**Response 404** — no key matches the header.

---

### Replace the full action set

```http
PUT /api/v{version}/api-key/actions
```

Identified by `idempotencyKey` in the body. Diffs the requested set against the currently active set, revoking removed actions and granting added ones inside a single transaction.

**Request body**

```json
{ "idempotencyKey": "...", "actions": ["Read", "Execute"] }
```

An empty `actions` array revokes everything.

**Response 200** — the resulting active actions, same shape as [List currently granted actions](#list-currently-granted-actions).
**Response 404** — no key matches the provided `idempotencyKey`.
**Response 422** — the set contains an undefined action value (e.g. an out-of-range integer).

---

### Grant a single action

```http
POST /api/v{version}/api-key/actions/{actionName}
```

`{actionName}` is matched case-insensitively against the exact enum name (`Read`, `Write`, `Delete`, `Execute`) — numeric values and comma-separated lists are rejected, unlike default `Enum.TryParse` behavior.

**Request body**

```json
{ "idempotencyKey": "..." }
```

**Response 201 — created**

```json
{ "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6", "action": "Read", "createdAt": "2026-06-03T12:00:00Z" }
```

The grant has no self-addressable URL, so no `Location` header is set.

**Response 404** — no key matches the provided `idempotencyKey`.
**Response 409** — the action is already actively granted. A database-level partial unique index (`(ApiKeyId, Action)` where not soft-deleted) is the authoritative guard for concurrent grants racing the same check.
**Response 422** — `{actionName}` doesn't match a defined action.

---

### Revoke a single action

```http
DELETE /api/v{version}/api-key/actions/{actionName}
```

**Request body**

```json
{ "idempotencyKey": "..." }
```

**Response 204** — success.
**Response 404** — no key matches the provided `idempotencyKey`, **or** the action is not currently granted.
**Response 422** — `{actionName}` doesn't match a defined action.

---

## Infrastructure

These endpoints are unversioned (not under `/api/v{version}/`) and do not require authentication.

### Liveness probe

```http
GET /health
```

Always returns `Healthy` as long as the process is running. No dependency checks. Use for container liveness probes.

**Response 200**

```json
{ "status": "Healthy", "totalDuration": "00:00:00.001", "entries": {} }
```

---

### Readiness probe

```http
GET /health/ready
```

Runs checks tagged `"READY"` (EF Core database, Redis cache). Returns `Healthy` only when all dependencies are reachable. Use for container readiness probes.

**Response 200 — ready**

```json
{ "status": "Healthy", "totalDuration": "00:00:00.012", "entries": {} }
```

**Response 503 — not ready**

```json
{
  "status": "Unhealthy",
  "totalDuration": "00:00:05.000",
  "entries": {
    "database": { "status": "Unhealthy", "description": "Connection refused" }
  }
}
```

---

### OpenAPI document and UI (Development only)

```http
GET /openapi/v1.json
GET /scalar/v1
```

`/openapi/v1.json` serves the generated OpenAPI 3 document for API version 1 (one document per version via `ApiVersionDocumentTransformer`). `/scalar/v1` serves a browsable [Scalar](https://scalar.com/) UI over that document. Both are only mapped when `ASPNETCORE_ENVIRONMENT=Development`.

---

## Error responses

All error responses follow [RFC 9457 ProblemDetails](https://www.rfc-editor.org/rfc/rfc9457), and are always `no-store` regardless of the endpoint (see [Response caching](#response-caching)).

| Status | When |
|---|---|
| `400 Bad Request` | `X-Api-Key` header missing, blank, or presented more than once (plain `ProblemDetails`, not the validation-errors shape) |
| `401 Unauthorized` | Missing or invalid bearer token |
| `404 Not Found` | Resource does not exist — unknown secret, unknown idempotency key, or an action that isn't currently granted |
| `409 Conflict` | A status transition from a terminal state (`Revoked`/`Expired`), or granting an action that's already actively granted |
| `422 Unprocessable Entity` | Request body failed validation (field-level errors in `errors`), an action name doesn't match a defined action, or a stored secret failed to decrypt |
| `500 Internal Server Error` | Unhandled exception — `detail` is redacted outside Development |

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.21",
  "title": "Unprocessable Entity",
  "status": 422,
  "errors": {
    "ExpiresAt": ["ExpiresAt must be in the future."]
  }
}
```

---

## Happy-path flow

1. **Issue** — `POST /api/v{version}/api-key` → save the returned `secret` and `idempotencyKey`.
2. **Distribute** — the caller delivers the raw secret to its downstream consumer over a secure channel.
3. **Activate** — `PATCH /api/v{version}/api-key/status` with `{ "idempotencyKey": "...", "status": "Active" }`.
4. **Grant permissions as needed** — `PUT`/`POST`/`DELETE .../api-key/actions...` using the same `idempotencyKey`.
5. **Validate** — a consumer's request can be checked with `POST /api/v{version}/api-key/validate`, or the caller can read its own key's state with `GET /api/v{version}/api-key` / `.../status` / `.../actions` using the `X-Api-Key` header.
6. **Retrieve again if needed** — `POST /api/v{version}/api-key/secret` with the saved `idempotencyKey` re-derives the DEK and decrypts the stored secret.
7. **Rotate or revoke** — `POST /api/v{version}/api-key/rotate` to replace the secret in place (same actions carried over), or `DELETE /api/v{version}/api-key` to retire it permanently.

---

## Not yet implemented

- **Rate limiting** — `WellKnown.RateLimitPolicies.PER_API_KEY` and the `X-Api-Key`-resolved identity it would partition on already exist (`ResolveApiKeyFilter`, `Controller.cs`), but no limiter is wired in yet. Redis is connected and checked in `/health/ready`, but not yet used for limiting.
- **`Idempotency-Key` request-deduplication semantics** — a client-supplied header with `409` on replay-with-different-body. Distinct from the idempotency key Locksmith itself generates and returns at creation, which is fully implemented and used throughout this document.
- **Agent expiry job** — nothing currently transitions a key to `Expired` automatically; `PATCH .../status` can still be used to set it manually.

See [TODO.md](../TODO.md) for exact status.
