# Locksmith — Threat Model

**Application Version: 1.1.1**

**Description**: Locksmith is an ASP.NET Core REST API that manages the full lifecycle of API keys — creation, validation, secret retrieval, activation, rotation, revocation, deletion, and per-key permissions. It is deployed as a Docker container and is accessible only to a single trusted internal service over the container's exposed HTTP port. The management surface is protected by a static bearer token (pre-shared secret injected at deploy time).

**Document Owner**: Anthony Noel Weiß  
**Participants**: Anthony Noel Weiß  
**Reviewer**: Claude Anthropic

---

## External Dependencies

| ID | Description | Trust |
|----|-------------|-------|
| ED-1 | HTTP port exposed from the Docker container (`:8080`) — the only network ingress point | Untrusted; all traffic is authenticated at the application layer |
| ED-2 | PostgreSQL — stores encrypted secrets, secret and idempotency-key hashes, Argon2id salts, state history, and action assignments | Trusted infrastructure; access must be limited to the Locksmith container only |
| ED-3 | Redis — rate-limit sliding windows (keyed by API key ID or credential hash) and the distributed cache | Trusted infrastructure; no raw secrets stored |
| ED-4 | Log sinks — Console and rolling files under `logs/` today; a log aggregator such as Seq is planned but not configured | Trusted infrastructure; logs must never contain raw secrets, idempotency keys, or the bearer token |

---

## Entry Points

