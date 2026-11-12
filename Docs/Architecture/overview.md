# Locksmith — Architecture Overview

Locksmith is an ASP.NET Core 10 REST API that manages the full lifecycle of API keys: issuance, listing, secret validation and retrieval, status transitions (activate/deactivate/revoke), rotation, deletion, and per-key action (permission) management. It exists to solve the problem of centralized, auditable API key management — so internal services don't have to implement their own inconsistent security mechanisms.

For the exact request/response shape of every endpoint, see [api-surface.md](api-surface.md).

## How Locksmith is organized

Locksmith follows Clean Architecture organized into five layers. Dependencies flow inward only — each layer can depend on anything inside it, but not outside it.

```
Domain ← Application ← Infrastructure ← Api
                                       ← Agent
```

### Domain

Located in `Src/Domain/`. Contains business entities and domain logic with **zero external dependencies**.

- **Entities:** `ApiKey`, `ApiKeyStatus`, `ApiKeyAction`, `IdempotencyKey` — all four implement `IAppendOnlyTable` (see [The data model](#the-data-model))
- **Enums:** `ApiKeyStatusEnum` (`Inactive`, `Active`, `Revoked`, `Expired`), `ApiKeyActionEnum` (`Read`, `Write`, `Delete`, `Execute`)
- **Exceptions:** `NotFoundException`, `ValidationException`, `ConflictException`, `DecryptionFailedException`, `AppendOnlyViolationException` — mapped to HTTP status codes by the Api layer (except `AppendOnlyViolationException`, which indicates an internal invariant violation and falls through to `500`)
- **Constants:** `WellKnown` — config section names, connection string keys, health check tags, auth scheme names, request header names, `HttpContext.Items` keys, rate-limit response header names, and caller identities, all in one place to avoid magic strings

Settings classes do **not** live here — they sit in the project that owns them (see [Configuration and startup validation](#configuration-and-startup-validation)).

This layer is pure .NET. It knows nothing about databases, HTTP, or external services.

### Application

Located in `Src/Application/`. Contains use cases and application services.

- **Key management services:** `CreateApiKeyService`, `ListApiKeysService`, `GetApiKeyByIdService`, `GetApiKeyBySecretService`, `ValidateApiKeySecretService`, `RetrieveSecretService`, `RotateApiKeyService`, `DeleteApiKeyService`
- **Status services** (`Services/Status/`): `GetApiKeyStatusService`, `GetApiKeyStatusHistoryService`, `UpdateApiKeyStatusService`
- **Action services** (`Services/Actions/`): `ListApiKeyActionsService`, `ReplaceApiKeyActionsService`, `GrantApiKeyActionService`, `RevokeApiKeyActionService`, plus the internal `ApiKeyActionParser` (exact, case-insensitive name matching — rejects the numeric and comma-list values `Enum.TryParse` would otherwise accept)
- No FluentValidation validators exist; validation is inline in services (e.g. `ExpiresAt` in `CreateApiKeyService`) or via `[Required]`/`[RegularExpression]` on the request records in Api
- **Interfaces:** `IApiKeyRepository`, `IApiKeyStatusRepository`, `IApiKeyActionRepository`, `IIdempotencyKeyRepository`, `IUnitOfWork`, `ICryptoService`, `IRateLimiter` — contracts for infrastructure concerns
- **Commands/DTOs:** `CreateApiKeyCommand`/`CreateApiKeyResult`, `ApiKeyMetadata` (+ `ApiKeyMetadataMapper`), and per-service result records
- **Settings:** `CryptoSettings` (Argon2id parameters) and `RateLimitSettings` (sliding-window limiter)

### Infrastructure

Located in `Src/Infrastructure/`. Implements the interfaces defined in Application, and manages all external concerns.

- **EF Core context:** `AppDbContext` with entity configurations, including the append-only guard (see [The data model](#the-data-model))
- **Repositories:** `ApiKeyRepository`, `ApiKeyStatusRepository`, `ApiKeyActionRepository`, `IdempotencyKeyRepository` — reads filter out soft-deleted rows where the domain requires "currently active" semantics; history reads intentionally don't
- **Unit of Work:** `UnitOfWork` manages transaction scope across multiple repositories (`ExecuteInTransactionAsync`)
- **Rate limiting:** `RedisSlidingWindowRateLimiter` — the `IRateLimiter` implementation, a Redis sorted-set sliding window evaluated atomically in a Lua script
- **External services:** Redis distributed cache and `IConnectionMultiplexer`
- **Migrations:** Entity Framework schema migrations (`Src/Infrastructure/Migrations/`)

### Api

Located in `Src/Api/`. ASP.NET Core entry point. Hosts HTTP endpoints and orchestrates the request-to-response pipeline.

- **Controllers:** `ApiKeyController`, `ApiKeyStatusController`, `ApiKeyActionController`, versioned under `Src/Api/Controllers/V1/` with an explicit `[ApiVersion(1.0)]` attribute — all inherit from `Src/Api/Controllers/Controller.cs`, which carries the versioned route template (`/api/v{version:apiVersion}/[controller]`) and `[Authorize]`; each controller overrides the route to `api/v{version:apiVersion}/api-key` so all three share the same base path
- **Authentication:** `BearerTokenAuthenticationHandler` — a custom `AuthenticationHandler<T>` that validates the static bearer token with constant-time comparison
- **`ResolveApiKeyFilter`:** an `IAsyncActionFilter` that resolves the `X-Api-Key` header to a key identity for the four read endpoints that need it (see [api-surface.md](api-surface.md#identifying-a-key))
- **`RateLimitFilter` / `CredentialRateLimitFilter`:** action filters that enforce the sliding-window quota — the first partitions on the key `ResolveApiKeyFilter` resolved, the second on a hash of the request-body credential (see [api-surface.md](api-surface.md#rate-limiting))
- **`ResponseCacheControlFilter`:** an `IAlwaysRunResultFilter` that stamps `no-store` on every response by default, or a short private cache on actions marked `[Cacheable]` (see [api-surface.md](api-surface.md#response-caching))
- **Settings:** `ApiSettings` (name, bearer token), `CacheSettings` (`[Cacheable]` max-age), and `DatabaseSettings` (currently unused — not bound anywhere)
- **Global exception handler:** `GlobalExceptionHandler` catches domain exceptions, converts to RFC 9457 ProblemDetails
- **Middleware:** Serilog request logging, authentication/authorization, health checks
- **OpenAPI:** automatic OpenAPI document generation (`/openapi/v1.json`) plus a [Scalar](https://scalar.com/) UI (`/scalar/v1`) in Development environment

### Agent

Located in `Src/Agent/`. Separate Worker Service executable — runs background jobs and scheduled tasks.

- **Worker service:** `Worker` implements `BackgroundService`/`IHostedService`. It currently only logs on startup and performs no periodic work — the key-expiry polling job described in [TODO.md](../TODO.md) isn't implemented yet.
- **Shared infrastructure:** reuses repositories, `AppDbContext`, and Infrastructure services via the same `AddInfrastructure()` extension the Api project calls
- **Independent startup:** wires services independently from Api (`AddAgentServices()`) and has its own `appsettings*.json` files (`Agent` section, connection strings, Serilog)

**Key distinction:** Api and Agent are separate entry points that share domain/infrastructure logic but have different purposes.

---

## How a request flows through Locksmith

When an HTTP request arrives at the Api, it travels through this pipeline (in `UseApiPipeline()`):

1. **Serilog request logging** — captures timestamp, HTTP method, path, query string, request headers; wraps the full pipeline so it can log the final status code even for exception-mapped responses
2. **Global exception handler** — wraps downstream execution; catches domain exceptions and converts them to ProblemDetails
3. **OpenAPI middleware** (Development only) — serves the OpenAPI document at `/openapi/v1.json` and a Scalar UI at `/scalar/v1`
4. **HTTPS redirect** — upgrades HTTP to HTTPS
5. **Authentication middleware** — `BearerTokenAuthenticationHandler` validates `Authorization: Bearer <token>` using constant-time comparison
6. **Authorization middleware** — enforces `[Authorize]` on the base `Controller` class; returns `401` on missing/invalid token
7. **Controller routing** — matches request to a controller action based on route template and HTTP method
8. **Action filters** — `ResolveApiKeyFilter` (on the four `X-Api-Key`-based reads) resolves the caller's key identity onto `HttpContext.Items`, then `RateLimitFilter` checks that key's quota; on credential-identified mutations `CredentialRateLimitFilter` checks the quota for the hashed body credential instead; `ResponseCacheControlFilter` (registered globally) stamps cache headers on the way out
9. **Action execution** — controller calls an application service, which uses repositories (via `IUnitOfWork`) to query/mutate data
10. **Response serialization** — result is serialized to JSON and returned
11. **Health check endpoints** — special-case unversioned routes (`/health`, `/health/ready`) that don't require authentication and sit outside the versioned controller routes

### What happens to domain exceptions

If an application service throws a domain exception, the global exception handler catches it and converts it to a ProblemDetails response:

| Exception | HTTP Status | When |
|---|---|---|
| `NotFoundException` | 404 | Resource doesn't exist (unknown secret, unknown idempotency key, action not granted) |
| `ValidationException` | 422 | Request body failed validation; includes field-level errors |
| `ConflictException` | 409 | Status transition attempted from a terminal state, or an action already actively granted |
| `DecryptionFailedException` | 422 | Stored ciphertext failed to decrypt (thrown by `RetrieveSecretService`) |
| `AppendOnlyViolationException` | 500 | Code attempted to update or delete an append-only entity — an internal bug, not a client error |
| (unhandled) | 500 | Any other exception; detail is redacted outside Development |

The `X-Api-Key`-missing case (`400`) is produced directly by `ResolveApiKeyFilter` as a `BadRequestObjectResult`, not by `GlobalExceptionHandler` — it never throws, so it isn't in this table.

Example validation error response:
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

## Health checks for container orchestration

Locksmith exposes two unversioned health check endpoints that don't require authentication:

### Liveness: `GET /health`

Always returns `Healthy` as long as the process is running. Use this for Kubernetes liveness probes.

```bash
curl http://localhost:5000/health
# { "status": "Healthy", "totalDuration": "00:00:00.001", "entries": {} }
```

### Readiness: `GET /health/ready`

Runs checks tagged `WellKnown.HealthCheckTags.READY` (`"READY"`) — the EF Core `AppDbContext` check and the Redis check. Returns `Healthy` (200) only when all dependencies are reachable, otherwise `Unhealthy` (503). Use this for Kubernetes readiness probes.

```bash
# When ready
curl http://localhost:5000/health/ready
# { "status": "Healthy", "totalDuration": "00:00:00.012",
#   "entries": { "AppDbContext": { "status": "Healthy", ... }, "redis": { "status": "Healthy", ... } } }

# When the database is down
curl http://localhost:5000/health/ready
# { "status": "Unhealthy", "totalDuration": "00:00:05.000",
#   "entries": { "AppDbContext": { "status": "Unhealthy", ... }, "redis": { "status": "Healthy", ... } } }
```

---

## API versioning

Locksmith uses URL-segment versioning. All endpoints live under `/api/v{version}/`:

```
POST   /api/v1/api-key
GET    /api/v1/api-key
GET    /api/v1/api-key/all
POST   /api/v1/api-key/validate
POST   /api/v1/api-key/secret
POST   /api/v1/api-key/rotate
DELETE /api/v1/api-key
GET    /api/v1/api-key/status
GET    /api/v1/api-key/status/history
PATCH  /api/v1/api-key/status
GET    /api/v1/api-key/actions
PUT    /api/v1/api-key/actions
POST   /api/v1/api-key/actions/{actionName}
DELETE /api/v1/api-key/actions/{actionName}
```

Note the route is singular — `api-key`, not `api-keys` or `keys` — and there is no `{id}` path segment; see [Identifying a key](api-surface.md#identifying-a-key) in the API surface doc for how each endpoint resolves the target key instead.

The base controller at `Src/Api/Controllers/Controller.cs` carries `[Authorize]` and the versioned route template; each of the three controllers (`ApiKeyController`, `ApiKeyStatusController`, `ApiKeyActionController`), living under `Src/Api/Controllers/V1/` and decorated with `[ApiVersion(1.0)]`, overrides the route to the shared `api/v{version:apiVersion}/api-key` prefix. Each API version can have its own OpenAPI document via `ApiVersionDocumentTransformer`, browsable through the Scalar UI at `/scalar/v1`.

To add a new version, create a `V2` controller folder/namespace with `[ApiVersion(2.0)]` on each controller, register a new `AddOpenApi("v2", ...)` call in `ServiceExtensions.AddApiServices()`, and add versioned controller actions. Old versions remain in the codebase until explicitly removed.

---

## Configuration and startup validation

All settings use the `IOptions<T>` pattern with validation that runs at startup — not at request time.

### How configuration works

1. Settings live in `Src/<Project>/Settings/` as POCOs with validation attributes:

| Class | Project | Section | Purpose |
|---|---|---|---|
| `ApiSettings` | Api | `Api` | Application name and the management bearer token (both required) |
| `CacheSettings` | Api | `Cache` | `ApiKeyReadSeconds` — max-age for `[Cacheable]` reads (default 60) |
| `CryptoSettings` | Application | `Cryptography` | Argon2id parameters |
| `RateLimitSettings` | Application | `RateLimiting` | `Enabled`, `PermitLimit`, `WindowSeconds`, `FailOpen` |
| `AgentSettings` | Agent | `Agent` | Agent name (required) |
| `DatabaseSettings` | Api | `Database` | Unused — present in `appsettings.json` but never bound |

For example:

```csharp
public sealed class ApiSettings
{
    [Required, StringLength(256, MinimumLength = 1)]
    public required string Name { get; init; }

    [Required, StringLength(256, MinimumLength = 1)]
    public required string BearerToken { get; init; }
}
```

2. In `AddApiServices()` (`Src/Api/Extensions/ServiceExtensions.cs`), they're bound from `appsettings.json`:

```csharp
builder.Services
    .AddOptions<ApiSettings>()
    .BindConfiguration(WellKnown.ConfigSections.API)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

3. Any controller or service that needs the settings requests `IOptions<T>` via constructor injection. `CryptoSettings` is bound and validated the same way, and is *additionally* registered as a plain singleton (`IOptions<CryptoSettings>.Value`), so `CryptoService` can take the class directly.

### Configuration section names

All configuration section name constants are centralized in `Domain.WellKnown.ConfigSections` to avoid magic strings:

```csharp
public static class ConfigSections
{
    public const string API = "API";
    public const string AGENT = "AGENT";
    public const string CRYPTOGRAPHY = "Cryptography";
    public const string CACHE = "Cache";
    public const string RATE_LIMITING = "RateLimiting";
}
```

Configuration binding is case-insensitive, so `appsettings.json` spells these `"Api"` and `"Agent"` — matching the constant's *value*, not necessarily its casing.

`CryptoSettings` (Argon2id tuning), bound to the `Cryptography` section:

```csharp
public sealed class CryptoSettings
{
    [Range(1, 10)]
    public int DegreeOfParallelism { get; init; } = 1;

    [Range(65536, 1048576)]
    public int MemorySize { get; init; } = 65536;

    [Range(5, 100)]
    public int Iterations { get; init; } = 8;
}
```

### Invalid configuration fails fast

If any setting is missing or invalid, the application fails to start with a clear error message. This is intentional — configuration errors are not request-time surprises; they're startup failures.

---

## Logging and observability

Both Api and Agent use **Serilog** for structured logging, configured from `appsettings.json`. Sinks today are Console (compact JSON) and rolling daily files under `logs/` (all events, plus a separate warnings-and-above file). `appsettings.Development.json` swaps the first sink (Console) to a human-readable template and lowers the minimum level to `Debug`; because configuration arrays merge by index, the File sinks still apply in Development. Classes log through `ILogger<T>`, with Serilog as the provider. No log aggregator (e.g. Seq) is wired up yet.

### What gets logged

- **Request/response:** timestamp, HTTP method, path, query string, status code, elapsed time (via `UseSerilogRequestLogging()`)
- **Application context:** machine name, environment name, enriched properties
- **Request context (Api only):** request host, request scheme, user agent

### Logging setup

`AddSerilogLogging()` extension method configures Serilog from `appsettings.json`. Both Api and Agent call this during startup.

The enrichers (`FromLogContext`, machine name, environment name) are added in code by `AddSerilogLogging()`, not in JSON. The Development console sink, from `Src/Api/appsettings.Development.json`:
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": { "Microsoft": "Information", "Microsoft.Hosting.Lifetime": "Information", "System": "Warning" }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

---

## Service registration pattern

Extension methods on `IHostApplicationBuilder` keep `Program.cs` clean:

### `AddApiServices()` — Api-specific wiring (`Src/Api/Extensions/ServiceExtensions.cs`)

Registers:
- Controllers with the global `ResponseCacheControlFilter`, plus the `ResolveApiKeyFilter`, `RateLimitFilter`, and `CredentialRateLimitFilter` action filters
- Bearer token authentication scheme and API versioning
- Version-aware OpenAPI document generation
- `ApiSettings`, `CryptoSettings`, `CacheSettings`, and `RateLimitSettings` options binding
- `ICryptoService` and every key/status/action application service
- Global exception handler and ProblemDetails formatting (including a custom `InvalidModelStateResponseFactory` so model-binding failures return the same 422 shape as domain validation errors)
- Health checks (EF Core + Redis, tagged `READY`)

### `AddAgentServices()` — Agent-specific wiring (`Src/Agent/Extensions/ServiceExtensions.cs`)

Registers:
- `Worker` as a hosted service
- `AgentSettings` options binding

### Both call `AddInfrastructure()`

Registered in a shared extension method in the Infrastructure layer (`Src/Infrastructure/Extensions/ServiceExtensions.cs`):
- EF Core `AppDbContext` (Npgsql)
- `IUnitOfWork` and all four repositories
- Redis distributed cache + `IConnectionMultiplexer`
- `IRateLimiter` → `RedisSlidingWindowRateLimiter` (singleton)

### Middleware pipeline via `UseApiPipeline()`

Extension method on `WebApplication` (`Src/Api/Extensions/PipelineExtensions.cs`) applies the request pipeline in order — see [How a request flows through Locksmith](#how-a-request-flows-through-locksmith) above.

---

## The data model

Locksmith stores four entity types with specific constraints to preserve audit history and enable efficient lookups. See [database.md](database.md) for the full schema reference.

### `ApiKey` — the core entity

```
id: GUID (primary key)
secret: string (AES-256-GCM ciphertext, base64)
secretHash: string (unique index)
createdAt: DateTime
createdBy: string
expiresAt: DateTime
```

There is no `ownerId` field — the entity has no concept of a key owner today. There is also no `DeletedAt` — unlike the other three entities, `ApiKey` can never be soft-deleted, only ever inserted. "Deleting" a key ([`DELETE .../api-key`](api-surface.md#delete-a-key)) soft-deletes its statuses, actions, and idempotency records instead, leaving the `ApiKey` row itself as a permanent, unreachable anchor.

### `ApiKeyStatus` — append-only state history

```
id: GUID (primary key)
apiKeyId: GUID (foreign key)
status: enum (Inactive|Active|Revoked|Expired)
createdAt: DateTime
createdBy: string
deletedAt: DateTime? (soft delete)
```

### `ApiKeyAction` — permissions junction table

```
id: GUID (primary key)
apiKeyId: GUID (foreign key)
action: enum (Read|Write|Delete|Execute)
createdAt: DateTime
createdBy: string
deletedAt: DateTime? (soft delete)
```

A partial unique index on `(ApiKeyId, Action)` (filtered to non-deleted rows) guarantees at most one active grant per action per key — the authoritative guard against concurrent duplicate grants.

### `IdempotencyKey` — secret retrieval and mutation targeting

```
id: GUID (primary key)
apiKeyId: GUID (foreign key)
idempotencyKeyHash: string (unique index)
salt: string (base64, random — the Argon2id salt for the DEK)
createdAt: DateTime
createdBy: string
deletedAt: DateTime? (soft delete)
```

Every mutating endpoint except create and validate resolves its target key through this table, by hashing the caller-supplied `idempotencyKey` and looking up the matching row (see [Identifying a key](api-surface.md#identifying-a-key)). It also stores the salt needed to re-derive the per-key DEK for `POST .../api-key/secret`.

### Append-only enforcement

All four entities implement `Domain.IAppendOnlyTable`. Enforcement is **application-layer**, in `AppDbContext.SaveChanges`/`SaveChangesAsync`: before delegating to EF Core, it walks the change tracker and throws `AppendOnlyViolationException` if any `IAppendOnlyTable` entity is `Deleted`, or if any is `Modified` with a change to any property other than `DeletedAt`. This is one guard shared by all four tables — not a per-table `HasNoKey()` configuration — and it's why soft-deleting via `DeletedAt` is always allowed but every other kind of update or a hard delete is not, even for `ApiKey`, which has no `DeletedAt` at all and so can never be touched again after insert.

---

## Cryptography and key management

Locksmith implements multiple layers of cryptographic security in `CryptoService`. [ADR-002](../Decisions/ADR-002-api-key-creation.md) records the decision to generate keys server-side; the encryption design below was built under it.

### Key generation

```csharp
byte[] idempotencyKeyBytes = RandomNumberGenerator.GetBytes(96); // base64url-encoded
byte[] secretBytes = RandomNumberGenerator.GetBytes(32);          // "lk_" + base64url-encoded
```

Both the idempotency key and the API key secret are cryptographically random, base64url-encoded (no padding). The secret is prefixed `lk_`. Both are returned to the caller exactly once, at creation (or rotation), and are **never** logged.

### Hashing for lookup

The same routine hashes both the API key secret (for the `SecretHash` unique index) and the idempotency key (for the `IdempotencyKeyHash` unique index) — SHA-256, base64-encoded, no salt:

```csharp
byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
string hashForLookup = Convert.ToBase64String(hash);
```

This gives O(1) equality lookups on both unique indexes.

### Encryption at rest

Raw API key secrets are encrypted with **AES-256-GCM** using a per-key data encryption key (DEK) derived from the *plaintext idempotency key itself* — not a separate master secret:

1. **DEK derivation:** at creation, a random 32-byte salt is generated and stored on the `IdempotencyKey` row. Argon2id derives the DEK from the plaintext idempotency key (as the password) and that salt:
   ```csharp
   using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(idempotencyKey))
   {
       Salt = salt,
       DegreeOfParallelism = settings.DegreeOfParallelism,
       MemorySize = settings.MemorySize,
       Iterations = settings.Iterations,
   };
   byte[] dek = argon2.GetBytes(32);
   ```
2. **Encryption:** AES-256-GCM encrypts the raw secret with a random 12-byte nonce and a 16-byte tag; `nonce || ciphertext || tag` is base64-encoded and stored as `ApiKey.Secret`.
3. **Retrieval:** `POST .../api-key/secret` re-derives the DEK from the caller-supplied idempotency key and the stored salt, then decrypts. Presenting the wrong idempotency key derives the wrong DEK, which fails AES-GCM's authentication tag check and surfaces as `DecryptionFailedException` → `422`.

This means the idempotency key is not just a lookup token — it is also key material. Losing it means the secret is unrecoverable even though the ciphertext is still in the database.

### Constant-time comparison

The bearer token check in `BearerTokenAuthenticationHandler` uses `CryptographicOperations.FixedTimeEquals()`:

```csharp
bool isValid = CryptographicOperations.FixedTimeEquals(tokenBytes, configuredTokenBytes);
```

Secret and idempotency-key lookups, by contrast, go through a SQL equality match on the hashed value (`SecretHash`/`IdempotencyKeyHash`) rather than an in-process byte comparison, so this specific timing-safe helper applies to the bearer token check only.

---

## Design principles

Five core principles guide all architectural decisions in Locksmith:

1. **Never store secrets in plaintext** — secrets are looked up by hash and persisted only as AES-256-GCM ciphertext; only hashes, salts, and ciphertext live in the database.

2. **Append-only audit log** — state and permission changes are new database rows, never updates; the full history of every key is always available and can never be overwritten.

3. **Least privilege by default** — keys are created in an `Inactive` state with only the permissions explicitly requested at creation (or granted afterward); nothing is implicitly active.

4. **Constant-time comparison for the credential a client presents directly** — the bearer token check is timing-safe; lookups by hash go through the database instead.

5. **Bounded blast radius** — a compromised API key is limited to exactly the actions assigned to it; a leaked management token can be rotated without touching key data.

---

## Technology decisions

Specific architectural choices and their reasoning are documented in Architecture Decision Records (ADRs). Don't duplicate that reasoning here — link instead:

- **[ADR-001: API Key Lifecycle](../Decisions/ADR-001-api-key-lifecycle.md)** — state machine design and append-only history
- **[ADR-002: API Key Creation](../Decisions/ADR-002-api-key-creation.md)** — system-generated keys; implementation notes cover the encryption at rest
- **[ADR-003: Action Management](../Decisions/ADR-003-api-key-action-management.md)** — permissions model and least-privilege design
- **[ADR-004: Authentication](../Decisions/ADR-004-authentication.md)** — why static bearer tokens instead of JWT/OIDC

---

## Technology stack

| Concern | Technology |
|---|---|
| Framework | .NET 10, ASP.NET Core |
| ORM | Entity Framework Core (Npgsql) |
| Database | PostgreSQL |
| Cache / rate limiting | Redis (`StackExchange.Redis`) |
| Cryptography | .NET `System.Security.Cryptography` (SHA-256, AES-GCM), `Konscious.Security.Cryptography.Argon2` |
| API docs | `Microsoft.AspNetCore.OpenApi` + Scalar |
| Logging | Serilog (structured logging) |
| Testing | xUnit, Moq, FluentAssertions, `WebApplicationFactory`, Testcontainers (PostgreSQL, Redis) |
| Code style | StyleCop.Analyzers with `.editorconfig` |

---

## Security boundaries

What data never leaves the server? What is the attack surface?

### Secrets never leave plaintext at rest

- Raw API key secrets are encrypted at rest (AES-256-GCM) and returned only on creation and rotation responses.
- The management bearer token is stored in environment variables only, never in code or logs.
- The bearer token comparison uses constant-time equality; secret/idempotency-key lookups use hashed unique indexes.

### Audit trail is append-only

- `ApiKeyStatus`, `ApiKeyAction`, and `IdempotencyKey` rows can only be inserted or have `DeletedAt` set — enforced in `AppDbContext.SaveChanges` for all four entities, `ApiKey` included (which has no `DeletedAt` at all, so it can never change after insert).
- Every state transition and permission grant/revoke is timestamped; the full history is always queryable via `GET .../status/history`.

### Attack surface is narrowed

- Every versioned endpoint requires a valid bearer token; the four `X-Api-Key`-resolved reads require that header too.
- Every response carries an explicit cache directive — no response is cacheable by accident.
- Every endpoint that targets an existing key is rate limited via a Redis-backed sliding window: the four `X-Api-Key`-resolved reads per resolved key (`RateLimitFilter`), and every credential-identified mutation per hashed body credential (`CredentialRateLimitFilter`). Only `create` and `list all` are unthrottled.

For threat-specific analysis, see [Security/threat-model.md](../Security/threat-model.md).
