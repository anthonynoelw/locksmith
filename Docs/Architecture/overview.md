# Locksmith — Architecture Overview

Locksmith is an ASP.NET Core 10 REST API that manages the full lifecycle of API keys: issuance, state transitions, rotation, and revocation. It exists to solve the problem of centralized, auditable API key management — so internal services don't have to implement their own inconsistent security mechanisms.

## How Locksmith is organized

Locksmith follows Clean Architecture organized into five layers. Dependencies flow inward only — each layer can depend on anything inside it, but not outside it.

```
Domain ← Application ← Infrastructure ← Api
                                       ← Agent
```

### Domain

Located in `Src/Domain/`. Contains business entities and domain logic with **zero external dependencies**.

- **Entities:** `ApiKey`, `ApiKeyStatus`, `ApiKeyAction`, `IdempotencyKey`
- **Exceptions:** `NotFoundException`, `ValidationException`, `ConflictException` — mapped to HTTP status codes by the Api layer
- **Constants:** `WellKnown.ConfigSections`, `WellKnown.HealthCheckTags`
- **Settings classes:** `ApiSettings`, `CryptoSettings`, `AgentSettings` — configuration POCOs with validation attributes

This layer is pure .NET. It knows nothing about databases, HTTP, or external services.

### Application

Located in `Src/Application/`. Contains use cases and application services.

- **Services:** `CreateApiKeyService`, validators, business logic that orchestrates across repositories
- **Interfaces:** `IApiKeyRepository`, `IUnitOfWork`, `ICryptoService` — contracts for infrastructure concerns
- **DTOs and request/response models** — application-level data contracts

### Infrastructure

Located in `Src/Infrastructure/`. Implements the interfaces defined in Application, and manages all external concerns.

- **EF Core context:** `AppDbContext` with entity configurations
- **Repositories:** `ApiKeyRepository`, `ApiKeyStatusRepository`, etc. — query filters exclude soft-deleted rows automatically
- **Unit of Work:** manages transaction scope across multiple repositories
- **External services:** Redis cache client, health check implementations
- **Migrations:** Entity Framework schema migrations

### Api

Located in `Src/Api/`. ASP.NET Core entry point. Hosts HTTP endpoints and orchestrates the request-to-response pipeline.

- **Controllers:** inherit from `Src/Api/Controllers/Controller.cs` which carries the versioned route template (`/api/v{version}/`)
- **Global exception handler:** catches domain exceptions, converts to RFC 9457 ProblemDetails
- **Middleware:** Serilog request logging, authorization, health checks
- **OpenAPI:** Swagger/OpenAPI automatic documentation in Development environment

### Agent

Located in `Src/Agent/`. Separate Worker Service executable — runs background jobs and scheduled tasks.

- **Worker service:** implements `IHostedService` for recurring operations
- **Shared infrastructure:** reuses repositories, DbContext, and services from Infrastructure layer
- **Independent startup:** wires services independently from Api; both read the same configuration

**Key distinction:** Api and Agent are separate entry points that share domain/infrastructure logic but have different purposes.

---

## How a request flows through Locksmith

When an HTTP request arrives at the Api, it travels through this pipeline (in `UseApiPipeline()`):

1. **Serilog request logging** — captures timestamp, HTTP method, path, query string, request headers
2. **Global exception handler** — wraps downstream execution; catches domain exceptions and converts them to ProblemDetails
3. **OpenAPI UI middleware** (Development only) — serves Swagger UI at `/swagger`
4. **HTTPS redirect** — upgrades HTTP to HTTPS in non-Development environments
5. **Authorization middleware** — validates `Authorization: Bearer <token>` header using constant-time comparison; returns `401` on invalid token
6. **Controller routing** — matches request to a controller action based on route template and HTTP method
7. **Action execution** — controller calls application service, which uses repositories to query/mutate data
8. **Response serialization** — result is serialized to JSON and returned
9. **Health check endpoints** — special-case unversioned routes (`/health`, `/health/ready`) that don't require authorization

### What happens to domain exceptions

If an application service throws a domain exception, the global exception handler catches it and converts it to a ProblemDetails response:

| Exception | HTTP Status | When |
|---|---|---|
| `NotFoundException` | 404 | Resource doesn't exist |
| `ValidationException` | 422 | Request body failed validation; includes field-level errors |
| `ConflictException` | 409 | State transition not allowed, or idempotency key already used |
| (unhandled) | 500 | Any other exception; detail is redacted outside Development |

Example validation error response:
```json
{
  "type": "https://tools.ietf.org/html/rfc9457",
  "title": "Validation failed",
  "status": 422,
  "errors": {
    "expiresInDays": ["Must be at least 1."]
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

Runs checks tagged `"ready"` (EF Core database, Redis cache). Returns `Healthy` only when all dependencies are reachable. Use this for Kubernetes readiness probes.

```bash
# When ready
curl http://localhost:5000/health/ready
# { "status": "Healthy", "totalDuration": "00:00:00.012", "entries": {} }

