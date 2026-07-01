# Locksmith — Threat Model

**Application Version: v0.0.1-alpha**

**Description**: Locksmith is an ASP.NET Core REST API that manages the full lifecycle of API keys — creation, activation, rotation, and revocation. It is deployed as a Docker container and is accessible only to a single trusted internal service over the container's exposed HTTP port. The management surface is protected by a static bearer token (pre-shared secret injected at deploy time).

**Document Owner**: Anthony Noel Weiß  
**Participants**: Anthony Noel Weiß  
**Reviewer**: Claude Anthropic

---

## External Dependencies

| ID | Description | Trust |
|----|-------------|-------|
| ED-1 | HTTP port exposed from the Docker container (`:8080`) — the only network ingress point | Untrusted; all traffic is authenticated at the application layer |
| ED-2 | PostgreSQL — stores API key hashes, salts, state history, and action assignments | Trusted infrastructure; access must be limited to the Locksmith container only |
| ED-3 | Redis — planned; used for caching and rate-limit counters | Trusted infrastructure; no raw secrets stored |
| ED-4 | Seq — structured log aggregation; receives all application log output | Trusted infrastructure; logs must never contain raw secrets or raw API keys |

---

## Entry Points

Implemented today:

| ID | Name | Description | Trust Level |
|----|------|-------------|-------------|
| EP-1 | `POST /api/v{n}/api-keys` | Creates a new API key record; returns the raw secret and an idempotency key in the response | TL-2 (authenticated management caller) |
| EP-2 | `GET /api/v{n}/api-keys` | Returns paginated key metadata | TL-2 |
| EP-3 | `GET /api/v{n}/api-keys/{id}` | Returns metadata for a single key | TL-2 |
| EP-4 | `POST /api/v{n}/api-keys/retrieve-secret` | Returns the raw key value, decrypted via the idempotency key in the request body | TL-2 |
| EP-5 | `POST /api/v{n}/api-keys/validate` | Hashes a presented secret and reports whether it's known and its current status; does not return the raw secret | TL-2 |
| EP-6 | `GET /health` | Liveness probe; no authentication required | TL-1 (unauthenticated) |
| EP-7 | `GET /health/ready` | Readiness probe; no authentication required | TL-1 (unauthenticated) |

All management entry points (EP-1 through EP-5) require `Authorization: Bearer <secret>` with constant-time comparison (`CryptographicOperations.FixedTimeEquals`).

Planned, not yet implemented — no attack surface exists for these yet, listed here so the threat model doesn't need re-deriving when they land: `PATCH /api/v{n}/api-keys/{id}` (activate/deactivate), `POST /api/v{n}/api-keys/{id}/rotate`, `DELETE /api/v{n}/api-keys/{id}` (revoke), and the `/actions` grant/revoke/list/replace endpoints. See [TODO.md](../TODO.md).

---

## Exit Points

| ID | Name | Description | Trust Level |
|----|------|-------------|-------------|
| XP-1 | HTTP response body | Returns key metadata, status codes, and RFC 9457 ProblemDetails on error | TL-2 — raw key returned on EP-1 (creation) and EP-4 (retrieval); both must travel over TLS |
| XP-2 | PostgreSQL writes | Persists key hash, salt, state transitions, action assignments, and audit fields | TL-3 (infrastructure) |
| XP-3 | Seq log events | Structured logs enriched with request context; must never include raw keys or secrets | TL-3 (infrastructure) |
| XP-4 | Redis writes | Planned rate-limit counters and cache entries; no raw secrets | TL-3 (infrastructure) |

---

## Assets

