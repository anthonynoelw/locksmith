# Locksmith TODO

Items are grouped by functional area within each priority tier. Check off items as they are completed.

---

## Must Have

Core functionality and security-critical items required before Locksmith can be used by any caller.

### Authentication

- [ ] Static bearer token middleware — extract from `Authorization: Bearer <token>` header, validate against `ApiSettings.BearerToken` using `FixedTimeEquals` (constant-time); return `401` on missing/invalid; runs before auth handler
- [ ] `[Authorize]` attribute on all management endpoints to enforce middleware result
- [x] Inject token via environment variable / config; validate with `[Required]` on `ApiSettings` at startup
- [x] Exempt `/health` and `/health/ready` from authentication (not under `/api/v{version}/`)

### Domain Entities

- [x] `ApiKey`
- [x] `ApiKeyStatus`
- [x] `ApiKeyAction`

- [x] `ApiKeyStatusEnum` — `Inactive`, `Active`, `Revoked`, `Expired`
- [x] `ApiKeyActionEnum` — `Read`, `Write`, `Delete`, `Execute`
- [ ] State transition guard — only allow valid transitions (`Inactive → Active`, `Active → Inactive`, `Active/Inactive → Revoked`); throw `ConflictException` on invalid transitions

### Cryptography

- [x] Key generation — cryptographically random secret for both idempotency key and API key (e.g., `RandomNumberGenerator.GetBytes`)
- [x] Idempotency key hashing — SHA-256 hash of plaintext idempotency key (no salt); salt is used for Argon2id DEK derivation only
- [x] API key encryption at rest — encrypt raw API key with AES-GCM using a derived encryption key (DEK); store ciphertext + nonce + tag
- [x] DEK management — derive DEK per-key using Argon2id with salt; configurable via `CryptoSettings`
- [ ] Key retrieval via secret hash — hash presented API key secret, compare to stored hash using `FixedTimeEquals`, decrypt and return raw API key if match succeeds

### Data Access

- [x] EF Core `AppDbContext` in `Infrastructure` — entity configurations for `ApiKey`, `ApiKeyStatus`, `ApiKeyAction`, `IdempotencyKey`
- [x] Append-only constraint on `ApiKeyStatus` and `IdempotencyKey` — no `Update` or `Delete` allowed through the context; enforced via `HasNoKey()` on entity builder
- [x] Unique index on `ApiKey.SecretHash` (hashed for deduplication)
- [x] Unique index on `IdempotencyKey.IdempotencyKeyHash` (hashed for deduplication)
- [x] Soft-delete support — `DeletedAt` column on `ApiKeyStatus`, `ApiKeyAction`, and `IdempotencyKey` for logical deletion
- [x] Migrations — creates `api_keys`, `api_key_statuses`, `api_key_actions`, `idempotency_keys` tables with proper constraints
- [x] Repository interfaces in `Application` — `IApiKeyRepository`, `IApiKeyStatusRepository`, `IApiKeyActionRepository`, `IIdempotencyKeyRepository`
- [x] Repository implementations in `Infrastructure` — query filters exclude soft-deleted rows
- [x] `IUnitOfWork` interface and implementation
- [x] Register DbContext and repositories in `Infrastructure` service extensions
- [x] EF Core readiness health check tagged `"ready"`
- [x] Redis distributed cache connection configured and validated in readiness health check

### Application Layer

- [x] `CreateApiKey` service — generate idempotency key and API key secret, hash secret for lookup, encrypt secret, persist ApiKey with SecretHash and IdempotencyKey to separate table, return raw key once; validates expiry date
- [ ] `GetApiKey` service — return metadata (no raw key)
- [ ] `GetApiKeySecret` service — hash/compare idempotency key, decrypt and return raw key
- [ ] `PatchApiKeyStatus` service — validate state transition, append status row
- [ ] `RotateApiKey` service — generate new secret, re-encrypt, re-hash, append status row atomically
- [ ] `RevokeApiKey` service — append `Revoked` status row; soft-delete semantics
- [ ] `ListApiKeyActions` service — return active actions (soft-delete filter)
- [ ] `ReplaceApiKeyActions` service — diff current vs. new set; soft-delete removed, insert added
- [ ] `GrantApiKeyAction` service — insert single action; throw `ConflictException` if already granted (including soft-deleted)
- [ ] `RevokeApiKeyAction` service — soft-delete single action; throw `NotFoundException` if not present
- [ ] FluentValidation validators for all service operations

