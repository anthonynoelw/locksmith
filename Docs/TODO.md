# Locksmith TODO

Items are grouped by functional area within each priority tier. Check off items as they are completed.

---

## Must Have

Core functionality and security-critical items required before Locksmith can be used by any caller.

### Authentication

- [x] Static bearer token middleware — extract from `Authorization: Bearer <token>` header, validate against `ApiSettings.BearerToken` using `FixedTimeEquals` (constant-time); return `401` on missing/invalid; runs before auth handler
- [x] `[Authorize]` attribute on all management endpoints to enforce middleware result — applied to every action in `ApiKeyController`
- [x] Inject token via environment variable / config; validate with `[Required]` on `ApiSettings` at startup
- [x] Exempt `/health` and `/health/ready` from authentication (not under `/api/v{version}/`)

### Domain Entities

- [x] `ApiKey`
- [x] `ApiKeyStatus`
- [x] `ApiKeyAction`

- [x] `ApiKeyStatusEnum` — `Inactive`, `Active`, `Revoked`, `Expired`
- [x] `ApiKeyActionEnum` — `Read`, `Write`, `Delete`, `Execute`
- [x] State transition guard — implemented as a terminal-state guard in `ApiKeyStatusRepository.SoftDeleteAsync`: throws `ConflictException` when the current status is `Revoked` or `Expired` (blocks further changes rather than validating the full `Inactive → Active` / `Active → Inactive` / `Active|Inactive → Revoked` transition matrix)

### Cryptography

- [x] Key generation — cryptographically random secret for both idempotency key and API key (e.g., `RandomNumberGenerator.GetBytes`)
- [x] Idempotency key hashing — SHA-256 hash of plaintext idempotency key (no salt); salt is used for Argon2id DEK derivation only
- [x] API key encryption at rest — encrypt raw API key with AES-GCM using a derived encryption key (DEK); store ciphertext + nonce + tag
- [x] DEK management — derive DEK per-key using Argon2id with salt; configurable via `CryptoSettings`
- [x] Key retrieval via secret hash — `ValidateApiKeySecretService` hashes the presented secret and looks it up via the unique `SecretHash` index to return validity/status; raw-key decryption is served separately by `RetrieveSecretService` keyed on the idempotency key (not the secret hash as originally scoped)

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
- [x] `GetApiKey` service — implemented as `GetApiKeyByIdService` (single) and `ListApiKeysService` (paginated); both return metadata only, no raw key
- [x] `GetApiKeySecret` service — implemented as `RetrieveSecretService` (hash/compare idempotency key, decrypt and return raw key) plus `ValidateApiKeySecretService` (hash/compare secret, return validity + status)
- [x] `PatchApiKeyStatus` service — implemented as `UpdateApiKeyStatusService`; resolves the API key via idempotency-key hash lookup (not keyId), soft-deletes the current status (guarding `Revoked`/`Expired`), then appends the new status row
- [x] `GetApiKeyStatus` service — implemented as `GetApiKeyStatusService` (current status by keyId) and `GetApiKeyStatusHistoryService` (full history by keyId); not in original scope
- [x] `RotateApiKey` service — implemented as `RotateApiKeyService`: atomically deletes the current key and issues a new one carrying the same active actions (not a re-encrypt-in-place; a delete + create in one transaction)
- [X] `RevokeApiKey` service — append `Revoked` status row; soft-delete semantics - implemented as `UpdateApiKeyStatusService`;
- [x] `ListApiKeyActions` service — return active actions (soft-delete filter)
- [x] `ReplaceApiKeyActions` service — diff current vs. new set; soft-delete removed, insert added
- [x] `GrantApiKeyAction` service — insert single action; throws `ConflictException` only if there is a currently *active* grant — a previously revoked (soft-deleted) action can be re-granted, matching how status tracks current state as the latest non-deleted row
- [x] `RevokeApiKeyAction` service — soft-delete single action; throw `NotFoundException` if not present

### Idempotency

- [x]! Deprecated(The Idempotency Key will be received via the request body): `Idempotency-Key` header extraction — read from request on idempotent operations (POST) 
- [ ] Deduplication cache — store response (status + body) keyed by `Idempotency-Key` hash; check before handler, return cached response if present; store result after successful execution
- [ ] Cache expiry — TTL matched to key expiry window (30 days default); cleared on cache eviction or explicit purge
- [ ] Conflict detection — if same `Idempotency-Key` used with different request body, return `409 Conflict` with error explaining mismatch

### API Endpoints — Key Management