| ID | Name | Description | Trust Level Required |
|----|------|-------------|----------------------|
| A-1 | Management bearer token | The pre-shared secret that authorises all key management operations. Highest-value secret in the system — compromise gives full management access with no per-caller revocation. | TL-4 (infrastructure admin) for configuration; TL-2 for use |
| A-2 | Raw API key (stored + in-transit) | The plaintext key is stored encrypted in PostgreSQL and is retrievable via EP-4. It also appears in the EP-1 creation response body. Compromise of the encryption key (A-7) exposes all stored raw keys at once. | TL-2 — retrieval and creation responses must travel over TLS; TL-3 for encrypted storage |
| A-3 | API key hashes + salts | Stored in PostgreSQL alongside the encrypted raw key. Used to validate keys on subsequent requests. Compromise enables offline cracking if the hashing algorithm or salt entropy is weak. | TL-3 (database infrastructure) |
| A-7 | Data encryption key (DEK) | The symmetric key used to encrypt raw API keys at rest in PostgreSQL. If compromised, all stored raw keys are immediately recoverable without cracking. Must be stored in a secrets manager or HSM, not alongside the data it protects. | TL-4 (infrastructure admin) |
| A-4 | API key state & action records | Lifecycle history (`ApiKeyStatus`) and per-key permissions (`ApiKeyAction`) in PostgreSQL. Tampering can silently re-enable revoked keys or escalate allowed actions. | TL-3 |
| A-5 | Audit trail | `CreatedBy` and `DeletedAt` fields (no `UpdatedBy`/`DeletedBy` exist — records are append-only, not updated) and Seq log events. Used for repudiation defence. | TL-3 |
| A-6 | Idempotency keys | Caller-supplied GUIDs used to prevent duplicate operations. Tied to specific management actions. | TL-2 |

---

## Trust Levels

| ID | Name | Description |
|----|------|-------------|
| TL-1 | Anonymous caller | Any process that can reach the container port. No credentials presented. May access only health probe endpoints. |
| TL-2 | Authenticated management caller | A single trusted internal service that presents the correct static bearer token. Has full access to all management endpoints (create, rotate, revoke, etc.). |
| TL-3 | Infrastructure services | PostgreSQL, Redis, and Seq running within the Docker network. Trusted as operating-environment components, not as application-layer principals. |
| TL-4 | Infrastructure admin | A person or process with access to the Docker host, environment variables, or secrets manager. Can read or rotate the management bearer token and database credentials. |
| TL-5 | Locksmith API process | The running application itself. Trusted with full internal access: reads secrets from environment, writes to all backing stores. |

---

## Determine Threats (STRIDE)

