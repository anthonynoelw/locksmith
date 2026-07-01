# Locksmith — API Surface

Locksmith is a REST API for managing the full lifecycle of API keys: issuance, validation, and retrieval today, with activation, rotation, revocation, and permission management planned next. It is called by a single trusted internal service that uses the keys it receives to authenticate its own downstream consumers. This document covers every implemented endpoint: the request shape, all possible responses, and how errors are returned. Planned-but-unbuilt endpoints are listed separately at the end so this document never claims more than what exists.

Quick links: [Authentication](#authentication) · [Key states](#key-states) · [Key management](#key-management) · [Infrastructure](#infrastructure) · [Error responses](#error-responses) · [Happy-path flow](#happy-path-flow) · [Planned endpoints](#planned-endpoints-not-yet-implemented)

---

## Authentication

All management endpoints require a static bearer token configured at deploy time (`ApiSettings.BearerToken`).

```http
Authorization: Bearer <token>
```

Any request missing this header, or presenting the wrong token, receives a `401 Unauthorized` before any handler runs. The token is compared with constant-time equality (`CryptographicOperations.FixedTimeEquals`) to prevent timing attacks. Inject it via environment variable or secrets manager — never commit it to source control.

See [ADR-004](../Decisions/ADR-004-authentication.md) for the full reasoning.

---

## Key states

A key moves through a defined set of states. The state history is stored in a separate append-only table (`api_key_statuses`), preserving every transition for audit purposes. See [ADR-001](../Decisions/ADR-001-api-key-lifecycle.md).

| State | Meaning |
|---|---|
| `Inactive` | Created but not yet activated. This is the state every key starts in. |
| `Active` | Accepted for use by downstream consumers. |
| `Revoked` | Permanently disabled. Cannot be re-activated. |
| `Expired` | Past its `expiresAt` date. Treated as invalid. |

Today, `Inactive` is the only status ever written — it is set once at creation and nothing currently transitions a key to `Active`, `Revoked`, or `Expired`. The `PATCH`/rotate/revoke endpoints and the Agent expiry job that would drive those transitions are not yet implemented; see [Planned endpoints](#planned-endpoints-not-yet-implemented) and [TODO.md](../TODO.md).

---

## Key management

Routes use `/api/v{version}/api-keys` (plural, hyphenated) — not `/api/v1/keys`.

### Issue a key

```http
POST /api/v{version}/api-keys
```

Creates a new API key in `Inactive` state. Generates a random secret and a random idempotency key, encrypts the secret at rest (AES-256-GCM with a per-key Argon2id-derived DEK), and returns the plaintext secret and idempotency key exactly once. See [ADR-002](../Decisions/ADR-002-api-key-creation.md).

**Headers**

| Header | Required | Description |
|---|---|---|
| `Authorization` | Yes | `Bearer <token>` |

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
| `actions` | `string[]` | No | Actions to grant on the new key. Valid values: `Read`, `Write`, `Delete`, `Execute`. Defaults to none. |

**Response 201 — created**

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "secret": "lk_...",
  "idempotencyKey": "..."
}
```

`secret` and `idempotencyKey` are returned only in this response — store both securely. `idempotencyKey` is required later to retrieve the secret via [Retrieve the raw secret](#retrieve-the-raw-secret). The response also sets `Cache-Control: no-store, no-cache, must-revalidate, max-age=0` and `Pragma: no-cache` so intermediaries never cache the secret.

**Response 422 — validation error**

Returned when `expiresAt` is not in the future. See [Error responses](#error-responses).

---

### List keys

```http
GET /api/v{version}/api-keys?limit=&offset=
```

Returns a page of key metadata (no raw secrets).

| Query param | Default | Description |
|---|---|---|
| `limit` | `50` | Max items to return. Values `<= 0` or `> 1000` fall back to the default. |
| `offset` | `0` | Items to skip. Negative values fall back to `0`. |

**Response 200**

```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "maskedSecretHash": "****...a1b2",
      "createdAt": "2026-06-03T12:00:00Z",
      "createdBy": "<bearer token>",
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

### Get key metadata

```http
GET /api/v{version}/api-keys/{id}
```

Returns metadata for a single key. Does not return the secret — use [Retrieve the raw secret](#retrieve-the-raw-secret) for that. `status` is derived from the most recent row in `api_key_statuses`; `actions` excludes soft-deleted grants.

**Response 200** — same shape as one item in the [list](#list-keys) response.

**Response 404** — key not found.

---

### Validate a secret

```http
POST /api/v{version}/api-keys/validate
```

Hashes a presented secret and looks it up by the unique `SecretHash` index. Returns whether it belongs to a key and that key's current status. Does **not** return the raw secret.

**Request body**

```json
{
  "secret": "lk_..."
}
```

**Response 200**

```json
{
  "apiKeyId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "isValid": true,
  "status": "Active"
}
```

`isValid` is `true` only when the key's current status is `Active`.

**Response 404** — no key matches the presented secret.

**Response 422** — `secret` is empty or whitespace.

---

### Retrieve the raw secret

```http
POST /api/v{version}/api-keys/retrieve-secret
```

Decrypts and returns the raw secret, looked up by the idempotency key returned at creation time (not by `{keyId}` in the path, and not via `GET`).

> **Security note:** The raw key is a high-value secret. This response must travel over TLS and must never be written to logs. See the [threat model](../Security/threat-model.md) (EP-4, A-2, A-7) for full risk details.

**Request body**

```json
{
  "idempotencyKey": "..."
}
```

**Response 200**

```json
{
  "apiKeyId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "secret": "lk_..."
}
```

**Response 404** — no idempotency key record matches.

**Response 422** — `idempotencyKey` is empty or whitespace, or (rarely) `422` if stored ciphertext fails to decrypt (`DecryptionFailedException`).

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

Runs checks tagged `"ready"` (EF Core database, Redis cache). Returns `Healthy` only when all dependencies are reachable. Use for container readiness probes.

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

All error responses follow [RFC 9457 ProblemDetails](https://www.rfc-editor.org/rfc/rfc9457).

| Status | When |
|---|---|
| `401 Unauthorized` | Missing or invalid bearer token |
| `404 Not Found` | Resource does not exist |
| `422 Unprocessable Entity` | Request body failed validation (field-level errors in `errors`), or a stored secret failed to decrypt |
| `500 Internal Server Error` | Unhandled exception — `detail` is redacted outside Development |

`409 Conflict` is mapped in the global exception handler for `ConflictException`, but no current endpoint throws it — it is reserved for the planned state-transition and action-grant endpoints (see below).

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

The sequence supported by what's implemented today:

1. **Issue** — `POST /api/v{version}/api-keys` → save the returned `secret` and `idempotencyKey`.
2. **Distribute** — the caller delivers the raw secret to its downstream consumer over a secure channel.
3. **Validate** — a consumer's request can be checked with `POST /api/v{version}/api-keys/validate`, which also reports the key's current status.
4. **Retrieve again if needed** — `POST /api/v{version}/api-keys/retrieve-secret` with the saved `idempotencyKey` re-derives the DEK and decrypts the stored secret.

There is currently no way to activate a key (every key stays `Inactive` forever), so `isValid` from the validate endpoint will always be `false` until [`PatchApiKeyStatus`](#planned-endpoints-not-yet-implemented) ships.

---

## Planned endpoints (not yet implemented)

These are designed but not built — see [TODO.md](../TODO.md) for current status. Do not rely on them existing yet.

- `PATCH /api/v{version}/api-keys/{id}` — activate/deactivate; `409` on invalid transition
- `POST /api/v{version}/api-keys/{id}/rotate` — issue a new secret, invalidate the old one
- `DELETE /api/v{version}/api-keys/{id}` — revoke permanently
- `GET /api/v{version}/api-keys/{id}/actions` — list granted actions
- `PUT /api/v{version}/api-keys/{id}/actions` — replace the full action set
- `POST /api/v{version}/api-keys/{id}/actions/{action}` — grant a single action
- `DELETE /api/v{version}/api-keys/{id}/actions/{action}` — revoke a single action
- `Idempotency-Key` request-deduplication semantics (client-supplied header, `409` on replay with a different body) — distinct from the idempotency key Locksmith generates and returns today, which is used only for secret retrieval