### Idempotency

- [ ] `Idempotency-Key` header extraction — read from request on idempotent operations (POST)
- [ ] Deduplication cache — store response (status + body) keyed by `Idempotency-Key` hash; check before handler, return cached response if present; store result after successful execution
- [ ] Cache expiry — TTL matched to key expiry window (30 days default); cleared on cache eviction or explicit purge
- [ ] Conflict detection — if same `Idempotency-Key` used with different request body, return `409 Conflict` with error explaining mismatch

### API Endpoints — Key Management

Note: Routes use `/api/v{version}/api-keys` (plural) not `/api/v1/keys`

- [x] `POST /api/v{version}/api-keys` — issue a key (calls `CreateApiKeyCommand`); `201` with raw key and idempotency key; `422` on validation failure; requires `[Authorize]`
- [ ] Idempotency — check `Idempotency-Key` header on POST; return `409` on duplicate hash (cache response); omit header for subsequent retrieval
- [ ] `GET /api/v{version}/api-keys/{keyId}` — metadata only (no raw secret); `404` if not found
- [ ] `GET /api/v{version}/api-keys/{keyId}/secret` — decrypt and return raw key via idempotency-key lookup; `404` if key or idempotency key not found
- [ ] `PATCH /api/v{version}/api-keys/{keyId}` — activate or deactivate; `409` on invalid transition; `422` on bad status value; appends `ApiKeyStatus` row
- [ ] `POST /api/v{version}/api-keys/{keyId}/rotate` — new secret, old invalid immediately; `409` if `Revoked`/`Expired`; returns new plaintext secret once
- [ ] `DELETE /api/v{version}/api-keys/{keyId}` — append `Revoked` status; `204` on success; `404` if not found

### API Endpoints — Action Management

- [ ] `GET /api/v{version}/api-keys/{keyId}/actions` — list granted actions (soft-delete filter); `404` if key not found
- [ ] `PUT /api/v{version}/api-keys/{keyId}/actions` — replace full action set; diff and soft-delete/insert; `404`/`422` as appropriate
- [ ] `POST /api/v{version}/api-keys/{keyId}/actions/{action}` — grant single action; `409` if already granted; `422` on invalid action name
- [ ] `DELETE /api/v{version}/api-keys/{keyId}/actions/{action}` — soft-delete single action; `404` if key or action not found

### Key Expiry (Agent)

- [ ] Background job in `Agent` — periodically query keys where `ExpiresAt <= now` and `Status != Expired/Revoked`; append `Expired` status row for each
- [ ] Configurable polling interval via `AgentSettings`

### Rate Limiting

- [ ] Redis connection validated in readiness health check (tag: `"ready"`)
- [ ] Sliding-window rate limiter middleware — per-caller key (from `ApiSettings.BearerToken` or per-endpoint policy)
- [ ] Configurable limit and window duration via `ApiSettings` (e.g., 100 requests per 60 seconds)
- [ ] Return `429 Too Many Requests` with `Retry-After` header (seconds until window resets) when limit exceeded
- [ ] `X-RateLimit-*` response headers for current limit, remaining, and reset time

### Tests

- [ ] Unit: `GlobalExceptionHandler` — verify correct status codes and ProblemDetails shape for all domain exceptions
- [ ] Unit: domain state machine — verify all valid and invalid transitions
- [x] Unit: `CryptoService` — round-trip Encrypt/Decrypt, hash consistency, key derivation with Argon2id
- [ ] Unit: `CreateApiKeyService` — validation (ExpiresAt), key generation, salt derivation, encryption
- [ ] Unit: FluentValidation validators for all commands
- [ ] Unit: idempotency hash matching — verify SHA-256 lookup hash compares correctly
- [ ] Integration: `POST /api/v{version}/api-keys` — `201` with raw key, idempotency key, cache headers; `422` on invalid ExpiresAt; idempotency deduplication on retry
- [ ] Integration: `GET /api/v{version}/api-keys/{keyId}` — metadata retrieval; `404` if not found
- [ ] Integration: `GET /api/v{version}/api-keys/{keyId}/secret` — idempotency-key lookup and decryption; `404` if key or idempotency key not found
- [ ] Integration: all action management endpoints (happy path + error cases)
- [ ] Integration: authentication middleware — missing token returns `401`, valid token allows request, invalid token returns `401`
- [ ] Integration: rate limiting — `429` after sliding-window limit exceeded
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