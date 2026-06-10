# Locksmith TODO

Items are grouped by functional area within each priority tier. Check off items as they are completed.

---

## Must Have

Core functionality and security-critical items required before Locksmith can be used by any caller.

### Authentication

- [ ] Static bearer token middleware — reject any management request missing or mismatching `Authorization: Bearer <token>` with a `401` before any handler runs
- [ ] Use `CryptographicOperations.FixedTimeEquals` for token comparison (constant-time, no timing attacks)
- [ ] Inject token via environment variable / secrets manager; add `[Required]` validation to `ApiSettings`
- [ ] Exempt `/health` and `/health/ready` from authentication (route-based, not wildcard bypass)

### Domain Entities

- [x] `ApiKey`
- [x] `ApiKeyStatus`
- [x] `ApiKeyAction`

- [x] `ApiKeyStatusEnum` — `Inactive`, `Active`, `Revoked`, `Expired`
- [x] `ApiKeyActionEnum` — `Read`, `Write`, `Delete`, `Execute`
- [ ] State transition guard — only allow valid transitions (`Inactive → Active`, `Active → Inactive`, `Active/Inactive → Revoked`); throw `ConflictException` on invalid transitions

### Cryptography

- [ ] Key generation — cryptographically random secret (e.g., `RandomNumberGenerator.GetBytes`)
- [ ] Key hashing — hash with per-key salt using PBKDF2 or Argon2; store hash + salt; never store raw
- [ ] Key encryption at rest — encrypt raw key with AES-GCM using a data encryption key (DEK); store ciphertext + nonce in `EncryptedKey`
- [ ] DEK management — load DEK from environment variable / secrets manager; fail fast on startup if absent
- [ ] Constant-time key comparison — use `FixedTimeEquals` when validating a presented key against the stored hash

### Data Access

- [x] EF Core `AppDbContext` in `Infrastructure` — entity configurations for `ApiKey`, `ApiKeyStatus`, `ApiKeyAction`
- [X] Append-only constraint on `ApiKeyStatus` — no `Update` or `Delete` allowed through the context
- [X] Unique index on `ApiKey.IdempotencyKey`
- [ ] Initial migration — creates `api_keys`, `api_key_statuses`, `api_key_actions` tables
- [ ] Repository interfaces in `Application` — `IApiKeyRepository`, `IApiKeyStatusRepository`, `IApiKeyActionRepository`
- [ ] Repository implementations in `Infrastructure`
- [ ] `IUnitOfWork` interface and implementation
- [ ] Register DbContext and repositories in `Infrastructure` service extensions
- [ ] Wire EF Core readiness health check tagged `"ready"`

### Application Layer 

- [ ] `CreateApiKeyCommand` + handler — generate key, hash, encrypt, persist, return raw key once
- [ ] `GetApiKeyQuery` + handler — return metadata (no raw key)
- [ ] `GetApiKeySecretQuery` + handler — decrypt and return raw key
- [ ] `PatchApiKeyStatusCommand` + handler — validate transition, append status row
- [ ] `RotateApiKeyCommand` + handler — generate new secret, re-encrypt, re-hash, append status row atomically
- [ ] `RevokeApiKeyCommand` + handler — append `Revoked` status row; soft-delete semantics
- [ ] `ListApiKeyActionsQuery` + handler
- [ ] `ReplaceApiKeyActionsCommand` + handler — diff current vs. new set; delete removed, insert added
- [ ] `GrantApiKeyActionCommand` + handler — insert single action; throw `ConflictException` if already granted
- [ ] `RevokeApiKeyActionCommand` + handler — delete single action; throw `NotFoundException` if not present
- [ ] FluentValidation validators for all commands

### Idempotency

- [ ] `IdempotencyKey` header extraction middleware or filter — read `Idempotency-Key` from request
- [ ] Store idempotency key + cached response on first execution of `CreateApiKeyCommand`
- [ ] Return cached response (original `201` body) on duplicate key without re-executing

### API Endpoints — Key Management

- [ ] `POST /api/v1/keys` — issue a key (calls `CreateApiKeyCommand`); `201` with raw key; `409` on duplicate idempotency key; `422` on validation failure
- [ ] `GET /api/v1/keys/{keyId}` — metadata only; `404` if not found
- [ ] `GET /api/v1/keys/{keyId}/secret` — decrypted raw key; `404` if not found
- [ ] `PATCH /api/v1/keys/{keyId}` — activate or deactivate; `409` on invalid transition; `422` on bad status value
- [ ] `POST /api/v1/keys/{keyId}/rotate` — new secret, old invalid immediately; `409` if `Revoked`/`Expired`
- [ ] `DELETE /api/v1/keys/{keyId}` — revoke permanently; `204` on success; `404` if not found