Implemented today. Note there is no `{id}` path segment anywhere — reads that target a specific key resolve it via the caller-presented secret in an `X-Api-Key` header; mutations resolve it via an `idempotencyKey` in the request body. See [api-surface.md](../Architecture/api-surface.md#identifying-a-key).

| ID | Name | Description | Trust Level |
|----|------|-------------|-------------|
| EP-1 | `POST /api/v{n}/api-key` | Creates a new API key record; returns the raw secret and an idempotency key in the response | TL-2 (authenticated management caller) |
| EP-2 | `GET /api/v{n}/api-key/all` | Returns paginated key metadata for all keys | TL-2 |
| EP-3 | `GET /api/v{n}/api-key` | Returns metadata for the key identified by `X-Api-Key` | TL-2 + key secret |
| EP-4 | `POST /api/v{n}/api-key/secret` | Returns the raw key value, decrypted via the idempotency key in the request body | TL-2 |
| EP-5 | `POST /api/v{n}/api-key/validate` | Hashes a presented secret and reports whether it's known and currently `Active`; does not return the raw secret | TL-2 |
| EP-6 | `POST /api/v{n}/api-key/rotate` | Atomically deletes the current key and issues a replacement with the same active actions; returns the new raw secret | TL-2 |
| EP-7 | `DELETE /api/v{n}/api-key` | Soft-deletes a key's status, action, and idempotency-key records, retiring it | TL-2 |
| EP-8 | `GET`/`PATCH /api/v{n}/api-key/status[/history]` | Reads or transitions a key's lifecycle status | TL-2 (+ key secret for the `GET` routes) |
| EP-9 | `GET`/`PUT`/`POST`/`DELETE /api/v{n}/api-key/actions[/{action}]` | Reads or changes a key's granted permissions | TL-2 (+ key secret for the `GET` route) |
| EP-10 | `GET /health` | Liveness probe; no authentication required | TL-1 (unauthenticated) |
| EP-11 | `GET /health/ready` | Readiness probe; no authentication required | TL-1 (unauthenticated) |

All management entry points (EP-1 through EP-9) require `Authorization: Bearer <secret>` with constant-time comparison (`CryptographicOperations.FixedTimeEquals`). EP-3 and the `GET` routes under EP-8/EP-9 additionally require a valid `X-Api-Key` header carrying the target key's own raw secret — a caller can read a key's own metadata/status/actions without knowing its idempotency key, but the secret itself is the credential being checked. Those same four `X-Api-Key`-resolved `GET` routes are rate limited per key via a Redis-backed sliding window (`RateLimitFilter`); every `idempotencyKey`/secret-identified mutation is rate limited the same way, partitioned instead on a hash of the request-body credential (`CredentialRateLimitFilter`). Only `create` and `list all` are unthrottled, since neither targets an existing key.

Planned, not yet implemented — no attack surface exists for this yet: client-supplied `Idempotency-Key` request-deduplication semantics. See [TODO.md](../TODO.md).

---

## Exit Points

| ID | Name | Description | Trust Level |
|----|------|-------------|-------------|
| XP-1 | HTTP response body | Returns key metadata, status codes, and RFC 9457 ProblemDetails on error | TL-2 — raw key returned on EP-1 (creation), EP-4 (retrieval), and EP-6 (rotation); all must travel over TLS |
| XP-2 | PostgreSQL writes | Persists ciphertext, hashes, salt, state transitions, action assignments, and audit fields | TL-3 (infrastructure) |
| XP-3 | Log events | Structured logs (Console/File) enriched with request context; must never include raw keys or secrets | TL-3 (infrastructure) |
| XP-4 | Redis writes | Rate-limit window entries; no raw secrets (credential partitions use the same SHA-256 lookup hash stored in PostgreSQL) | TL-3 (infrastructure) |

---

## Assets

| ID | Name | Description | Trust Level Required |
|----|------|-------------|----------------------|
| A-1 | Management bearer token | The pre-shared secret that authorises all key management operations. Highest-value secret in the system — compromise gives full management access with no per-caller revocation. | TL-4 (infrastructure admin) for configuration; TL-2 for use |
| A-2 | Raw API key (stored + in-transit) | Stored in PostgreSQL only as AES-256-GCM ciphertext; returned in the EP-1 (create), EP-4 (retrieve), and EP-6 (rotate) response bodies. Anyone holding a key's idempotency key (A-6) plus database or EP-4 access can recover it. | TL-2 — those responses must travel over TLS; TL-3 for encrypted storage |
| A-3 | Lookup hashes + salts | `SecretHash` and `IdempotencyKeyHash` are unsalted SHA-256 over high-entropy random values (32 and 96 bytes), so offline brute force is infeasible; the per-key `Salt` is the Argon2id salt for DEK derivation, not a password-hash salt. | TL-3 (database infrastructure) |
| A-4 | API key state & action records | Lifecycle history (`ApiKeyStatus`) and per-key permissions (`ApiKeyAction`) in PostgreSQL. Tampering can silently re-enable revoked keys or escalate allowed actions. | TL-3 |
| A-5 | Audit trail | `CreatedBy` (always the constant `api-client` — there is no per-caller identity) and `DeletedAt` fields, plus log events. No `UpdatedBy`/`DeletedBy` exist — records are append-only, not updated. | TL-3 |
| A-6 | Idempotency keys | Server-generated, 96 random bytes, returned once at creation/rotation. They identify the target key on every mutation **and** are the Argon2id password the DEK is derived from — effectively the decryption key for the stored secret. Only their SHA-256 hash is stored. | TL-2 — held by the caller only |
| A-7 | Data encryption keys (DEKs) | Per-key AES-256 keys derived on demand via Argon2id from the idempotency key (A-6) and the stored salt. Never persisted, so there is no master key to steal — compromise of a single key's secret requires that key's idempotency key. | TL-5 (in process memory only) |

---

## Trust Levels

| ID | Name | Description |
|----|------|-------------|
| TL-1 | Anonymous caller | Any process that can reach the container port. No credentials presented. May access only health probe endpoints. |
| TL-2 | Authenticated management caller | A single trusted internal service that presents the correct static bearer token. Has full access to all management endpoints (create, rotate, revoke, etc.). |
| TL-3 | Infrastructure services | PostgreSQL, Redis, and log storage running within the Docker network / host. Trusted as operating-environment components, not as application-layer principals. |
| TL-4 | Infrastructure admin | A person or process with access to the Docker host, environment variables, or secrets manager. Can read or rotate the management bearer token and database credentials. |
| TL-5 | Locksmith API process | The running application itself. Trusted with full internal access: reads secrets from environment, writes to all backing stores. |

---

## Determine Threats (STRIDE)

| Type | Threat | Security Control |
|------|--------|-----------------|
| Spoofing | An attacker intercepts or steals the management bearer token and masquerades as the trusted internal service, gaining full control over key management operations. | Token injected via environment variable or secrets manager — never committed to source control. Constant-time comparison (`FixedTimeEquals`) prevents timing-based guessing. All management traffic must travel over TLS to prevent interception. Token rotation must coordinate caller and server simultaneously. |
| Spoofing | An attacker obtains a raw API key — by intercepting EP-1, EP-4, or EP-6 responses in transit, capturing it from caller logs, or calling EP-4 with a stolen bearer token *and* the key's idempotency key — and uses it to authenticate as a legitimate consumer. | Transport must use TLS to prevent interception. EP-4 requires both the bearer token and the target key's idempotency key, and is rate limited per idempotency-key hash. Every response carries `Cache-Control: no-store` for secret-bearing endpoints. Callers are responsible for securing the key after receipt. |
| Tampering | An attacker with database access directly modifies `ApiKeyStatus` or `ApiKeyAction` records — for example, re-activating a revoked key or escalating its allowed actions — bypassing the application entirely. | PostgreSQL access must be restricted to the Locksmith container within the Docker network (no external port exposure outside local dev). Append-only is enforced only in `AppDbContext` today; database-level privileges are recommended but not applied (see [database.md](../Architecture/database.md#setting-up-database-privileges-production)). Audit fields (`CreatedAt`, `DeletedAt`) record when each row changed but cannot prevent direct DB writes. |
| Tampering | A man-in-the-middle between the management caller and the API modifies request bodies (e.g., changing the `expiresAt` or `actions` on a creation request). | TLS between caller and container prevents in-transit modification. Request records are validated with DataAnnotations (`[Required]`, `[RegularExpression]`) plus in-service checks (e.g. `ExpiresAt` in the future, exact action-name matching); failures return `422`. |
| Repudiation | A management caller denies having created, rotated, or revoked a key — for example, to avoid accountability for a leaked credential. | With a single static bearer token there is exactly one caller, so attribution is to that caller by definition; `CreatedBy` is the constant `api-client`. Each row carries `CreatedAt` and, on soft delete/revoke, `DeletedAt`; Serilog request logs provide a secondary record. Per-caller identity requires the authentication evolution in [TODO.md](../TODO.md). |
| Information Disclosure | The management bearer token (A-1) appears in structured log output, unhandled exception messages, or environment dumps and is exfiltrated via a log sink or export. | Serilog request logging does not log request headers by default, but no destructuring/redaction policy for `Authorization` exists yet (planned — see TODO). `GlobalExceptionHandler` strips exception details from 500 responses outside the Development environment. The bearer token must never be written to any log sink. |
| Information Disclosure | An attacker with database read access tries to decrypt stored secrets (A-2). | There is no stored master key: each DEK (A-7) is derived from that key's idempotency key, which only the caller holds (just its hash is stored). Recovering a secret therefore requires the specific idempotency key. Argon2id makes guessing it expensive, on top of its 96 bytes of entropy. |
| Information Disclosure | Lookup hashes and salts (A-3) are extracted from PostgreSQL by an attacker who gains database access. | The hashed values are high-entropy random tokens (32-byte secret, 96-byte idempotency key), so unsalted SHA-256 is not crackable in practice; salts are only useful together with the idempotency key. All queries go through EF Core with parameterized SQL — there is no raw SQL built from input. |
| Information Disclosure | A management endpoint returns key metadata (status, actions) to an unauthenticated caller, leaking the existence of a key. | All management endpoints (EP-1 through EP-9) are gated behind bearer-token middleware. The handler is never reached without a valid token; the `X-Api-Key`-resolved routes additionally require the target key's own secret. Health probes (EP-10, EP-11) return no sensitive data. |
| Denial of Service | An attacker floods the management endpoints with requests, exhausting the connection pool, database connections, or compute resources. | Rate limiting (Redis-backed sliding window) covers every EP-1–EP-9 endpoint except `create`/`list all`: the four `X-Api-Key`-resolved reads are partitioned by resolved API key, and every `idempotencyKey`/secret-identified mutation is partitioned by a hash of the request-body credential. Container resource limits in Docker Compose. Liveness probe (EP-10) performs no dependency checks, remaining fast regardless of backing-store health. |
| Denial of Service | An attacker sends oversized request bodies to creation or update endpoints, exhausting memory or triggering excessive allocations. | ASP.NET Core default request body size limit applies. Plan to add a per-endpoint limit appropriate for key management payloads (a few KB). |
| Elevation of Privilege | An unauthenticated caller bypasses the bearer-token check and reaches a management handler — for example, due to a misconfigured middleware order or a route that falls outside the auth policy. | Authentication middleware runs before any controller dispatch (enforced by `UseApiPipeline` ordering). All management routes inherit from the base `Controller` class, which carries `[Authorize]` and the versioned route prefix. Health probes are explicitly exempted by route, not by a wildcard bypass. |
| Elevation of Privilege | A replayed creation or rotation request produces an extra key, effectively doubling access. | Idempotency keys are server-generated per key and enforced unique by `IX_IdempotencyKeys_IdempotencyKeyHash`. Rotation soft-deletes the old idempotency key in the same transaction, so replaying a rotate with the old key returns `404`. Replaying a *create* does issue another key — client-supplied `Idempotency-Key` request deduplication is planned (see TODO). |

---

## Threat Analysis

```mermaid
flowchart LR
    subgraph Callers
        A([Management Caller\nTL-2])
        B([Anonymous Caller\nTL-1])
    end

    subgraph Docker Network
        subgraph Locksmith API
            M[Bearer Token\nMiddleware]
            H[Management\nHandlers\nEP-1 to EP-9]
            HP[Health Probes\nEP-10, EP-11]
        end

        DB[(PostgreSQL\nApiKeys / ApiKeyStatus\n/ ApiKeyAction)]
        R[(Redis\nRate-limit counters)]
        LOG[Log sinks\nConsole / File]
    end

    A -->|"Authorization: Bearer &lt;secret&gt;\n(over TLS)"| M
    B -->|No credentials| M

    M -->|Valid token| H
    M -->|"No / invalid token → 401"| A
    B -->|"/health, /health/ready only"| HP

    H -->|"Read / write key records\n+ encrypted raw key"| DB
    H -->|Rate-limit check| R
    H -->|"Raw key in response\n(EP-1 create, EP-4 retrieve, EP-6 rotate)"| A
    H -->|Structured log events| LOG

    HP -->|DB ping (readiness only)| DB
```

### Key trust boundaries

- The only trusted entry into the system is a request carrying the correct static bearer token over TLS. Everything else is unauthenticated.
- The Docker network boundary separates Locksmith from its backing services (PostgreSQL, Redis). No backing service port should be exposed to the host except for local development (the dev `docker-compose.yml` does publish them).
- The raw API key crosses the trust boundary on EP-1 (creation), EP-4 (retrieval), and EP-6 (rotation). It is stored encrypted in PostgreSQL; the idempotency key (A-6), held only by the caller, is what unlocks it, making it the highest-value per-key secret. The management bearer token (A-1) remains the highest-value system-wide secret.