| Type | Threat | Security Control |
|------|--------|-----------------|
| Spoofing | An attacker intercepts or steals the management bearer token and masquerades as the trusted internal service, gaining full control over key management operations. | Token injected via environment variable or secrets manager — never committed to source control. Constant-time comparison (`FixedTimeEquals`) prevents timing-based guessing. All management traffic must travel over TLS to prevent interception. Token rotation must coordinate caller and server simultaneously. |
| Spoofing | An attacker obtains a raw API key — by intercepting EP-1 or EP-4 responses in transit, capturing it from caller logs, or calling EP-4 directly with a stolen bearer token — and uses it to authenticate as a legitimate consumer. | Transport must use TLS to prevent interception. The key retrieval endpoint (EP-4) is gated behind the same bearer-token middleware as all other management endpoints. Callers are responsible for securing the key after receipt. |
| Tampering | An attacker with database access directly modifies `ApiKeyStatus` or `ApiKeyAction` records — for example, re-activating a revoked key or escalating its allowed actions — bypassing the application entirely. | PostgreSQL access must be restricted to the Locksmith container IP within the Docker network (no external port exposure). Application-level audit fields (`CreatedBy`, `CreatedAt`) record who wrote each row but cannot prevent direct DB writes. |
| Tampering | A man-in-the-middle between the management caller and the API modifies request bodies (e.g., changing the `expiresAt` or `actions` on a creation request). | TLS between caller and container prevents in-transit modification. Input validation via `FluentValidation` (planned) validates all input at the application boundary. |
| Repudiation | A management caller denies having created, rotated, or revoked a key — for example, to avoid accountability for a leaked credential. | `CreatedBy` written on every insert, and `DeletedAt` on every soft-delete/revoke, per row. Structured log events forwarded to Seq provide a secondary audit record. Idempotency keys (A-6) bind a specific management action to a unique caller-supplied GUID, creating a durable correlation point. |
| Information Disclosure | The management bearer token (A-1) appears in structured log output, unhandled exception messages, or environment dumps and is exfiltrated via Seq or a log export. | Serilog must use destructuring policies that redact the `Authorization` header value. `GlobalExceptionHandler` strips exception details from 500 responses outside the Development environment. The bearer token must never be written to any log sink. |
| Information Disclosure | The data encryption key (A-7) is compromised — for example, committed to source control, logged, or leaked from the secrets manager — allowing an attacker who also has database read access to decrypt all stored raw API keys at once. | The DEK must be stored in a secrets manager or HSM, never in source control or alongside the database. The DEK and the database credentials must not share the same secret store entry. Rotation of the DEK requires re-encrypting all stored keys. |
| Information Disclosure | API key hashes and salts (A-3) are extracted from PostgreSQL by an attacker who gains database access (e.g., via SQL injection in a future endpoint or direct DB access). | Hashes are produced with a secure algorithm (e.g., PBKDF2 or Argon2) with per-key salts, making offline cracking computationally expensive. Input validation via `FluentValidation` (planned) prevents SQL injection vectors into EF Core queries. |
| Information Disclosure | A management endpoint returns key metadata (status, actions) to an unauthenticated caller, leaking the existence of a key. | All management endpoints (EP-1 through EP-5) are gated behind bearer-token middleware. The handler is never reached without a valid token. Health probes (EP-6, EP-7) return no sensitive data. |
| Denial of Service | An attacker floods the management endpoints with requests, exhausting the connection pool, database connections, or compute resources. | Rate limiting (planned — Redis-backed sliding window). Container resource limits in Docker Compose. Liveness probe (EP-6) performs no dependency checks, remaining fast regardless of backing-store health. |
| Denial of Service | An attacker sends oversized request bodies to creation or update endpoints, exhausting memory or triggering excessive allocations. | ASP.NET Core default request body size limit applies. Plan to add a per-endpoint limit appropriate for key management payloads (a few KB). |
| Elevation of Privilege | An unauthenticated caller bypasses the bearer-token check and reaches a management handler — for example, due to a misconfigured middleware order or a route that falls outside the auth policy. | Authentication middleware runs before any controller dispatch (enforced by `UseApiPipeline` ordering). All management routes inherit from the base `Controller` class which carries the versioned route prefix. Health probes are explicitly exempted by route, not by a wildcard bypass. |
| Elevation of Privilege | A bug in idempotency-key processing allows a replayed creation or rotation request to produce a second key for the same idempotency key, effectively doubling access. | Idempotency keys are stored and checked before execution; a duplicate key returns the original response without re-executing the operation (planned: EF Core unique index on `IdempotencyKey`). |

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
            H[Management\nHandlers\nEP-1 to EP-5]
            HP[Health Probes\nEP-6, EP-7]
        end

        DB[(PostgreSQL\nApiKeys / ApiKeyStatus\n/ ApiKeyAction)]
        R[(Redis\nRate-limit counters)]
        SEQ[Seq\nLog aggregation]
    end

    A -->|"Authorization: Bearer &lt;secret&gt;\n(over TLS)"| M
    B -->|No credentials| M

    M -->|Valid token| H
    M -->|"No / invalid token → 401"| A
    B -->|"/health, /health/ready only"| HP

    H -->|"Read / write key records\n+ encrypted raw key"| DB
    H -->|Rate-limit check| R
    H -->|"Raw key in response\n(EP-1 creation + EP-4 retrieval)"| A
    H -->|Structured log events| SEQ

    HP -->|DB ping (readiness only)| DB
```

### Key trust boundaries

- The only trusted entry into the system is a request carrying the correct static bearer token over TLS. Everything else is unauthenticated.
- The Docker network boundary separates Locksmith from its backing services (PostgreSQL, Redis, Seq). No backing service port should be exposed to the host except for local development.
- The raw API key crosses the trust boundary on EP-1 (creation) and EP-4 (retrieval). It is stored encrypted in PostgreSQL; the data encryption key (A-7) is the highest-value infrastructure secret after the management bearer token.
