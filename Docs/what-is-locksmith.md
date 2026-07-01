# What is Locksmith?

Locksmith is a self-contained REST API service being built to manage the full lifecycle of API keys for internal services. Its job is simple to state: create API keys securely, control what those keys are allowed to do, and provide the infrastructure to activate, deactivate, rotate, and revoke them over time — while maintaining a permanent, tamper-evident audit trail of every state change. Today it covers secure creation, listing, secret validation, and secret retrieval; the state-transition and permission-management endpoints described in [what Locksmith will do next](#what-locksmith-will-do-next) are designed but not yet built. See [TODO.md](TODO.md) for exact status.

## The problem it solves

Any service that exposes a programmatic interface needs a way to authenticate its callers and constrain their privileges. Rolling that logic into each service individually leads to inconsistent security, duplicate code, and audit gaps. Locksmith externalises that concern: one service issues and manages API keys so individual applications do not have to.

## What Locksmith does today

### Secure key creation

When a caller requests a new API key, Locksmith generates a cryptographically random secret and a random idempotency key, hashes the secret with SHA-256 for lookup, and encrypts it at rest with AES-256-GCM using a per-key DEK derived via Argon2id. The raw secret and the idempotency key are returned to the caller exactly once at creation. Unlike a pure hash-and-discard design, the raw secret *can* be recovered later — by presenting the idempotency key to the retrieval endpoint, which re-derives the DEK and decrypts the stored ciphertext. This trade-off exists because the caller needs a way to re-fetch a secret it failed to persist on its end without Locksmith ever storing the secret in plaintext.

Keys start in an **Inactive** state. Nothing currently transitions a key out of `Inactive` — activation, deactivation, rotation, and revocation are designed (see [ADR-001](Decisions/ADR-001-api-key-lifecycle.md)) but not yet implemented. See [what Locksmith will do next](#what-locksmith-will-do-next).

### Lifecycle state machine

Each key moves through a defined set of states:

| State | Meaning |
|---|---|
| Inactive | Created but not yet usable. Default state at creation, and currently the only state ever reached. |
| Active | Authorized to authenticate requests. Not yet reachable — no activation endpoint exists yet. |
| Revoked | Permanently retired. Cannot be reinstated. Not yet reachable. |
| Expired | Past its expiry date. Not yet reachable — no expiry job exists yet. |

State history is stored in a separate, **append-only** status table. The key record itself is never modified or deleted — every transition is a new row. This means the full history of a key's lifecycle is always available for audit and forensic investigation, and soft deletes preserve evidence of the moment a key was retired.

### Per-key action permissions

Not every API key should have the same privileges. Locksmith assigns actions to keys via a normalized junction table (`api_key_actions`), giving each key an independently configurable set of allowed operations:

- `Read`
- `Write`
- `Delete`
- `Execute`

This follows the principle of least privilege: a key carries only the permissions it needs, so a compromised key's blast radius is bounded by its specific grants. Today, actions can only be set at creation time (via the `actions` field on `POST /api-keys`); the dedicated grant/revoke/list/replace endpoints described in [what Locksmith will do next](#what-locksmith-will-do-next) don't exist yet, so permissions can't currently be changed after a key is issued.

### Validating and retrieving keys

`POST /api-keys/validate` hashes a presented secret, looks it up, and reports whether it's known and what status it currently has. `POST /api-keys/retrieve-secret` re-derives the encryption key from a caller-supplied idempotency key and decrypts the stored secret — this is how a caller recovers a secret it failed to persist after creation, without Locksmith ever having stored it in plaintext.

### Authentication of the management surface

Locksmith's own management endpoints are protected by a static bearer token: a single pre-shared secret configured via environment variable. Every incoming request must carry it as `Authorization: Bearer <secret>`. Comparison uses `CryptographicOperations.FixedTimeEquals` (constant-time) to prevent timing attacks. Any request without a valid token is rejected with a 401 before any handler runs.

This approach was chosen because, at this stage, Locksmith has exactly one internal caller. JWT, OIDC, and mTLS all add complexity that buys nothing in a single-caller model. The trade-off is explicit: there is no per-caller identity and no granular revocation — if the token leaks, the only remedy is rotating the secret for all callers simultaneously. This decision carries an obligation to revisit authentication before a second independent caller is onboarded.

## What Locksmith will do next

The following capabilities are designed (see the linked ADRs) but not yet built — see [api-surface.md](Architecture/api-surface.md#planned-endpoints-not-yet-implemented) and [TODO.md](TODO.md) for exact status:

**Key lifecycle endpoints**
- `PATCH /api-keys/{id}` — activate or deactivate a key
- `POST /api-keys/{id}/rotate` — issue a new secret and invalidate the old one atomically
- `DELETE /api-keys/{id}` — revoke a key permanently

**Permission management endpoints**
- `GET /api-keys/{id}/actions` — list an existing key's granted actions
- `PUT /api-keys/{id}/actions` — replace the full action set
- `POST /api-keys/{id}/actions/{action}` — grant a single permission after issuance
- `DELETE /api-keys/{id}/actions/{action}` — revoke a single permission

**Operational capabilities**
- Rate limiting enforced at the middleware layer per key
- OpenTelemetry traces and metrics
- CORS configuration for cross-origin callers
- Structured Serilog logging already in place; spans and metrics to follow

**Authentication evolution**
- When a second independent caller is required, authentication will migrate to per-client API keys — the same model Locksmith itself issues. This closes the gap between the single-token model and the auditable, per-caller identity that multiple consumers require.

## Key design principles

- **Hash at rest, never store secrets.** Raw keys are ephemeral. Only hashes and salts live in the database.
- **Append-only audit log.** State transitions are new rows, not updates. Evidence of what happened and when is never overwritten.
- **Least privilege by default.** Keys are inactive at creation and carry no permissions until explicitly granted.
- **Constant-time comparisons everywhere.** All token and key comparisons use timing-safe equality to prevent side-channel attacks.
- **Bounded blast radius.** A compromised key can only do what its specific permissions allow. A leaked management token can be rotated without touching key data.