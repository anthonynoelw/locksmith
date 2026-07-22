# What is Locksmith?

Locksmith is a self-contained REST API service that manages the full lifecycle of API keys for internal services. Its job is simple to state: create API keys securely, control what those keys are allowed to do, and activate, rotate, and revoke them over time — while maintaining a permanent, tamper-evident audit trail of every state change. Today it covers all of that: creation, listing, secret validation and retrieval, status transitions (activate/deactivate/revoke), rotation, deletion, and per-key permission management. See [api-surface.md](Architecture/api-surface.md) for the exact endpoints, and [TODO.md](TODO.md) for what's still outstanding (mainly rate limiting and an automated expiry job).

## The problem it solves

Any service that exposes a programmatic interface needs a way to authenticate its callers and constrain their privileges. Rolling that logic into each service individually leads to inconsistent security, duplicate code, and audit gaps. Locksmith externalises that concern: one service issues and manages API keys so individual applications do not have to.

## What Locksmith does today

### Secure key creation

When a caller requests a new API key, Locksmith generates a cryptographically random secret and a random idempotency key, hashes the secret with SHA-256 for lookup, and encrypts it at rest with AES-256-GCM using a per-key DEK derived via Argon2id from the idempotency key itself. The raw secret and the idempotency key are returned to the caller exactly once at creation (or rotation). Unlike a pure hash-and-discard design, the raw secret *can* be recovered later — by presenting the idempotency key to the retrieval endpoint, which re-derives the DEK and decrypts the stored ciphertext. This trade-off exists because the caller needs a way to re-fetch a secret it failed to persist on its end without Locksmith ever storing the secret in plaintext.

Keys start in an **Inactive** state. From there, a caller drives every subsequent transition explicitly through `PATCH /api-key/status` (see [ADR-001](Decisions/ADR-001-api-key-lifecycle.md)) — nothing transitions automatically except the not-yet-built expiry job described below.

### Lifecycle state machine

Each key moves through a defined set of states:

| State | Meaning |
|---|---|
| Inactive | Created but not yet usable. Default state at creation. |
| Active | Authorized to authenticate requests. Reached via `PATCH /api-key/status`. |
| Revoked | Permanently retired. Cannot be reinstated — any further status change is rejected with `409`. |
| Expired | Past its expiry date. Reachable manually via `PATCH /api-key/status` today; nothing sets it automatically yet — the Agent expiry job that would do so on `expiresAt` isn't built. |

The transition guard is a terminal-state check, not a full matrix: any status can be set as the new one, *unless* the key's current status is already `Revoked` or `Expired`.

State history is stored in a separate, **append-only** status table (`GET /api-key/status/history` returns the full timeline). The key record itself is never modified — every transition is a new row, and even deleting a key (`DELETE /api-key`) only soft-deletes its status/action/idempotency-key rows, never the key row itself. This means the full history of a key's lifecycle is always available for audit and forensic investigation.

### Per-key action permissions

Not every API key should have the same privileges. Locksmith assigns actions to keys via a normalized junction table (`api_key_actions`), giving each key an independently configurable set of allowed operations:

- `Read`
- `Write`
- `Delete`
- `Execute`

This follows the principle of least privilege: a key carries only the permissions it needs, so a compromised key's blast radius is bounded by its specific grants. Actions can be set at creation time (via the `actions` field on `POST /api-key`) and changed afterward through dedicated endpoints: list the active set, replace it wholesale, or grant/revoke a single action — each backed by a database-level unique constraint that rejects a duplicate active grant even under concurrent requests.

### Rotating and deleting keys

`POST /api-key/rotate` atomically deletes the current key and issues a replacement carrying the same active actions — for routine credential rotation without a service having to re-request its permission set. `DELETE /api-key` retires a key: its status history, actions, and idempotency-key record are soft-deleted, so it stops resolving anywhere the idempotency key or its secret is used to identify a key, while the underlying row (and its audit trail) is preserved.

### Validating and retrieving keys

`POST /api-key/validate` hashes a presented secret, looks it up, and reports whether it's known and currently `Active`. `POST /api-key/secret` re-derives the encryption key from a caller-supplied idempotency key and decrypts the stored secret — this is how a caller recovers a secret it failed to persist after creation, without Locksmith ever having stored it in plaintext. A caller can also read its own key's metadata, status, and granted actions directly, by presenting the raw secret in an `X-Api-Key` header rather than the idempotency key.

### Authentication of the management surface

Locksmith's own management endpoints are protected by a static bearer token: a single pre-shared secret configured via environment variable. Every incoming request must carry it as `Authorization: Bearer <secret>`. Comparison uses `CryptographicOperations.FixedTimeEquals` (constant-time) to prevent timing attacks. Any request without a valid token is rejected with a 401 before any handler runs.

This approach was chosen because, at this stage, Locksmith has exactly one internal caller. JWT, OIDC, and mTLS all add complexity that buys nothing in a single-caller model. The trade-off is explicit: there is no per-caller identity and no granular revocation — if the token leaks, the only remedy is rotating the secret for all callers simultaneously. This decision carries an obligation to revisit authentication before a second independent caller is onboarded.

## What Locksmith will do next

The following capabilities are designed but not yet built — see [TODO.md](TODO.md) for exact status:

**Idempotency and reliability**
- `Idempotency-Key` request-deduplication semantics (client-supplied header, `409` on replay with a different body) — distinct from the idempotency key Locksmith itself generates and returns at creation, which is fully implemented
- Automated key expiry — an Agent background job that transitions keys to `Expired` once `expiresAt` has passed; today expiry only happens if a caller sets it manually via `PATCH /api-key/status`

**Operational capabilities**
- Rate limiting enforced at the middleware layer, per resolved API key (the identity-resolution plumbing already exists; no limiter is wired in yet)
- OpenTelemetry traces and metrics
- CORS configuration for cross-origin callers

**Authentication evolution**
- When a second independent caller is required, authentication will migrate to per-client API keys — the same model Locksmith itself issues. This closes the gap between the single-token model and the auditable, per-caller identity that multiple consumers require.

## Key design principles

- **Hash at rest, never store secrets.** Raw keys are ephemeral. Only hashes and salts live in the database.
- **Append-only audit log.** State transitions are new rows, not updates. Evidence of what happened and when is never overwritten.
- **Least privilege by default.** Keys are inactive at creation and carry no permissions until explicitly granted.
- **Constant-time comparisons everywhere.** All token and key comparisons use timing-safe equality to prevent side-channel attacks.
- **Bounded blast radius.** A compromised key can only do what its specific permissions allow. A leaked management token can be rotated without touching key data.