# When database is down
curl http://localhost:5000/health/ready
# {
#   "status": "Unhealthy",
#   "totalDuration": "00:00:05.000",
#   "entries": {
#     "database": { "status": "Unhealthy", "description": "Connection refused" }
#   }
# }
```

---

## API versioning

Locksmith uses URL-segment versioning. All endpoints live under `/api/v{version}/`:

```
POST /api/v1/api-keys
GET /api/v1/api-keys/{keyId}
PATCH /api/v1/api-keys/{keyId}
```

The base controller at `Src/Api/Controllers/Controller.cs` carries the route template; all controllers inherit from it. Each API version can have its own OpenAPI (Swagger) document via `ApiVersionDocumentTransformer`.

To add a new version, create a new controller that inherits from the base and override the route if needed. Old versions remain in the codebase until explicitly removed.

---

## Configuration and startup validation

All settings use the `IOptions<T>` pattern with validation that runs at startup — not at request time.

### How configuration works

1. Settings live in `Src/<Project>/Settings/` as POCOs with validation attributes:

```csharp
public class ApiSettings
{
    [Required]
    public string BearerToken { get; set; }

    [Range(1, 1000)]
    public int CryptoIterations { get; set; }
}
```

2. In `Program.cs`, they're bound from `appsettings.json`:

```csharp
builder.Services
    .AddOptions<ApiSettings>()
    .BindConfiguration(WellKnown.ConfigSections.Api)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

3. Any controller or service that needs the settings requests `IOptions<ApiSettings>` via constructor injection.

### Configuration section names

All configuration section names are constants in `Domain/WellKnown.ConfigSections` to avoid magic strings:

```csharp
public static class ConfigSections
{
    public const string Api = "Api";
    public const string Cryptography = "Cryptography";
    public const string Agent = "Agent";
}
```

### Invalid configuration fails fast

If any setting is missing or invalid, the application fails to start with a clear error message. This is intentional — configuration errors are not request-time surprises; they're startup failures.

---

## Logging and observability

Both Api and Agent use **Serilog** for structured logging, configured from `appsettings.json`.

### What gets logged

- **Request/response:** timestamp, HTTP method, path, query string, status code, elapsed time (via `UseSerilogRequestLogging()`)
- **Application context:** machine name, environment name, enriched properties
- **Request context (Api only):** request host, request scheme, user agent

### Logging setup

`AddSerilogLogging()` extension method configures Serilog from `appsettings.json`. Both Api and Agent call this during startup.

Example `appsettings.Development.json`:
```json
{
  "Serilog": {
    "MinimumLevel": "Debug",
    "WriteTo": [
      {
        "Name": "Console",
        "Args": { "theme": "Ansi" }
      }
    ],
    "Enrich": [
      "FromLogContext",
      "WithMachineName",
      "WithEnvironmentName"
    ]
  }
}
```

---

## Service registration pattern

Two extension methods on `IHostApplicationBuilder` keep `Program.cs` clean:

### `AddApiServices()` — Api-specific wiring

Registers:
- Controllers and routing
- API versioning
- OpenAPI (Swagger) configuration
- Global exception handler
- Health checks (with `"ready"` tags for EF Core and Redis)
- ProblemDetails formatting

### `AddAgentServices()` — Agent-specific wiring

Registers:
- Background `Worker` as `IHostedService`
- Scheduled job configurations

### Both call `AddInfrastructureServices()`

Registered in shared extension method in Infrastructure layer:
- EF Core `DbContext`
- Repositories
- Unit of Work
- Redis cache client
- Crypto service

### Middleware pipeline via `UseApiPipeline()`

Extension method on `WebApplication` applies the request pipeline in order:
- Serilog request logging
- Exception handler
- OpenAPI UI (Development)
- HTTPS redirect
- Authorization middleware
- Controller routing
- Health check endpoints

---

## The data model

Locksmith stores four entity types with specific constraints to preserve audit history and enable efficient lookups.

### `ApiKey` — the core entity

```
id: GUID (primary key)
ownerId: string
createdAt: DateTime
expiresAt: DateTime
secretHash: string (unique index)
```

The key record itself is never updated or deleted. State changes are tracked in a separate `ApiKeyStatus` table.

### `ApiKeyStatus` — append-only state history

```
id: GUID (primary key)
apiKeyId: GUID (foreign key)
status: enum (Inactive, Active, Revoked, Expired)
createdAt: DateTime
deletedAt: DateTime (soft delete)
```

**Append-only enforcement:** configured with `HasNoKey()` — the context cannot issue `UPDATE` or `DELETE` on this table. Every state change is a new row. Full history is always available for audit.

### `ApiKeyAction` — permissions junction table

```
id: GUID (primary key)
apiKeyId: GUID (foreign key)
action: enum (Read, Write, Delete, Execute)
createdAt: DateTime
deletedAt: DateTime (soft delete)
```

Permissions are stored separately so they can be granted or revoked independently of key state. Soft deletes preserve the record of when a permission was revoked.

### `IdempotencyKey` — deduplication for retries

```
id: GUID (primary key)
idempotencyKeyHash: string (unique index)
apiKeyId: GUID (foreign key)
cachedResponse: string (JSON)
createdAt: DateTime
expiresAt: DateTime
deletedAt: DateTime (soft delete)
```

