# What is Locksmith?

Locksmith is a self-contained REST API service that manages the full lifecycle of API keys for internal services. Its job is simple to state: it creates API keys securely, controls what those keys are allowed to do, and provides the infrastructure to activate, deactivate, rotate, and revoke them over time — while maintaining a permanent, tamper-evident audit trail of every state change.

## The problem it solves

Any service that exposes a programmatic interface needs a way to authenticate its callers and constrain their privileges. Rolling that logic into each service individually leads to inconsistent security, duplicate code, and audit gaps. Locksmith externalises that concern: one service issues and manages API keys so individual applications do not have to.

## What Locksmith does today

### Secure key creation

When a caller requests a new API key, Locksmith generates a cryptographically random secret, hashes it with a salt using a secure algorithm, and stores only the hash. The raw key is returned to the caller exactly once and never persisted. All subsequent validation re-hashes the presented key and compares against the stored hash. There is no way to recover a key after creation — only to revoke it and issue a new one.

Keys start in an **inactive** state. The caller must explicitly activate a key before it can be used to authenticate requests. This prevents accidentally live credentials from being issued before a consumer is ready to receive them.

### Lifecycle state machine

Each key moves through a defined set of states:

| State | Meaning |
|---|---|
| Inactive | Created but not yet usable. Default state at creation. |
| Active | Authorized to authenticate requests. |
| Deactivated | Temporarily suspended. Can be re-activated. |
| Revoked | Permanently retired. Cannot be reinstated. |

State history is stored in a separate, **append-only** status table. The key record itself is never modified or deleted — every transition is a new row. This means the full history of a key's lifecycle is always available for audit and forensic investigation, and soft deletes preserve evidence of the moment a key was retired.

### Per-key action permissions

Not every API key should have the same privileges. Locksmith assigns actions to keys via a normalized junction table, giving each key an independently configurable set of allowed operations:

- `read`
- `write`
- `delete`
- `execute`

This follows the principle of least privilege: a key carries only the permissions it needs, so a compromised key's blast radius is bounded by its specific grants. Permissions can be added or revoked without touching the key itself.

Only the key owner or an administrator can modify a key's permissions, enforced at the authorization layer. This is a deliberate constraint — without it, privilege escalation becomes a realistic attack vector.

### Authentication of the management surface

Locksmith's own management endpoints are protected by a static bearer token: a single pre-shared secret configured via environment variable. Every incoming request must carry it as `Authorization: Bearer <secret>`. Comparison uses `CryptographicOperations.FixedTimeEquals` (constant-time) to prevent timing attacks. Any request without a valid token is rejected with a 401 before any handler runs.

This approach was chosen because, at this stage, Locksmith has exactly one internal caller. JWT, OIDC, and mTLS all add complexity that buys nothing in a single-caller model. The trade-off is explicit: there is no per-caller identity and no granular revocation — if the token leaks, the only remedy is rotating the secret for all callers simultaneously. This decision carries an obligation to revisit authentication before a second independent caller is onboarded.

## What Locksmith will do next

The following capabilities are planned and directly follow from the design decisions already made:

**Key lifecycle endpoints**
- `POST /api-keys` — create and issue a key (returns raw secret once)
- `PUT /api-keys/{id}/activate` — move key to Active
- `PUT /api-keys/{id}/deactivate` — suspend without revoking
- `PUT /api-keys/{id}/revoke` — permanently retire
- `PUT /api-keys/{id}/rotate` — issue a new key and revoke the old one atomically

**Permission management endpoints**
- `POST /api-keys/{id}/actions` — grant a permission
- `DELETE /api-keys/{id}/actions/{action}` — revoke a permission

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