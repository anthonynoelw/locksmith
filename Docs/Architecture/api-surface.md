# Locksmith — API Surface

Locksmith is a REST API for managing the full lifecycle of API keys: issuance, activation, rotation, and revocation. It is called by a single trusted internal service that uses the keys it receives to authenticate its own downstream consumers. This document covers every endpoint: the request shape, all possible responses, and how errors are returned.

Quick links: [Authentication](#authentication) · [Key states](#key-states) · [Key management](#key-management) · [Action management](#action-management) · [Infrastructure](#infrastructure) · [Error responses](#error-responses) · [Happy-path flow](#happy-path-flow)

---

## Authentication

All management endpoints require a static bearer token configured at deploy time.

```http
Authorization: Bearer <LOCKSMITH_ADMIN_TOKEN>
```

Any request missing this header, or presenting the wrong token, receives a `401 Unauthorized` before any handler runs. The token is compared with constant-time equality (`CryptographicOperations.FixedTimeEquals`) to prevent timing attacks. Inject it via environment variable or secrets manager — never commit it to source control.

See [ADR-004](../Decisions/ADR-004-authentication.md) for the full reasoning.

---

## Key states

A key moves through a defined set of states. The state history is stored in a separate append-only table, preserving every transition for audit purposes. See [ADR-001](../Decisions/ADR-001-api-key-lifecycle.md).

| State | Meaning |
|---|---|
| `Inactive` | Created but not yet activated. Any request using this key is rejected. |
| `Active` | Accepted for use by downstream consumers. |
| `Revoked` | Permanently disabled. Cannot be re-activated. Record and full history are preserved (soft delete). |
| `Expired` | Past its `expiresAt` date. Treated as invalid. Recorded in the state history. |

Valid transitions:

- `Inactive → Active` (activate via PATCH)
- `Active → Inactive` (deactivate via PATCH)
- `Active` or `Inactive → Revoked` (via DELETE)
- Any → `Expired` automatically when `expiresAt` is reached

---

## Key management

### Issue a key

```http
POST /api/v1/keys
```

Creates a new API key. The raw key is returned in the response body and is also stored encrypted at rest — it can be retrieved later via [GET /api/v1/keys/{keyId}/secret](#retrieve-the-raw-key). The key starts in `Inactive` state; activate it with [PATCH](#activate-or-deactivate-a-key) before it can be used. See [ADR-002](../Decisions/ADR-002-api-key-creation.md).

**Headers**

| Header | Required | Description |
|---|---|---|
| `Authorization` | Yes | `Bearer <token>` |
| `Idempotency-Key` | Recommended | Client-generated UUID. Repeating the same value on retry returns the original `201` body without creating a second key. |

**Request body**

```json
{
  "ownerId": "svc_billing",
  "expiresInDays": 90
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `ownerId` | `string` | Yes | Identifier of the entity this key belongs to. |
| `expiresInDays` | `integer` | Yes | Days from now until the key expires. Minimum `1`. |

**Response 201 — created**

```json
{
  "keyId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "key": "...",
  "ownerId": "svc_billing",
  "status": "Inactive",
  "createdAt": "2026-06-03T12:00:00Z",
  "expiresAt": "2026-09-01T12:00:00Z"
}
```

**Response 409 — idempotency conflict**

The `Idempotency-Key` was already used. The body is identical to the original `201`.

**Response 422 — validation error**

See [Error responses](#error-responses).

---

### Get key metadata

```http
GET /api/v1/keys/{keyId}
```

Returns metadata for a key. Does not return the key value — use [/secret](#retrieve-the-raw-key) for that.

**Response 200**

```json
{
  "keyId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "ownerId": "svc_billing",
  "status": "Active",
  "actions": ["read", "write"],
  "createdAt": "2026-06-03T12:00:00Z",
  "expiresAt": "2026-09-01T12:00:00Z"
}
```

**Response 404** — key not found.

---

### Retrieve the raw key

```http
GET /api/v1/keys/{keyId}/secret
```

Returns the raw key value. The key is stored encrypted at rest in PostgreSQL using a data encryption key (DEK); this endpoint decrypts and returns it. The same bearer token is required.

> **Security note:** The raw key is a high-value secret. This response must travel over TLS. The value must never be written to logs. See the [threat model](../Security/threat-model.md) (EP-2a, A-2, A-7) for full risk details.

**Response 200**

```json
{
  "keyId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "key": "..."
}
```

**Response 404** — key not found.

---

### Activate or deactivate a key

```http
PATCH /api/v1/keys/{keyId}
```

Transitions the key between `Active` and `Inactive`. Each transition is appended to the state history.

**Request body**

```json
{
  "status": "Active"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `status` | `string` | Yes | Target state. One of: `Active`, `Inactive`. |

**Response 200**

```json
{
  "keyId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Active"
}
```

**Response 404** — key not found.

**Response 409** — key is already in the requested state, or is `Revoked`/`Expired` and cannot be transitioned.

**Response 422** — `status` is not a valid value.

---

### Rotate a key

```http
POST /api/v1/keys/{keyId}/rotate
```

Generates a new secret for an existing key. The old secret is invalidated immediately. The key ID, owner, expiry, and actions are unchanged. The new raw value is returned in the response and stored encrypted at rest.

**Response 200**

```json
{
  "keyId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "key": "...",
  "status": "Active",
  "rotatedAt": "2026-06-03T14:30:00Z"
}
```

**Response 404** — key not found.

**Response 409** — key is `Revoked` or `Expired` and cannot be rotated.

---

### Revoke a key

```http
DELETE /api/v1/keys/{keyId}
```

Permanently revokes the key. A `Revoked` status entry is appended to the state history. The key record is never physically removed — full history is preserved for audit.

**Response 204** — revoked. No body.

**Response 404** — key not found.

---

## Action management

Actions define what a key holder is permitted to do. Per-key least-privilege: a compromised key's blast radius is bounded by exactly the actions assigned to it. Valid action names: `read`, `write`, `delete`, `execute`. A key with no actions cannot authorize any operation.

Actions are stored in a normalized junction table (`api_key_permissions`) so they can be granted or revoked independently of the key. See [ADR-003](../Decisions/ADR-003-api-key-action-management.md).

### List actions

```http
GET /api/v1/keys/{keyId}/actions
```

**Response 200**

```json
{
  "keyId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "actions": ["read", "write"]
}
```

**Response 404** — key not found.

---

### Replace all actions

```http
PUT /api/v1/keys/{keyId}/actions
```

Replaces the full set of actions for the key. Any actions not in the new list are revoked.

**Request body**

```json
{
  "actions": ["read", "write", "delete"]
}
```

**Response 200**

```json
{
  "keyId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "actions": ["read", "write", "delete"]
}
```

**Response 404** — key not found.

**Response 422** — one or more action names are not valid.

---

### Grant an action

```http
POST /api/v1/keys/{keyId}/actions/{action}
```

Grants a single action. Valid values for `{action}`: `read`, `write`, `delete`, `execute`.

**Response 204** — granted. No body.

**Response 404** — key not found.

**Response 409** — action is already granted.

**Response 422** — `{action}` is not a valid action name.

---

### Revoke an action

```http
DELETE /api/v1/keys/{keyId}/actions/{action}
```

Revokes a single action from the key.

**Response 204** — revoked. No body.

**Response 404** — key not found, or action was not granted.

---

## Infrastructure

These endpoints are unversioned (not under `/api/v1/`) and do not require authentication.

### Liveness probe

```http
GET /health
```

Returns `Healthy` as long as the process is running. No dependency checks. Use for container liveness probes.

**Response 200**

```json
{ "status": "Healthy", "totalDuration": "00:00:00.001", "entries": {} }
```

---

### Readiness probe

```http
GET /health/ready
```

Runs checks for dependencies tagged `"ready"` (database, cache). Returns `Healthy` only when all are reachable. Use for container readiness probes.

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

## Error responses

All error responses follow [RFC 9457 ProblemDetails](https://www.rfc-editor.org/rfc/rfc9457).

| Status | When |
|---|---|
| `401 Unauthorized` | Missing or invalid bearer token |
| `404 Not Found` | Resource does not exist |
| `409 Conflict` | State transition not allowed; or idempotency key already used |
| `422 Unprocessable Entity` | Request body failed validation — field-level errors in `errors` |
| `500 Internal Server Error` | Unhandled exception — `detail` is redacted outside Development |

```json
{
  "type": "https://tools.ietf.org/html/rfc9457",
  "title": "Validation failed",
  "status": 422,
  "errors": {
    "ownerId": ["The ownerId field is required."],
    "expiresInDays": ["Must be at least 1."]
  }
}
```

---

## Happy-path flow

The typical sequence from first request to a key ready for use by a downstream consumer:

1. **Issue** — `POST /api/v1/keys` → save the returned `key` value.
2. **Grant actions** — `PUT /api/v1/keys/{keyId}/actions` with the permissions this key should carry.
3. **Activate** — `PATCH /api/v1/keys/{keyId}` with `{ "status": "Active" }` → key is now live.
4. **Distribute** — the caller delivers the raw key to its downstream consumer over a secure channel.
5. **Rotate** (when needed) — `POST /api/v1/keys/{keyId}/rotate` → distribute the new key; the old secret is invalid immediately.
6. **Revoke** (when done) — `DELETE /api/v1/keys/{keyId}` → key is permanently retired; full history preserved.