Used to detect duplicate requests: if the same `Idempotency-Key` header is sent twice, return the cached response instead of creating a second key.

### Query filters

Repositories automatically exclude soft-deleted rows. The EF Core context has query filters on entities with a `DeletedAt` column:

```csharp
modelBuilder.Entity<ApiKeyStatus>()
    .HasQueryFilter(x => x.DeletedAt == null);
```

This means queries are safe by default — you don't have to remember to filter out deleted rows.

---

## Cryptography and key management

Locksmith implements multiple layers of cryptographic security. See [ADR-002](../Decisions/ADR-002-api-key-creation.md) for the full design rationale.

### Key generation

Raw API key secrets are generated using `RandomNumberGenerator.GetBytes()` — cryptographically random, not pseudo-random.

```csharp
var secret = RandomNumberGenerator.GetBytes(32); // 256 bits
var base64Secret = Convert.ToBase64String(secret);
```

This raw secret is returned to the caller exactly once. It is **never** logged and **never** persisted in plaintext.

### Hashing for deduplication

Idempotency keys are hashed with SHA-256 (no salt) for fast lookup deduplication:

```csharp
var hash = SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey));
var hexHash = Convert.ToHexString(hash).ToLower();
```

This allows O(1) lookup: present the idempotency key → hash it → query the database for that hash → if found, return cached response.

### Encryption at rest

Raw API key secrets are encrypted with **AES-256-GCM** using a derived encryption key (DEK):

1. **DEK derivation:** Argon2id KDF derives a unique DEK per key from a master salt:
   ```csharp
   var dek = Argon2id.ComputeHash(
       password: masterSalt,
       salt: perKeySalt,
       iterations: settings.Iterations,
       memorySize: settings.MemorySize,
       parallelism: settings.Parallelism
   );
   ```

2. **Encryption:** AES-256-GCM encrypts the raw secret:
   ```csharp
   var (ciphertext, nonce, tag) = AesGcm.Encrypt(plaintext: secret, dek);
   ```

3. **Storage:** ciphertext, nonce, and tag are stored in the database. The raw secret is discarded.

4. **Retrieval:** Application service decrypts and returns the raw secret on request.

### Constant-time comparison

All token and key comparisons use `CryptographicOperations.FixedTimeEquals()` to prevent timing-based attacks:

```csharp
var isValid = CryptographicOperations.FixedTimeEquals(
    expected: storedHash,
    actual: presentedHash
);
```

This takes the same amount of time regardless of where the strings first differ, preventing attackers from using response time to guess valid prefixes.

---

## Design principles

Five core principles guide all architectural decisions in Locksmith:

1. **Hash at rest, never store secrets** — raw keys are ephemeral; only hashes and salts persist in the database.

2. **Append-only audit log** — state transitions are new database rows, never updates; the full history of every key is always available and can never be overwritten.

3. **Least privilege by default** — keys are created in an Inactive state with no permissions; activation and permission grants are explicit operations.

4. **Constant-time comparison everywhere** — all token and key comparisons use timing-safe equality to prevent side-channel attacks.

5. **Bounded blast radius** — a compromised API key is limited to exactly the actions assigned to it; a leaked management token can be rotated without touching key data.

---

## Technology decisions

Specific architectural choices and their reasoning are documented in Architecture Decision Records (ADRs). Don't duplicate that reasoning here — link instead:

- **[ADR-001: API Key Lifecycle](../Decisions/ADR-001-api-key-lifecycle.md)** — state machine design and append-only history
- **[ADR-002: API Key Creation](../Decisions/ADR-002-api-key-creation.md)** — cryptography, DEK derivation, encryption at rest
- **[ADR-003: Action Management](../Decisions/ADR-003-api-key-action-management.md)** — permissions model and least-privilege design
- **[ADR-004: Authentication](../Decisions/ADR-004-authentication.md)** — why static bearer tokens instead of JWT/OIDC

---

## Technology stack

| Concern | Technology |
|---|---|
| Framework | .NET 10, ASP.NET Core |
| ORM | Entity Framework Core |
| Database | PostgreSQL |
| Cache | Redis |
| Logging | Serilog (structured logging) |
| Testing | xUnit, Moq, FluentAssertions |
| Code style | StyleCop.Analyzers with `.editorconfig` |

---

## Security boundaries

What data never leaves the server? What is the attack surface?

### Secrets never leave plaintext

- Raw API key secrets are encrypted at rest (AES-256-GCM) and returned only once during creation.
- Management bearer token is stored in environment variables only, never in code or logs.
- All comparisons (token, hash) use constant-time equality.

### Audit trail is immutable

- State history (`ApiKeyStatus` table) is append-only: the application cannot update or delete rows.
- Soft deletes preserve evidence of when keys and permissions were revoked.
- Every state transition is timestamped; the full history is always queryable.

### Attack surface is narrowed

- All endpoints (except health checks) require valid bearer token.
- Request body size is limited to prevent DoS via large payloads.
- Rate limiting (planned) will throttle per-caller to prevent brute-force attacks.

For threat-specific analysis, see [Security/threat-model.md](../Security/threat-model.md).