### API Endpoints — Action Management

- [ ] `GET /api/v1/keys/{keyId}/actions` — list granted actions; `404` if key not found
- [ ] `PUT /api/v1/keys/{keyId}/actions` — replace full action set; `404`/`422` as appropriate
- [ ] `POST /api/v1/keys/{keyId}/actions/{action}` — grant single action; `409` if already granted; `422` on invalid action name
- [ ] `DELETE /api/v1/keys/{keyId}/actions/{action}` — revoke single action; `404` if key or action not found

### Key Expiry (Agent)

- [ ] Background job in `Agent` — periodically query keys where `ExpiresAt <= now` and `Status != Expired/Revoked`; append `Expired` status row for each
- [ ] Configurable polling interval via `AgentSettings`

### Rate Limiting

- [ ] Redis-backed sliding-window rate limiter on all management endpoints
- [ ] Configurable limit and window size via `ApiSettings`
- [ ] Return `429 Too Many Requests` with `Retry-After` header when limit is exceeded

### Tests

- [ ] Unit: `GlobalExceptionHandler` — verify correct status codes and ProblemDetails shape for all domain exceptions
- [ ] Unit: domain state machine — verify all valid and invalid transitions
- [ ] Unit: cryptography helpers — round-trip hash/verify, encrypt/decrypt
- [ ] Unit: FluentValidation validators for all commands
- [ ] Unit: idempotency filter — duplicate key returns cached response
- [ ] Integration: all key lifecycle endpoints (happy path + error cases)
- [ ] Integration: all action management endpoints (happy path + error cases)
- [ ] Integration: authentication middleware — missing token `401`, valid token passes
- [ ] Integration: rate limiting — `429` after limit exceeded
- [ ] Application: full happy-path flow (issue → grant actions → activate → rotate → revoke)
- [ ] Application: key expiry job transitions `Inactive`/`Active` keys to `Expired`

---

## Should Have

Important for production readiness but not blocking initial functionality.

### Observability

- [ ] OpenTelemetry SDK — add to `Api` and `Agent`
- [ ] Traces — instrument all HTTP handlers and database operations
- [ ] Metrics — request count, request duration, key creation/rotation/revocation counters, active key count gauge
- [ ] Export to OTLP (console or Seq in development; configurable endpoint in production)

### Security Hardening

- [ ] Serilog destructuring policy — redact the `Authorization` header value from all request-scoped log events; confirm raw keys are never written to any sink
- [ ] Per-endpoint request body size limit — appropriate max size for key management payloads (a few KB)
- [ ] CORS — configure allowed origins via `ApiSettings`; deny wildcard in production
- [ ] `ApiKeyStatus` table — enforce append-only at the database layer (e.g., revoke `UPDATE`/`DELETE` privileges for the app user on this table)

### API Polish

- [ ] OpenAPI XML comments on all request/response types
- [ ] Example request/response bodies in OpenAPI via `WithOpenApi()` or schema filters
- [ ] `ETag` / `Last-Modified` on `GET /api/v1/keys/{keyId}` for conditional reads
- [ ] Cursor-based list endpoint — `GET /api/v1/keys?ownerId=&cursor=&limit=` for bulk queries

### Operations

- [ ] Docker Compose production profile — no exposed database ports; secrets via `.env` file
- [ ] Database connection retry on startup (Polly or EF Core resilience)
- [ ] Redis connection retry and circuit-breaker
- [ ] Graceful shutdown — drain in-flight requests before stopping

---

## Can Have

Nice-to-have improvements; revisit when the above tiers are complete.

### Authentication Evolution

- [ ] Migrate management authentication from static bearer token to per-client API keys (the same model Locksmith issues) when a second independent caller is onboarded
- [ ] Per-caller audit log — `CreatedBy` / `UpdatedBy` fields populated with caller identity derived from the authenticated API key

### DEK Rotation

- [ ] DEK rotation procedure — re-encrypt all stored raw keys in a migration job when the DEK is rotated

### Developer Experience

- [ ] `dotnet watch` launch profile for local development with hot reload
- [ ] Seed script for local development — creates a test API key and prints the raw value

### Future API Versions

- [ ] `v2` of key management endpoints (breaking changes, e.g., richer metadata or new fields) using the existing versioning infrastructure