Note: Routes use `/api/v{version}/api-key` (**singular** — not `/api1/keys` or the originally scoped `/api-keys` plural). There is no `{keyId}` path segment anywhere; endpoints resolve the target key via either the `X-Api-Key` header (reads) or an `idempotencyKey` in the request body (mutations) — see [api-surface.md](Architecture/api-surface.md#identifying-a-key).

- [x] `POST /api/v{version}/api-key` — issue a key (calls `CreateApiKeyCommand`); `201` with raw key and idempotency key; `422` on validation failure; requires `[Authorize]`
- [x] `GET /api/v{version}/api-key/all` — list with pagination (`limit`/`offset`); metadata only (not in original scope, added alongside single-key retrieval)
- [x] `GET /api/v{version}/api-key` — current key's metadata only (no raw secret), resolved via `X-Api-Key` header; `400` if header missing/duplicated, `404` if unknown — implemented as the `X-Api-Key`-resolved "current key" route rather than the originally scoped `GET .../{keyId}`
- [x] `POST /api/v{version}/api-key/validate` — validate a presented secret by hash, return `apiKeyId`/`isValid`; `404` if secret unknown (not in original scope; response dropped `status` from the field list)
- [x] `POST /api/v{version}/api-key/secret` — decrypt and return raw key via idempotency-key lookup; `404` if idempotency key not found — implemented as `POST .../secret` rather than the originally scoped `GET .../{keyId}/secret`
- [x] `GET /api/v{version}/api-key/status` — current status, resolved via `X-Api-Key` header; `404` if no status exists (not in original scope)
- [x] `GET /api/v{version}/api-key/status/history` — full status history, resolved via `X-Api-Key` header, including soft-deleted entries (not in original scope)
- [x] `PATCH /api/v{version}/api-key/status` — activate/deactivate/revoke by `idempotencyKey`; `409` if current status is `Revoked`/`Expired`; `422` on missing/bad status value; appends `ApiKeyStatus` row — implemented as `PATCH .../status` rather than the originally scoped `PATCH .../{keyId}`
- [x] `POST /api/v{version}/api-key/rotate` — deletes the current key and issues a new one with the same active actions atomically; `404` if idempotency key unknown; returns new plaintext secret once
- [x] `DELETE /api/v{version}/api-key` — soft-deletes active status/action/idempotency-key rows by `idempotencyKey`; `204` on success; `404` if not found — the `api_keys` row itself is never deleted

### API Endpoints — Action Management

- [x] `GET /api/v{version}/api-key/actions` — list granted actions (soft-delete filter), resolved via `X-Api-Key` header; `404` if key not found
- [x] `PUT /api/v{version}/api-key/actions` — replace full action set by `idempotencyKey`; diff and soft-delete/insert; `404`/`422` as appropriate; returns the resulting active set
- [x] `POST /api/v{version}/api-key/actions/{action}` — grant single action by `idempotencyKey`; `201` with the grant (no `Location` header); `409` if already actively granted; `422` on invalid action name; `404` if key not found
- [x] `DELETE /api/v{version}/api-key/actions/{action}` — soft-delete single action by `idempotencyKey`; `204` on success; `404` if key or action not found

### Key Expiry (Agent)

- [ ] Background job in `Agent` — periodically query keys where `ExpiresAt <= now` and `Status != Expired/Revoked`; append `Expired` status row for each
- [ ] Configurable polling interval via `AgentSettings`

### Rate Limiting

- [x] Redis connection validated in readiness health check (tag: `"ready"`) — `AddRedis(...)` in `AddApiServices`
- [x] Distributed sliding-window rate limiter — implemented as the `RateLimitFilter` MVC action filter (`Api/Filters`) backed by a Redis sorted-set Lua script (`Infrastructure/RateLimiting/RedisSlidingWindowRateLimiter`) behind the `IRateLimiter` abstraction. Keyed **per resolved API key ID**, read from `HttpContext.Items` after `ResolveApiKeyFilter` runs — applied to the four `X-Api-Key`-resolved `GET` endpoints (current key, current status, status history, active actions). Runs after authentication, so `401`s are never counted. Fails open on Redis outage (configurable)
- [x] Configurable limit and window duration via `RateLimitSettings` (`RateLimiting` config section; defaults 100 requests / 60 seconds) — dedicated options class mirroring `CryptoSettings` rather than folding into `ApiSettings`
- [x] Return `429 Too Many Requests` with `Retry-After` header (seconds until window resets) as an RFC 9457 ProblemDetails body when limit exceeded
- [x] `X-RateLimit-*` response headers for current limit, remaining, and reset time (Unix seconds) on every rate-limited response
- [x] Extend rate limiting to `idempotencyKey`/secret-identified mutation endpoints (secret retrieval, validate, rotate, delete, replace, grant, revoke, update-status) — implemented as the `CredentialRateLimitFilter` MVC action filter, which partitions on a hash of the bound request body's credential (the same `HashForLookup` hash the corresponding DB lookup uses) instead of a route-resolved identity, so no extra DB round trip is needed just to rate limit. `create` remains unthrottled — it targets no existing key, so there is nothing to partition on

### Tests

Note: HTTP-level tests ended up living in `Tests/Application` (real middleware, real in-memory app via `ApplicationFixture`) rather than `Tests/Integration`, which is still an empty `WebApplicationFactory` scaffold with no tests. Items below labeled "Integration:" are satisfied by `Tests/Application` coverage unless noted otherwise.

- [x] Unit: `GlobalExceptionHandler` — verify correct status codes and ProblemDetails shape for all domain exceptions (`Tests/Unit/Handler/GlobalExceptionHandlerTests.cs`)
- [x] Unit: domain state machine — covered as the terminal-state guard in `ApiKeyStatusRepository.SoftDeleteAsync` (`Tests/Unit/Services/Status/UpdateApiKeyStatusServiceTests.cs`, `Tests/Unit/Services/Status/GetApiKeyStatusServiceTests.cs`, `GetApiKeyStatusHistoryServiceTests.cs`); no full transition-matrix validation yet — still blocked on `RotateApiKey`/`RevokeApiKey`
- [x] Unit: `CryptoService` — round-trip Encrypt/Decrypt, hash consistency, key derivation with Argon2id
- [x] Unit: `CreateApiKeyService` — validation (ExpiresAt), key generation, salt derivation, encryption (`Tests/Unit/Services/CreateApiKeyServiceTests.cs`)
- [x] Unit: idempotency hash matching — verify SHA-256 lookup hash compares correctly (covered via `CryptoServiceTests.HashForLookup_*`)
- [x] Integration: `POST /api/v{version}/api-key` — `201` with raw key, idempotency key covered (`CreateApiKeyEndpointTests`); cache headers covered (`ResponseCachingHeaderTests`); idempotency-deduplication-on-retry not covered (no dedup cache exists)
- [x] Integration: `GET /api/v{version}/api-key` (resolved via `X-Api-Key`) — metadata retrieval; `404` if not found; `400` if header missing (`GetApiKeyByIdEndpointTests`)
- [x] Integration: `POST /api/v{version}/api-key/secret` — idempotency-key lookup and decryption; `404` if idempotency key not found (`RetrieveSecretEndpointTests`)
- [x] Integration: status get/history/update endpoints — happy path, unknown-id `404`, missing/null-status `422`, and revoked-current-status `409` all covered (`ApiKeyStatusEndpointTests`)
- [x] Integration: all action management endpoints (happy path + error cases) — list/grant/revoke/replace happy paths, unknown-key `404`, duplicate-grant `409`, invalid-action-name `422`, revoke-not-granted `404`, re-grant-after-revoke, and missing-token `401` all covered (`ApiKeyActionEndpointTests`)
- [x] Integration: authentication middleware — missing token returns `401`, invalid token returns `401` (`CreateApiKeyAuthorizationTests`, `RetrievalEndpointAuthorizationTests`); valid-token-allows-request is implicitly covered by the happy-path tests on each endpoint
- [x] Integration: rate limiting — `429` after limit exceeded across all four `RateLimitFilter`-covered `GET` endpoints and all eight `CredentialRateLimitFilter`-covered mutation endpoints, with `Retry-After`/`X-RateLimit-*` headers and problem+json body, plus auth-precedes-limiter `401` (`RateLimitEndpointTests`, self-contained factory + `FakeRateLimiter`/`FakeGetApiKeyBySecretService`); real-Redis sliding-window arithmetic (allow/deny/remaining/reset, per-partition isolation) covered against a live Testcontainers Redis (`Tests/Integration/RateLimiting/RedisSlidingWindowRateLimiterTests`); backend-unavailable fail-open/closed and both filters' partitioning/disabled/reject behavior covered by mocks (`Tests/Unit/Filters`)
- [ ] Application: full happy-path flow (issue → grant actions → activate → rotate → revoke) — status update now exists (`UpdateApiKeyStatusService`), still blocked on action/rotate/revoke services
- [ ] Application: key expiry job transitions `Inactive`/`Active` keys to `Expired` — no Agent background job exists yet

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
- [ ] Cursor-based list endpoint — `GET /api/v1/api-key/all?ownerId=&cursor=&limit=` for bulk queries (an offset/limit-based `GET /api/v{version}/api-key/all` already exists — see Must Have; this item is about moving to cursor pagination and adding `ownerId` filtering)

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